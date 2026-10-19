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
internal sealed class WindowsStartupStore(bool scheduled,IStartupStore? runStore=null,IScheduledStartupStore? taskStore=null,string? owner=null):IStartupStore,ITransactionalStartupStore
{
    readonly IStartupStore run=runStore??new RegistryStartupStore();
    readonly IScheduledStartupStore tasks=taskStore??new NativeScheduledStartupStore();
    string Sid=>owner??WindowsIdentity.GetCurrent().User!.Value;
    public string Mode=>scheduled?"task":"run";
    internal static string ValidateXml(string xml,string name,string sid)
    {
        var doc=XDocument.Parse(xml);XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";var task=doc.Root??throw new IOException("Invalid startup task.");
        string Value(XElement? parent,string key)=>parent?.Element(ns+key)?.Value??"";
        var actions=task.Element(ns+"Actions")?.Elements().ToArray()??Array.Empty<XElement>();var principals=task.Element(ns+"Principals")?.Elements().ToArray()??Array.Empty<XElement>();var triggers=task.Element(ns+"Triggers")?.Elements().ToArray()??Array.Empty<XElement>();
        if(Value(task.Element(ns+"RegistrationInfo"),"Source")!="Open Net startup:"+name||actions.Length!=1||actions[0].Name!=ns+"Exec"||principals.Length!=1||Value(principals[0],"UserId")!=sid||Value(principals[0],"LogonType")!="InteractiveToken"||Value(principals[0],"RunLevel")!="HighestAvailable"||triggers.Length!=1||triggers[0].Name!=ns+"LogonTrigger"||Value(triggers[0],"UserId")!=sid||Value(actions[0],"Arguments")!="--autostart")throw new IOException(L.T("The startup task was changed outside Open Net. It was left unchanged."));
        string exe=Value(actions[0],"Command");var settings=task.Element(ns+"Settings");
        if(Value(actions[0],"WorkingDirectory")!=Path.GetDirectoryName(exe)||Value(triggers[0],"Enabled")!="true"||Value(triggers[0],"Delay")!="PT8S"||Value(settings,"Enabled")!="true"||Value(settings,"Hidden")!="false"||Value(settings,"MultipleInstancesPolicy")!="IgnoreNew"||Value(settings,"ExecutionTimeLimit")!="PT0S"||Value(settings,"RunOnlyIfNetworkAvailable")!="false")throw new IOException(L.T("The startup task was changed outside Open Net. It was left unchanged."));
        return AutostartService.CommandFor(exe);
    }
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
    string? TaskXml(string name)=>tasks.Read(name,Sid);
    void Register(string name,string xml)=>tasks.Write(name,Sid,xml);
    void DeleteTask(string name)=>tasks.Delete(name,Sid);
    public string? Read(string name)
    {
        string? value=run.Read(name),xml=TaskXml(name),task=xml==null?null:ValidateXml(xml,name,Sid);
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
        if(currentXml!=null)_ =ValidateXml(currentXml,name,Sid);
        if(currentRun!=null&&currentRun!=previous.Run&&currentRun!=lastCommand)throw new IOException("Startup rollback blocked: registration changed externally.");
        if(currentXml!=null&&currentXml!=previous.Task&&ValidateXml(currentXml,name,Sid)!=lastCommand)throw new IOException("Startup rollback blocked: task changed externally.");
        if(previous.Task!=null)Register(name,previous.Task);else if(TaskXml(name)!=null)DeleteTask(name);
        if(previous.Run==null)run.Delete(name);else run.Write(name,previous.Run);
    }
    public void Write(string name,string command)
    {
        _ =Read(name);
        lastCommand=command;
        if(scheduled){Register(name,XmlFor(name,command,Sid));if(ValidateXml(TaskXml(name)!,name,Sid)!=command)throw new IOException("Startup task verification failed.");run.Delete(name);}
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
internal sealed class NativeScheduledStartupStore:IScheduledStartupStore
{
    static string Name(string name,string sid)=>"OpenNet.Startup."+sid+"."+name;
    static dynamic Connect(){var type=Type.GetTypeFromProgID("Schedule.Service")??throw new IOException("Task Scheduler is unavailable.");dynamic service=Activator.CreateInstance(type)!;service.Connect();return service;}
    public string? Read(string name,string sid){dynamic service=Connect();dynamic folder=service.GetFolder("\\");try{return (string)folder.GetTask(Name(name,sid)).Xml;}catch(FileNotFoundException){return null;}catch(COMException ex)when(ex.HResult==unchecked((int)0x80070002)||ex.HResult==unchecked((int)0x8004130F)){return null;}}
    public void Write(string name,string sid,string xml){dynamic service=Connect();dynamic folder=service.GetFolder("\\");string acl="D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;"+sid+")";folder.RegisterTask(Name(name,sid),xml,22,sid,null,3,acl);}
    public void Delete(string name,string sid){dynamic service=Connect();dynamic folder=service.GetFolder("\\");folder.DeleteTask(Name(name,sid),0);}
}
