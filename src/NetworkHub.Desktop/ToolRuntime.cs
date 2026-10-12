using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;

namespace NetworkHub.Desktop;
public sealed class ToolRuntime
{
    private readonly AppFiles files;
    private readonly bool testMode;
    private readonly Dictionary<string,OwnedReceipt> owned=new();
#if OPENNET_TESTING
    private readonly Dictionary<string,OwnedIdentity> stubs=new();
    private StubSupervisor? supervisor;
#endif
    private string tgStamp="";
    private string? tgValidation;
    public int LaunchCount {get;private set;}
    public bool TestMode=>testMode;
    public ToolRuntime(AppFiles files,bool testMode)
    {
#if !OPENNET_TESTING
        if(testMode)throw new InvalidOperationException("Safe mode is not included in this release.");
#endif
        this.files=files;this.testMode=testMode;
        if(!testMode) foreach(string id in ModuleCatalog.All.Select(x=>x.Id).ToArray())
        {
            string p=ReceiptPath(id); if(File.Exists(p))
            {try {var r=JsonSerializer.Deserialize<OwnedReceipt>(File.ReadAllText(p)); if(r!=null&&r.Module==id&&Guid.TryParseExact(r.Token,"N",out _)) owned[id]=r;} catch(Exception ex) {files.Log("Receipt: "+ex.Message);} }
        }
    }
    private string ReceiptPath(string id)=>Paths.Owned(files.Data,"owned/"+id+".json");
    public ToolObservation Observe(string id,UserSettings settings)
    {
        try
        {
#if OPENNET_TESTING
            if(testMode)
            {
                if(stubs.TryGetValue(id,out var stub)) return new(id,supervisor!.State(stub.Token)==ModuleState.Running ? ObservedState.Running : ObservedState.Stopped,L.T("Safe test process"),stub.Pid);
                return new(id,ObservedState.Stopped,L.T("Test mode: network tools are not launched."));
            }
#endif
            if(owned.TryGetValue(id,out var receipt))
            {
                var observed=ProcessIdentity.ObserveOwned(receipt);
                if(observed.State!=ObservedState.Stopped) return observed;
                owned.Remove(id);
            }
            var external=ProcessObserver.Find(id=="zapret" ? "winws" : "TgWsProxy_windows");
            if(external.Length>0) return new(id,ObservedState.External,L.T("An external instance was found. It will be left unchanged."),external[0].Pid);
            if(id=="zapret")
            {
                if(string.IsNullOrWhiteSpace(settings.ZapretDirectory)||!Directory.Exists(settings.ZapretDirectory))return new(id,ObservedState.NotInstalled,L.T("Click Install to obtain the official compatible release."));
                if(File.Exists(ReceiptPath(id))&&DriverControl.Loaded(settings.ZapretDirectory))return new(id,ObservedState.CleanupRequired,L.T("The process stopped but WinDivert is still loaded. Click Stop to complete cleanup."));
                files.ValidateIsolation(settings);
                if(!NetworkHub.Adapters.ReviewedStrategy.Supported(settings.ZapretDirectory,settings.Strategy,files.ForZapret(settings).TrustedFiles)) return new(id,ObservedState.Error,L.T("This strategy is not reviewed or its BAT was modified. Select general.bat or general (ALT13).bat from the official 1.10.3 bundle; no automatic substitution."));
                var c=new NetworkHub.Adapters.ZapretAdapter(files.ForZapret(settings)).Validate().FirstOrDefault(x=>x.Error);
                if(c!=null) return new(id,ObservedState.Error,Translate(c));
            }
            else
            {
                if(string.IsNullOrWhiteSpace(settings.TgExe)||!File.Exists(settings.TgExe))return new(id,ObservedState.NotInstalled,L.T("Click Install to obtain the Windows x64 EXE."));
                var info=new FileInfo(settings.TgExe);string stamp=settings.TgExe+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;
                if(stamp!=tgStamp){tgValidation=new TgExeAdapter(files).Validate(settings.TgExe);tgStamp=stamp;}
                if(tgValidation!=null)return new(id,ObservedState.Error,tgValidation);
            }
            return new(id,ObservedState.Stopped,L.T("Ready to start."));
        }
        catch(Exception ex) { files.Log(id+": "+ex.Message); return new(id,ObservedState.Unknown,L.T("Could not verify the files or process. Check the diagnostic log.")); }
    }
    private static string Translate(Check check) => check.Code switch {
        "MissingBinary" or "MissingStrategy" or "MissingData"=>L.T("Zapret files are missing. Check the folder in Settings."),
        "UnsupportedBinary" or "UnsupportedStrategy"=>L.T("This build or strategy differs from the reviewed version. Install the compatible release or select a reviewed strategy."),
        "EmptyList"=>L.T("An empty list was found. Check the list file before starting."),
        "StrategyInvalid"=>L.Error(new InvalidDataException(check.Message)),
        _=>L.T("Zapret preparation failed. Check the diagnostic log and selected files.")
    };
    public async Task<string> Start(string id,UserSettings settings,bool launchAuthorized=false)
    {
        if(!testMode&&!launchAuthorized) throw new InvalidOperationException(L.T("A real launch requires explicit user confirmation."));
        var current=Observe(id,settings);
        if(current.State==ObservedState.Running||current.State==ObservedState.External||current.State==ObservedState.Unknown) return current.Detail;
        if(current.State==ObservedState.Error) throw new InvalidDataException(current.Detail);
        if(current.State==ObservedState.CleanupRequired)throw new InvalidOperationException(current.Detail);
#if OPENNET_TESTING
        if(testMode)
        {
            string host=Environment.ProcessPath!,stub=host;
            supervisor??=new StubSupervisor(host,stub,Metadata.Hash(stub),Paths.Owned(files.Data,"stubs"),true);
            stubs[id]=await Task.Run(()=>supervisor.Start()); LaunchCount++; return L.T("Test process started.");
        }
#endif
        if(id=="tg")
        {
            using var p=Process.Start(new TgExeAdapter(files).Plan(settings.TgExe)) ?? throw new InvalidOperationException(L.T("TG WS Proxy did not start. Check the diagnostic log."));
            var identity=ProcessIdentity.Read(p.Id);
            if(!identity.Exists||!identity.IdentityReadable) return L.T("TG was launched but its process is not confirmed yet; check its tray.");
            Store(new OwnedReceipt(Guid.NewGuid().ToString("N"),p.Id,identity.StartFileTime,identity.Executable!,id)); LaunchCount++;
            return L.T("TG started normally. Its own tray is available.");
        }
        _=ZapretPreparation.Prepare(files,settings);
        string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token); Directory.CreateDirectory(folder);
        var parent=ProcessIdentity.Read(Environment.ProcessId);
        if(!parent.IdentityReadable) throw new InvalidOperationException(L.T("Could not verify the Open Net process. Check the diagnostic log."));
        Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime)));
        var psi=new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=true,Verb="runas",WorkingDirectory=files.Root};
        psi.ArgumentList.Add("--elevated");psi.ArgumentList.Add(token);psi.ArgumentList.Add("--data-root");psi.ArgumentList.Add(files.Data);
        using var helper=Process.Start(psi) ?? throw new InvalidOperationException(L.T("The Zapret helper did not start. Check the UAC prompt and diagnostic log."));
        string response=Path.Combine(folder,"response.json");
        // A log-pump helper intentionally remains alive with its own winws.
        var deadline=Stopwatch.StartNew();
        while(!File.Exists(response))
        {
            if(helper.HasExited) throw new InvalidOperationException(L.T("The helper exited without a result. Check the diagnostic log."));
            if(deadline.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(L.T("The helper did not respond within 30 seconds. Check the diagnostic log."));
            await Task.Delay(100);
        }
        var r=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response)) ?? throw new InvalidDataException(L.T("No launch result was returned. Check the diagnostic log."));
        if(!r.Success||r.Receipt==null) throw new InvalidOperationException(r.Message);
        Store(r.Receipt); LaunchCount++;
        if(ProcessIdentity.ObserveOwned(r.Receipt).State==ObservedState.Running){settings.LastWorkingStrategy=settings.Strategy;files.Save(settings);}
        return L.T("Zapret started without a console window; its process is being checked.");
    }
    private void Store(OwnedReceipt receipt)
    {
        owned[receipt.Module]=receipt; string p=ReceiptPath(receipt.Module); Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p,JsonSerializer.Serialize(receipt));
    }
    public async Task<string> Stop(string id,UserSettings settings)
    {
#if OPENNET_TESTING
        if(testMode && stubs.TryGetValue(id,out var stub)) {await Task.Run(()=>supervisor!.Stop(stub.Token));stubs.Remove(id);return L.T("Test process stopped.");}
#endif
        var state=Observe(id,settings);
        if(state.State==ObservedState.External) return L.T("The external instance was left unchanged.");
        if(state.State is ObservedState.Stopped or ObservedState.Error or ObservedState.NotInstalled) return L.T("The process is not running.");
        OwnedReceipt? receipt=null;
        if(state.State==ObservedState.Running)owned.TryGetValue(id,out receipt);
        else if(state.State==ObservedState.CleanupRequired&&id=="zapret")receipt=JsonSerializer.Deserialize<OwnedReceipt>(File.ReadAllText(ReceiptPath(id)));
        if(receipt==null)throw new InvalidOperationException(L.T("Process ownership could not be verified. It was left unchanged."));
        if(id=="tg")await Task.Run(()=>OwnedStop.Stop(receipt,true));
        else
        {
            string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token);Directory.CreateDirectory(folder);
            var parent=ProcessIdentity.Read(Environment.ProcessId);
            if(!parent.IdentityReadable)throw new InvalidOperationException(L.T("Could not verify Open Net. Check the diagnostic log."));
            Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime,"stop")));
            var psi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas"};
            psi.ArgumentList.Add("--elevated");psi.ArgumentList.Add(token);psi.ArgumentList.Add("--data-root");psi.ArgumentList.Add(files.Data);
            using var helper=Process.Start(psi)??throw new InvalidOperationException(L.T("The stop helper did not start. Check the UAC prompt and diagnostic log."));
            string response=Path.Combine(folder,"response.json");var deadline=Stopwatch.StartNew();
            while(!File.Exists(response))
            {
                if(helper.HasExited)throw new InvalidOperationException(L.T("The stop helper exited without a result. Check the diagnostic log."));
                if(deadline.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(L.T("The stop helper did not respond. Check the diagnostic log."));
                await Task.Delay(100);
            }
            var result=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response));
            if(result?.Success!=true)throw new InvalidOperationException(result?.Message??L.T("Stop was not confirmed. Check the process and diagnostic log."));
        }
        if(ProcessIdentity.ObserveOwned(receipt).State!=ObservedState.Stopped)throw new InvalidOperationException(L.T("The process is still running. Stop was not confirmed."));
        owned.Remove(id);if(id!="zapret")File.Delete(ReceiptPath(id));
        return id=="zapret"?L.T("The process and WinDivert stopped."):L.T("The process stopped.");
    }
    public void CloseShell()
    {
        // v0.1 shell exit never kills network processes, even when they were started by this installation.
#if OPENNET_TESTING
        if(testMode) supervisor?.Dispose();
#endif
    }
}







