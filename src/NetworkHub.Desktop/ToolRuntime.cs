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
    private ZapretResponse? inspectedTg;
#if OPENNET_TESTING
    private readonly Dictionary<string,OwnedIdentity> stubs=new();
    private StubSupervisor? supervisor;
    internal Func<string,string,Exception?>? FaultForTests {get;set;}
    internal Func<string,ToolObservation?>? ObservationForTests {get;set;}
    internal Func<string,ProcessObservation[]>? ProcessListForTests {get;set;}
    internal Func<string,OwnedReceipt>? ExternalCandidateForTests {get;set;}
    internal Func<string,OwnedReceipt,Task<string>>? StopExternalForTests {get;set;}
#endif
    private string tgStamp="";
    private string? tgValidation;
    public int LaunchCount {get;private set;}
    public bool TestMode=>testMode;
    public event Action<string>? ProgressChanged;
    private async Task<Process> StartHelper(ProcessStartInfo info)
    {
        ProgressChanged?.Invoke(L.T("Waiting for Windows permission…"));
        var timer=Stopwatch.StartNew();
        try{return await Task.Run(()=>Process.Start(info))??throw new IOException("Windows did not start the helper.");}
        finally{files.Log("Timing helper UAC/start: "+timer.ElapsedMilliseconds+" ms");}
    }
    string simulatedProfile="";
    internal string ActiveZapretProfile=>testMode?simulatedProfile:owned.TryGetValue("zapret",out var receipt)?receipt.Profile??"":"";
    public ToolRuntime(AppFiles files,bool testMode)
    {
#if !OPENNET_TESTING
        if(testMode)throw new InvalidOperationException("Safe mode is not included in this release.");
#endif
        this.files=files;this.testMode=testMode;
        // A user-writable receipt alone does not prove that a previous shell
        // launched a live process. After restart, classify it as external and
        // require an explicit confirmation until durable provenance is available.
    }
    private string ReceiptPath(string id)=>Paths.Owned(files.Data,"owned/"+id+".json");
    public ToolObservation Observe(string id,UserSettings settings)
    {
        try
        {
#if OPENNET_TESTING
            if(testMode)
            {
                if(ObservationForTests?.Invoke(id) is ToolObservation injected)return injected;
                if(stubs.TryGetValue(id,out var stub)) return new(id,supervisor!.State(stub.Token)==ModuleState.Running ? ObservedState.Running : ObservedState.Stopped,L.T("Safe test process"),stub.Pid);
                return new(id,ObservedState.Stopped,L.T("Test mode: network tools are not launched."));
            }
#endif
            var external=
#if OPENNET_TESTING
                ProcessListForTests?.Invoke(id)??
#endif
                ProcessObserver.Find(id=="zapret" ? "winws" : "TgWsProxy_windows");
            if(owned.TryGetValue(id,out var receipt))
            {
                var observed=ProcessIdentity.ObserveOwned(receipt);
                if(observed.State==ObservedState.Running&&external.Any(x=>x.Pid!=receipt.Pid&&!(id=="tg"&&ProcessObserver.IsDirectChild(x,receipt.Pid,receipt.StartFileTime))))return new(id,ObservedState.External,L.T("Multiple instances are running. No automatic stop is available."));
                if(observed.State!=ObservedState.Stopped) return observed;
                owned.Remove(id);
            }
            if(external.Length>0) return new(id,ObservedState.External,L.T("An external instance is running."),external[0].Pid);
            if(id=="zapret")
            {
                if(string.IsNullOrWhiteSpace(settings.ZapretDirectory)||!Directory.Exists(settings.ZapretDirectory))return new(id,ObservedState.NotInstalled,L.T("Click Install to obtain the official compatible release."));
                // A loaded shared driver is not a running Zapret or a reason to
                // unload it. winws opens its own handle and reports launch errors.
                files.ValidateIsolation(settings);
                if(!NetworkHub.Adapters.ReviewedStrategy.Supported(settings.ZapretDirectory,settings.Strategy,files.ForZapret(settings).TrustedFiles)) return new(id,ObservedState.Error,L.T("The selected strategy is unsupported, changed, or has no matching official installation record. Check the strategy and installed Zapret files."));
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
        string launchedStrategy=settings.Strategy;
        var current=await Task.Run(()=>Observe(id,settings));
        if(current.State==ObservedState.Running||current.State==ObservedState.External||current.State==ObservedState.Unknown) return current.Detail;
        if(current.State==ObservedState.Error) throw new InvalidDataException(current.Detail);
        if(current.State==ObservedState.CleanupRequired)throw new InvalidOperationException(current.Detail);
#if OPENNET_TESTING
        if(testMode)
        {
            if(FaultForTests?.Invoke("start",id) is Exception fault)throw fault;
            string host=Environment.ProcessPath!,stub=host;
            supervisor??=new StubSupervisor(host,stub,Metadata.Hash(stub),Paths.Owned(files.Data,"stubs"),true);
            stubs[id]=await Task.Run(()=>supervisor.Start());if(id=="zapret")simulatedProfile=launchedStrategy; LaunchCount++; return L.T("Test process started.");
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
        await Task.Run(()=>ZapretPreparation.Prepare(files,settings));
        string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token); Directory.CreateDirectory(folder);
        var parent=ProcessIdentity.Read(Environment.ProcessId);
        if(!parent.IdentityReadable) throw new InvalidOperationException(L.T("Could not verify the Open Net process. Check the diagnostic log."));
        Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime)));
        var psi=new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=true,Verb="runas",WorkingDirectory=files.Root};
        psi.ArgumentList.Add("--elevated");psi.ArgumentList.Add(token);psi.ArgumentList.Add("--data-root");psi.ArgumentList.Add(files.Data);
        using var helper=await StartHelper(psi) ?? throw new InvalidOperationException(L.T("The Zapret helper did not start. Check the UAC prompt and diagnostic log."));
        string response=Path.Combine(folder,"response.json");
        // The one-shot helper exits after publishing the verified child receipt.
        var deadline=Stopwatch.StartNew();
        while(!File.Exists(response))
        {
            if(helper.HasExited&&!File.Exists(response)) throw new InvalidOperationException(L.T("The helper exited without a result. Check the diagnostic log."));
            if(deadline.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(L.T("The helper did not respond within 30 seconds. Check the diagnostic log."));
            await Task.Delay(100);
        }
        var r=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response)) ?? throw new InvalidDataException(L.T("No launch result was returned. Check the diagnostic log."));
        if(!r.Success||r.Receipt==null) throw new InvalidOperationException(r.Message);
        Store(r.Receipt with{Profile=launchedStrategy}); LaunchCount++;
        var launchState=ProcessIdentity.ObserveOwned(r.Receipt);
        files.Log("Zapret launch: process identity check="+launchState.State+"; strategy="+Path.GetFileName(launchedStrategy)+"; network availability not checked.");
        if(launchState.State!=ObservedState.Running)throw new InvalidOperationException(L.T("Zapret was launched but its process could not be confirmed. Check the diagnostic log."));
        return L.T("Zapret started without a console window; its process is being checked.");
    }
    private void Store(OwnedReceipt receipt)
    {
        owned[receipt.Module]=receipt; string p=ReceiptPath(receipt.Module); Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p,JsonSerializer.Serialize(receipt));
    }
    public OwnedReceipt IdentifyExternal(string id,UserSettings settings)
    {
#if OPENNET_TESTING
        if(testMode&&ExternalCandidateForTests!=null)return ExternalCandidateForTests(id);
#endif
        if((id=="zapret"&&string.IsNullOrWhiteSpace(settings.ZapretDirectory))||(id=="tg"&&string.IsNullOrWhiteSpace(settings.TgExe)))throw new InvalidOperationException(L.T("The external instance is not from the selected verified installation. No process was stopped."));
        string expected=id switch {"zapret"=>Path.GetFullPath(Path.Combine(settings.ZapretDirectory,"bin","winws.exe")),"tg"=>Path.GetFullPath(settings.TgExe),_=>throw new InvalidOperationException("Unsupported module")};
        Paths.NoReparseAncestors(expected);
        bool verified=id=="zapret"
            ?files.ForZapret(settings).TrustedFiles.TryGetValue("bin/winws.exe",out var hash)&&File.Exists(expected)&&string.Equals(Metadata.Hash(expected),hash,StringComparison.OrdinalIgnoreCase)
            :ModuleTrust.Matches(files,"tg",expected);
        if(!verified||!ExternalProcessControl.ReviewedBinary(id,Metadata.Hash(expected)))throw new InvalidOperationException(L.T("The external instance is not from the selected verified installation. No process was stopped."));
        return ExternalProcessControl.SelectInstance(id,expected,ProcessObserver.Find(id=="zapret"?"winws":"TgWsProxy_windows"));
    }
    private async Task<ZapretResponse> TgHelper(string action,UserSettings settings,ZapretResponse? approved=null)
    {
        string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token);Directory.CreateDirectory(folder);
        var parent=ProcessIdentity.Read(Environment.ProcessId);
        var request=new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime,action){ExternalPid=approved?.Receipt?.Pid??0,ExternalStart=approved?.Receipt?.StartFileTime??0,Members=approved?.Members??Array.Empty<OwnedReceipt>()};
        Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(request));
        var psi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WorkingDirectory=files.Root};foreach(var arg in new[]{"--elevated",token,"--data-root",files.Data})psi.ArgumentList.Add(arg);
        using var helper=await StartHelper(psi)??throw new IOException("TG helper did not start");string response=Path.Combine(folder,"response.json");var timer=Stopwatch.StartNew();
        while(!File.Exists(response)){if((helper.HasExited||timer.Elapsed>TimeSpan.FromSeconds(30))&&!File.Exists(response))throw new IOException("TG helper did not return a result");await Task.Delay(100);}
        var result=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response))??throw new InvalidDataException("Invalid TG response");if(!result.Success)throw new InvalidOperationException(result.Message);return result;
    }
    internal async Task<OwnedReceipt> InspectExternal(string id,UserSettings settings)
    {
#if OPENNET_TESTING
        if(testMode)return IdentifyExternal(id,settings);
#endif
        if(id!="tg")return await Task.Run(()=>IdentifyExternal(id,settings));
        try{await TgRuntimeControl.VerifyOfficial(files,settings.TgExe);inspectedTg=await Task.Run(()=>TgRuntimeControl.Inspect(settings.TgExe));}
        catch(System.ComponentModel.Win32Exception){inspectedTg=await TgHelper("inspect-tg",settings);}
        catch(InvalidOperationException) when(ProcessObserver.Find("TgWsProxy_windows").Any(p=>!p.MetadataAccessible)){inspectedTg=await TgHelper("inspect-tg",settings);}
        return inspectedTg.Receipt!;
    }
    public async Task<string> StopExternal(string id,UserSettings settings,OwnedReceipt approved)
    {
#if OPENNET_TESTING
        if(testMode&&StopExternalForTests!=null)return await StopExternalForTests(id,approved);
#endif
        if(id=="tg")
        {
            var approvedTree=inspectedTg??throw new InvalidOperationException("Inspect TG before confirming stop");
            if(approvedTree.Receipt?.Pid!=approved.Pid||approvedTree.Receipt.StartFileTime!=approved.StartFileTime)throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
            await TgHelper("stop-tg",settings,approvedTree);inspectedTg=null;return L.T("The process stopped.");
        }
        var current=await Task.Run(()=>IdentifyExternal(id,settings));
        if(current.Pid!=approved.Pid||current.StartFileTime!=approved.StartFileTime||!string.Equals(current.Executable,approved.Executable,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
        {
            string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token);Directory.CreateDirectory(folder);
            var parent=ProcessIdentity.Read(Environment.ProcessId);if(!parent.IdentityReadable)throw new InvalidOperationException(L.T("Could not verify Open Net. Check the diagnostic log."));
            var request=new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime,"stop-external"){ExternalPid=current.Pid,ExternalStart=current.StartFileTime,ExternalExecutable=current.Executable};
            Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(request));
            var psi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WorkingDirectory=files.Root};psi.ArgumentList.Add("--elevated");psi.ArgumentList.Add(token);psi.ArgumentList.Add("--data-root");psi.ArgumentList.Add(files.Data);
            using var helper=await StartHelper(psi)??throw new InvalidOperationException(L.T("The stop helper did not start. Check the UAC prompt and diagnostic log."));
            string response=Path.Combine(folder,"response.json");var deadline=Stopwatch.StartNew();
            while(!File.Exists(response)){if(helper.HasExited&&!File.Exists(response))throw new InvalidOperationException(L.T("The stop helper exited without a result. Check the diagnostic log."));if(deadline.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(L.T("The stop helper did not respond. Check the diagnostic log."));await Task.Delay(100);}
            var result=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response));if(result?.Success!=true)throw new InvalidOperationException(result?.Message??L.T("Stop was not confirmed. Check the process and diagnostic log."));
        }
        if(ProcessObserver.Find(id=="zapret"?"winws":"TgWsProxy_windows").Length!=0)throw new InvalidOperationException(L.T("Another instance is still running. Check the module status."));
        return L.T("The external process stopped.");
    }
    public async Task<string> Stop(string id,UserSettings settings)
    {
#if OPENNET_TESTING
        if(testMode && stubs.TryGetValue(id,out var stub)) {if(FaultForTests?.Invoke("stop",id) is Exception fault)throw fault;await Task.Run(()=>supervisor!.Stop(stub.Token));stubs.Remove(id);return L.T("Test process stopped.");}
#endif
        var state=await Task.Run(()=>Observe(id,settings));
        if(state.State==ObservedState.External) return L.T("The external instance was left unchanged.");
        if(state.State is ObservedState.Stopped or ObservedState.Error or ObservedState.NotInstalled) return L.T("The process is not running.");
        OwnedReceipt? receipt=null;
        if(state.State==ObservedState.Running)owned.TryGetValue(id,out receipt);
        else if(state.State==ObservedState.CleanupRequired&&id=="zapret")receipt=JsonSerializer.Deserialize<OwnedReceipt>(File.ReadAllText(ReceiptPath(id)));
        if(receipt==null)throw new InvalidOperationException(L.T("Process ownership could not be verified. It was left unchanged."));
        if(id=="tg"){await InspectExternal(id,settings);await TgHelper("stop-tg",settings,inspectedTg);inspectedTg=null;}
        else
        {
            string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"requests/"+token);Directory.CreateDirectory(folder);
            var parent=ProcessIdentity.Read(Environment.ProcessId);
            if(!parent.IdentityReadable)throw new InvalidOperationException(L.T("Could not verify Open Net. Check the diagnostic log."));
            Paths.WriteNew(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(new ZapretRequest(token,settings,Environment.ProcessId,parent.StartFileTime,"stop")));
            var psi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas"};
            psi.ArgumentList.Add("--elevated");psi.ArgumentList.Add(token);psi.ArgumentList.Add("--data-root");psi.ArgumentList.Add(files.Data);
            using var helper=await StartHelper(psi)??throw new InvalidOperationException(L.T("The stop helper did not start. Check the UAC prompt and diagnostic log."));
            string response=Path.Combine(folder,"response.json");var deadline=Stopwatch.StartNew();
            while(!File.Exists(response))
            {
                if(helper.HasExited&&!File.Exists(response))throw new InvalidOperationException(L.T("The stop helper exited without a result. Check the diagnostic log."));
                if(deadline.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException(L.T("The stop helper did not respond. Check the diagnostic log."));
                await Task.Delay(100);
            }
            var result=JsonSerializer.Deserialize<ZapretResponse>(File.ReadAllText(response));
            if(result?.Success!=true)throw new InvalidOperationException(result?.Message??L.T("Stop was not confirmed. Check the process and diagnostic log."));
        }
        if(ProcessIdentity.ObserveOwned(receipt).State!=ObservedState.Stopped)throw new InvalidOperationException(L.T("The process is still running. Stop was not confirmed."));
        owned.Remove(id);if(id!="zapret")File.Delete(ReceiptPath(id));
        return L.T("The process stopped.");
    }
    public void CloseShell()
    {
        // v0.1 shell exit never kills network processes, even when they were started by this installation.
#if OPENNET_TESTING
        if(testMode) supervisor?.Dispose();
#endif
    }
}







