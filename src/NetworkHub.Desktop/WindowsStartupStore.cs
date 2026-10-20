using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using NetworkHub.App;

namespace NetworkHub.Desktop;

internal interface ITransactionalStartupStore
{
    object Capture(string name);
    void Rollback(string name,object snapshot);
    string Mode {get;}
}

// A single registration per installation: either the existing Run value, or an
// explicit interactive-token logon task. Passwords and network conditions are absent.
internal sealed class WindowsStartupStore(bool scheduled,IStartupStore? runStore=null,IScheduledStartupStore? taskStore=null,string? owner=null,string? approvedTaskHash=null):IStartupStore,ITransactionalStartupStore
{
    readonly IStartupStore run=runStore??new RegistryStartupStore();
    readonly IScheduledStartupStore tasks=taskStore??new NativeScheduledStartupStore();
    string Sid=>owner??WindowsIdentity.GetCurrent().User!.Value;
    public string Mode=>scheduled?"task":"run";
    internal static string ValidateXml(string xml,string name,string sid)=>StartupTaskContract.Validate(xml,name,sid);
    internal static string XmlFor(string name,string command,string sid)
    {
        if(!command.EndsWith("\" --autostart",StringComparison.Ordinal)||!command.StartsWith('"'))throw new IOException("Invalid startup command.");
        string exe=command.Substring(1,command.Length-14);if(AutostartService.CommandFor(exe)!=command)throw new IOException("Invalid startup command.");
        XNamespace n="http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string key,object value)=>new(n+key,value);
        return new XDocument(new XElement(n+"Task",new XAttribute("version","1.4"),
            E("RegistrationInfo",E("Source","Open Net startup:"+name)),
            E("Triggers",E("LogonTrigger",new object[]{E("Enabled","true"),E("UserId",sid),E("Delay","PT8S")})),
            E("Principals",new XElement(n+"Principal",new XAttribute("id","User"),E("UserId",sid),E("LogonType","InteractiveToken"),E("RunLevel","HighestAvailable"))),
            E("Settings",new object[]{E("MultipleInstancesPolicy","IgnoreNew"),E("DisallowStartIfOnBatteries","false"),E("StopIfGoingOnBatteries","false"),E("AllowHardTerminate","false"),E("StartWhenAvailable","false"),E("RunOnlyIfNetworkAvailable","false"),E("AllowStartOnDemand","true"),E("Enabled","true"),E("Hidden","false"),E("ExecutionTimeLimit","PT0S")}),
            new XElement(n+"Actions",new XAttribute("Context","User"),E("Exec",new object[]{E("Command",exe),E("Arguments","--autostart"),E("WorkingDirectory",Path.GetDirectoryName(exe)!)})))).ToString();
    }
    internal static string Hash(string text)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    bool Approved(string xml,string name)=>approvedTaskHash!=null&&Hash(xml)==approvedTaskHash&&StartupTaskContract.Identity(xml,name,Sid);
    string ReadTask(string xml,string name)=>Approved(xml,name)?StartupTaskContract.RawCommand(xml):ValidateXml(xml,name,Sid);
    internal StartupRegistrationObservation Observe(string name)
    {
        string? value=run.Read(name),xml=TaskXml(name);if(xml==null)return new(value!=null,value!=null,true,value,"run","",false,false,null,null);
        bool identity=false,enabled=false,valid=false;string reason="",command="";
        try{identity=StartupTaskContract.Identity(xml,name,Sid);command=StartupTaskContract.RawCommand(xml);enabled=StartupTaskContract.Enabled(xml);_ =ValidateXml(xml,name,Sid);valid=true;}catch(Exception ex){reason=ex.Message;}
        if(value!=null){valid=false;reason=L.T("Duplicate startup registrations require review. No registration was changed.");}
        StartupTaskRuntime? runtime=null;try{runtime=(tasks as IScheduledStartupDiagnostics)?.Inspect(name,Sid);}catch(Exception ex){reason+=(reason.Length>0?"; ":"")+L.Error(ex);}
        return new(true,enabled&&(runtime?.Enabled??true),valid,command,"task",reason,identity,value!=null,Hash(xml),runtime);
    }
    internal bool ApprovedRepair(string name,string? hash){string? xml=TaskXml(name);return xml!=null&&hash!=null&&Hash(xml)==hash&&StartupTaskContract.Identity(xml,name,Sid);}
    string? TaskXml(string name)=>tasks.Read(name,Sid);
    internal string BackupTask(string name)=>TaskXml(name)??throw new IOException("Startup task is missing.");
    void Register(string name,string xml)=>tasks.Write(name,Sid,xml);
    void DeleteTask(string name)=>tasks.Delete(name,Sid);
    public string? Read(string name)
    {
        string? value=run.Read(name),xml=TaskXml(name),task=xml==null?null:ReadTask(xml,name);
        if(value!=null&&task!=null)throw new IOException(L.T("Duplicate startup registrations require review. No registration was changed."));
        return task??value;
    }
    public object Capture(string name){_ =Read(name);return new Snapshot(run.Read(name),TaskXml(name));}
    sealed record Snapshot(string? Run,string? Task);
    public void Rollback(string name,object snapshot)
    {
        var previous=(Snapshot)snapshot;
        // Never undo by deleting an unrecognizable task or a foreign Run command.
        string? currentXml=TaskXml(name),currentRun=run.Read(name);
        if(currentXml!=null)_ =ReadTask(currentXml,name);
        if(currentRun!=null&&currentRun!=previous.Run&&currentRun!=lastCommand)throw new IOException("Startup rollback blocked: registration changed externally.");
        if(currentXml!=null&&currentXml!=previous.Task&&ReadTask(currentXml,name)!=lastCommand)throw new IOException("Startup rollback blocked: task changed externally.");
        if(currentXml!=previous.Task){if(previous.Task!=null)Register(name,previous.Task);else if(currentXml!=null)DeleteTask(name);}
        if(currentRun!=previous.Run){if(previous.Run==null)run.Delete(name);else run.Write(name,previous.Run);}
    }
    public void Write(string name,string command)
    {
        _ =Read(name);
        string? existing=TaskXml(name);if(existing!=null&&!StartupTaskContract.Enabled(existing)&&!Approved(existing,name))throw new IOException(L.T("Startup task is disabled. Review and repair the registration explicitly."));
        lastCommand=command;
        if(scheduled){Register(name,XmlFor(name,command,Sid));if(StartupTaskContract.Validate(TaskXml(name)!,name,Sid,requireEnabled:true)!=command)throw new IOException("Startup task verification failed.");run.Delete(name);}
        else{run.Write(name,command);if(TaskXml(name)!=null)DeleteTask(name);}
    }
    string? lastCommand;
    public void Delete(string name){lastCommand=Read(name);if(TaskXml(name)!=null)DeleteTask(name);run.Delete(name);}
}

