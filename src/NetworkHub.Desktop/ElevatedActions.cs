using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using NetworkHub.App;
using NetworkHub.Core;

internal static class ElevatedActions
{
    internal static bool SameLaunch(OwnedReceipt? original,OwnedReceipt saved)=>original!=null&&original.Token==saved.Token&&original.Module==saved.Module&&original.Pid==saved.Pid&&original.StartFileTime==saved.StartFileTime&&string.Equals(original.Executable,saved.Executable,StringComparison.OrdinalIgnoreCase);
    // Fixed start/stop actions only; stop target is read from this installation's receipt.
    public static int Run(string[] args)
    {
        if(args.Length!=4 || args[0]!="--elevated" || args[2]!="--data-root" || !Guid.TryParseExact(args[1],"N",out _)) return 2;
        string root=Path.GetDirectoryName(Environment.ProcessPath!)!;
        var files=new AppFiles(root,args[3]);
        string token=args[1],request=Paths.Owned(files.Data,"requests/"+token+"/request.json"),response=Paths.Owned(files.Data,"requests/"+token+"/response.json");
        try
        {
            using var identity=WindowsIdentity.GetCurrent();
            if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException(L.T("The helper requires explicit UAC permission."));
            // Reject already completed requests. Concurrent launches are serialized by the fixed module mutex.
            var r=JsonSerializer.Deserialize<ZapretRequest>(File.ReadAllText(request)) ?? throw new InvalidDataException("Invalid request");
            L.Set(r.Settings.Language);
            if(r.Token!=token || File.Exists(response)) throw new InvalidDataException(L.T("The request is invalid or already processed. Execution was blocked."));
            var parent=ProcessIdentity.Read(r.ParentPid);
            string expected=Environment.ProcessPath!;
            if(!parent.Exists||!parent.IdentityReadable||parent.StartFileTime!=r.ParentStart||!string.Equals(parent.Executable,expected,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L.T("The request does not belong to this Open Net instance. Execution was blocked."));
            if(r.Action is "inspect-tg" or "stop-tg")
            {
                TgRuntimeControl.VerifyOfficial(files,r.Settings.TgExe).GetAwaiter().GetResult();
                var inspected=TgRuntimeControl.Inspect(r.Settings.TgExe);
                if(r.Action=="stop-tg")
                {
                    if(inspected.Receipt!.Pid!=r.ExternalPid||inspected.Receipt.StartFileTime!=r.ExternalStart||!TgRuntimeControl.SameMembers(r.Members,inspected.Members))throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
                    OwnedStop.Stop(inspected.Receipt,true,true,r.Members);
                    if(ProcessObserver.Find("TgWsProxy_windows").Length!=0)throw new InvalidOperationException(L.T("Another instance is still running. Check the module status."));
                    inspected=new(true,L.T("The process stopped."),null);
                }
                PublishResponse(response,JsonSerializer.Serialize(inspected));return 0;
            }
            if(r.Action=="stop-external")
            {
                string binary=Path.GetFullPath(Path.Combine(r.Settings.ZapretDirectory,"bin","winws.exe"));
                Paths.NoReparseAncestors(binary);
                var hashes=files.ForZapret(r.Settings).TrustedFiles;
                if(!hashes.TryGetValue("bin/winws.exe",out var verified)||!File.Exists(binary)||!string.Equals(Metadata.Hash(binary),verified,StringComparison.OrdinalIgnoreCase)||!ExternalProcessControl.ReviewedBinary("zapret",verified))throw new InvalidDataException(L.T("The external instance cannot be identified uniquely. No process was stopped."));
                var candidate=ExternalProcessControl.Select("zapret",binary,ProcessObserver.Find("winws"),ProcessIdentity.Read,ExternalProcessControl.IsServiceProcess);
                if(candidate.Pid!=r.ExternalPid||candidate.StartFileTime!=r.ExternalStart||!string.Equals(candidate.Executable,r.ExternalExecutable,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
                OwnedStop.Stop(candidate,false);
                if(ProcessObserver.Find("winws").Length!=0)throw new InvalidOperationException(L.T("Another instance is still running. Check the module status."));
                PublishResponse(response,JsonSerializer.Serialize(new ZapretResponse(true,L.T("The external process stopped. The driver was not changed."),null)));return 0;
            }
            if(r.Action=="stop")
            {
                var receiptPath=Paths.Owned(files.Data,"owned/zapret.json");
                var owned=JsonSerializer.Deserialize<OwnedReceipt>(File.ReadAllText(receiptPath)) ?? throw new InvalidDataException(L.T("No owned Zapret receipt exists. The process was left unchanged."));
                string binary=Path.GetFullPath(Path.Combine(r.Settings.ZapretDirectory,"bin","winws.exe"));
                if(owned.Module!="zapret"||!Guid.TryParseExact(owned.Token,"N",out _)||!string.Equals(owned.Executable,binary,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("The Zapret receipt does not match the selected build. Stop was blocked."));
                var launchResponse=Paths.Owned(files.Data,"requests/"+owned.Token+"/response.json");
                var original=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(launchResponse));
                if(original?.Success!=true||!SameLaunch(original.Receipt,owned))throw new InvalidDataException(L.T("No confirmed launch of this Zapret exists. Stop was blocked."));
                var hashes=files.ForZapret(r.Settings).TrustedFiles;
                if(Metadata.Hash(binary)!=(hashes.TryGetValue("bin/winws.exe",out var verified)?verified:"AFFB4F69D2EA302A7ABCCD5325D81826E140DDAE014F1E070BC4A6C0DD555188"))throw new InvalidDataException(L.T("The winws file was modified. Reinstall the official release."));
                OwnedStop.Stop(owned,false);
                PublishResponse(response,JsonSerializer.Serialize(new ZapretResponse(true,L.T("The process stopped."),owned)));
                return 0;
            }
            if(r.Action!="start")throw new InvalidDataException(L.T("Unknown action. Execution was blocked."));
            using var moduleLock=new System.Threading.Mutex(false,"Local\\NetworkHub-Zapret-Start-0.1");
            if(!moduleLock.WaitOne(0)) throw new InvalidOperationException(L.T("Another Zapret launch request is still running. Wait and retry."));
            try
            {
                if(ProcessObserver.Find("winws").Length>0) throw new InvalidOperationException(L.T("Zapret is already running. The existing instance was left unchanged."));
                var plan=ZapretPreparation.Prepare(files,r.Settings);
                Directory.CreateDirectory(Path.GetDirectoryName(plan.LogFile)!);
                // Preserve the exact effective plan for first/repeat comparisons.
                // This local diagnostic never runs the BAT or changes driver state.
                Paths.WriteNew(Path.Combine(Path.GetDirectoryName(response)!,"launch-plan.json"),JsonSerializer.Serialize(new{
                    Strategy=Path.GetFullPath(Path.Combine(r.Settings.ZapretDirectory,r.Settings.Strategy)),
                    StrategySHA256=Metadata.Hash(Path.Combine(r.Settings.ZapretDirectory,r.Settings.Strategy)),
                    Executable=plan.Executable,ExecutableSHA256=Metadata.Hash(plan.Executable),
                    WorkingDirectory=plan.WorkingDirectory,Arguments=plan.Redacted().Arguments,
                    PreparedUtc=DateTime.UtcNow,WebsiteAvailability="Not tested"
                },new JsonSerializerOptions{WriteIndented=true}));
                using var process=DetachedToolProcess.Start(plan);
                if(process.WaitForExit(750))throw new InvalidOperationException(L.T("Zapret exited during startup. Check the module output log; website availability was not tested."));
                var p=ProcessIdentity.Read(process.Id);
                if(!p.IdentityReadable) throw new InvalidOperationException(L.T("The process was created but ownership is not confirmed. Stop it manually."));
                var receipt=new OwnedReceipt(token,process.Id,p.StartFileTime,p.Executable!,"zapret");
                PublishResponse(response,JsonSerializer.Serialize(new ZapretResponse(true,L.T("The process was created; its state is being checked."),receipt)));
                // The child owns its output handle; the elevated helper can exit.
                return 0;
            }
            finally {moduleLock.ReleaseMutex();}
        }
        catch(Exception ex)
        {
            files.Log("Elevated action failed: "+ex);
            try { if(!File.Exists(response)) PublishResponse(response,JsonSerializer.Serialize(new ZapretResponse(false,LogSafety.Redact(ex.Message),null))); }
            catch(Exception writeError){files.Log("Cannot publish elevated response: "+writeError);}
            return 1;
        }
    }
    private static void PublishResponse(string path,string text){string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";Paths.WriteNew(temporary,text);File.Move(temporary,path,false);}
}





