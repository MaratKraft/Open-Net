using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using NetworkHub.App;
using NetworkHub.Core;

internal static class ElevatedActions
{
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
            if(r.Action=="stop")
            {
                var receiptPath=Paths.Owned(files.Data,"owned/zapret.json");
                var owned=JsonSerializer.Deserialize<OwnedReceipt>(File.ReadAllText(receiptPath)) ?? throw new InvalidDataException(L.T("No owned Zapret receipt exists. The process was left unchanged."));
                string binary=Path.GetFullPath(Path.Combine(r.Settings.ZapretDirectory,"bin","winws.exe"));
                if(owned.Module!="zapret"||!Guid.TryParseExact(owned.Token,"N",out _)||!string.Equals(owned.Executable,binary,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("The Zapret receipt does not match the selected build. Stop was blocked."));
                var launchResponse=Paths.Owned(files.Data,"requests/"+owned.Token+"/response.json");
                var original=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(launchResponse));
                if(original?.Success!=true||original.Receipt!=owned)throw new InvalidDataException(L.T("No confirmed launch of this Zapret exists. Stop was blocked."));
                var hashes=files.ForZapret(r.Settings).TrustedFiles;
                if(Metadata.Hash(binary)!=(hashes.TryGetValue("bin/winws.exe",out var verified)?verified:"AFFB4F69D2EA302A7ABCCD5325D81826E140DDAE014F1E070BC4A6C0DD555188"))throw new InvalidDataException(L.T("The winws file was modified. Reinstall the official release."));
                OwnedStop.Stop(owned,false);
                DriverControl.Stop(r.Settings.ZapretDirectory,hashes.TryGetValue("bin/WinDivert64.sys",out var driverHash)?driverHash:null);
                Paths.WriteNew(response,JsonSerializer.Serialize(new ZapretResponse(true,L.T("Zapret and its driver stopped."),owned)));
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
                var info=plan.CreateStartInfo();
                // v0.1 deliberately does not promise a safe stop or kill-on-shell-exit for the network process.
                using var process=Process.Start(info) ?? throw new InvalidOperationException(L.T("winws did not start. Check the diagnostic log."));
                process.OutputDataReceived+=(_,e)=>{if(e.Data!=null) Append(plan.LogFile,e.Data);};
                process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null) Append(plan.LogFile,e.Data);};
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                var p=ProcessIdentity.Read(process.Id);
                if(!p.IdentityReadable) throw new InvalidOperationException(L.T("The process was created but ownership is not confirmed. Stop it manually."));
                var receipt=new OwnedReceipt(token,process.Id,p.StartFileTime,p.Executable!,"zapret");
                Paths.WriteNew(response,JsonSerializer.Serialize(new ZapretResponse(true,L.T("The process was created; its state is being checked."),receipt)));
                // Keep only this one-shot log pump alive until winws exits. No listener, service, command server, or stop API.
                process.WaitForExit(); return process.ExitCode;
            }
            finally {moduleLock.ReleaseMutex();}
        }
        catch(Exception ex)
        {
            files.Log("Elevated action failed: "+ex);
            try { if(!File.Exists(response)) Paths.WriteNew(response,JsonSerializer.Serialize(new ZapretResponse(false,LogSafety.Redact(ex.Message),null))); }
            catch(Exception writeError){files.Log("Cannot publish elevated response: "+writeError);}
            return 1;
        }
    }
    private static readonly object logGate=new();
    private static void Append(string path,string line) {lock(logGate) {try {File.AppendAllText(path,LogSafety.Redact(line)+Environment.NewLine);} catch(Exception ex){Trace.WriteLine("Open Net output log failed: "+ex);}}}
}