internal interface IScheduledStartupStore
{
    string? Read(string name,string sid);
    void Write(string name,string sid,string xml);
    void Delete(string name,string sid);
}
internal sealed class NativeScheduledStartupStore:IScheduledStartupStore,IScheduledStartupDiagnostics
{
    static string Name(string name,string sid)=>"OpenNet.Startup."+sid+"."+name;
    static dynamic Connect(){var type=Type.GetTypeFromProgID("Schedule.Service")??throw new IOException("Task Scheduler is unavailable.");dynamic service=Activator.CreateInstance(type)!;service.Connect();return service;}
    public string? Read(string name,string sid){dynamic service=Connect();dynamic folder=service.GetFolder("\\");try{return (string)folder.GetTask(Name(name,sid)).Xml;}catch(FileNotFoundException){return null;}catch(COMException ex)when(ex.HResult==unchecked((int)0x80070002)){return null;}}
    public StartupTaskRuntime? Inspect(string name,string sid)
    {
        dynamic service=Connect();dynamic folder=service.GetFolder("\\");dynamic task=folder.GetTask(Name(name,sid));DateTime last=(DateTime)task.LastRunTime;
        bool? history=null;try{using var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\Microsoft-Windows-TaskScheduler/Operational");if(key?.GetValue("Enabled") is int enabled)history=enabled!=0;}catch(System.Security.SecurityException){}
        return new(last.Year>=2000?last.ToUniversalTime().ToString("O"):null,(int)task.LastTaskResult,(int)task.State,(bool)task.Enabled,history,Name(name,sid));
    }
    public void Write(string name,string sid,string xml){dynamic service=Connect();dynamic folder=service.GetFolder("\\");string acl="D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;"+sid+")";folder.RegisterTask(Name(name,sid),xml,22,sid,null,3,acl);}
    public void Delete(string name,string sid){dynamic service=Connect();dynamic folder=service.GetFolder("\\");folder.DeleteTask(Name(name,sid),0);}
}

internal sealed record StartupTaskRuntime(string? LastRunUtc,int LastResult,int State,bool Enabled,bool? HistoryEnabled,string TaskName);
internal interface IScheduledStartupDiagnostics {StartupTaskRuntime? Inspect(string name,string sid);}
internal sealed record StartupRegistrationObservation(bool Exists,bool Enabled,bool ConfigurationValid,string? Command,string Mode,string Reason,bool IdentityMatches,bool Duplicate,string? TaskHash,StartupTaskRuntime? Runtime);
