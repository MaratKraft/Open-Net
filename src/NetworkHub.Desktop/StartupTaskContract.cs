using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Xml;
using System.Xml.Linq;
using NetworkHub.App;

namespace NetworkHub.Desktop;
internal sealed class StartupTaskMismatch(string field):IOException(L.T("Startup task mismatch: ")+field);
internal static class StartupTaskContract
{
    static readonly XNamespace N="http://schemas.microsoft.com/windows/2004/02/mit/task";
    static string V(XElement? element,string name)=>element?.Element(N+name)?.Value??"";
    static bool B(XElement? element,string name,bool fallback){string value=V(element,name);if(value=="")return fallback;try{return XmlConvert.ToBoolean(value);}catch(FormatException){throw new StartupTaskMismatch(name);}}
    static string UserSid(string value)=>value.StartsWith("S-1-",StringComparison.Ordinal)?new SecurityIdentifier(value).Value:((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value;
    static bool SameUser(string value,string sid,Func<string,string>? resolve){try{return value.Length>0&&(resolve??UserSid)(value)==sid;}catch(IdentityNotMappedException){return false;}catch(ArgumentException){return false;}}
    static bool PathEquals(string left,string right)=>Path.IsPathFullyQualified(left)&&Path.IsPathFullyQualified(right)&&string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),StringComparison.OrdinalIgnoreCase);
    internal static bool Identity(string xml,string name,string sid,Func<string,string>? resolve=null)
    {
        var task=XDocument.Parse(xml).Root;var principals=task?.Element(N+"Principals")?.Elements().ToArray()??Array.Empty<XElement>();
        return V(task?.Element(N+"RegistrationInfo"),"Source")=="Open Net startup:"+name&&principals.Length==1&&SameUser(V(principals[0],"UserId"),sid,resolve);
    }
    internal static bool Enabled(string xml){var task=XDocument.Parse(xml).Root;return B(task?.Element(N+"Settings"),"Enabled",true)&&B(task?.Element(N+"Triggers")?.Elements().SingleOrDefault(),"Enabled",true);}
    internal static string RawCommand(string xml){var exec=XDocument.Parse(xml).Root?.Element(N+"Actions")?.Elements().FirstOrDefault();return "\""+V(exec,"Command")+"\" "+V(exec,"Arguments");}
    internal static string Validate(string xml,string name,string sid,Func<string,string>? resolve=null,bool requireEnabled=false)
    {
        var task=XDocument.Parse(xml).Root??throw new StartupTaskMismatch("Task");
        var actions=task.Element(N+"Actions")?.Elements().ToArray()??Array.Empty<XElement>();var principals=task.Element(N+"Principals")?.Elements().ToArray()??Array.Empty<XElement>();var triggers=task.Element(N+"Triggers")?.Elements().ToArray()??Array.Empty<XElement>();
        void Require(bool value,string field){if(!value)throw new StartupTaskMismatch(field);}
        Require(Identity(xml,name,sid,resolve),"Source / principal UserId");Require(actions.Length==1&&actions[0].Name==N+"Exec","Actions");Require(principals.Length==1,"Principals");
        Require(V(principals[0],"LogonType")=="InteractiveToken","LogonType");Require(V(principals[0],"RunLevel")=="HighestAvailable","RunLevel");
        Require(triggers.Length==1&&triggers[0].Name==N+"LogonTrigger","Triggers");Require(SameUser(V(triggers[0],"UserId"),sid,resolve),"LogonTrigger.UserId");
        Require(triggers[0].Elements().All(e=>new[]{"UserId","Delay","Enabled"}.Contains(e.Name.LocalName)),"LogonTrigger conditions");
        TimeSpan Duration(string text){try{return XmlConvert.ToTimeSpan(text);}catch(FormatException){throw new StartupTaskMismatch("duration");}}
        Require(Duration(V(triggers[0],"Delay"))==TimeSpan.FromSeconds(8),"LogonTrigger.Delay");
        Require(V(actions[0],"Arguments")=="--autostart","Arguments");string exe=V(actions[0],"Command");Require(Path.IsPathFullyQualified(exe),"Command");Require(PathEquals(V(actions[0],"WorkingDirectory"),Path.GetDirectoryName(exe)!),"WorkingDirectory");
        var settings=task.Element(N+"Settings");
        // Schema defaults are semantic values. Windows omits these nodes on readback.
        Require(!B(settings,"Hidden",false),"Hidden");Require(!B(settings,"RunOnlyIfNetworkAvailable",false),"RunOnlyIfNetworkAvailable");Require(!B(settings,"RunOnlyIfIdle",false),"RunOnlyIfIdle");
        Require(!B(settings,"StartWhenAvailable",false),"StartWhenAvailable");Require(!B(settings,"WakeToRun",false),"WakeToRun");
        Require(!B(settings,"DisallowStartIfOnBatteries",true),"DisallowStartIfOnBatteries");Require(!B(settings,"StopIfGoingOnBatteries",true),"StopIfGoingOnBatteries");Require(!B(settings,"AllowHardTerminate",true),"AllowHardTerminate");
        Require(V(settings,"MultipleInstancesPolicy") is "" or "IgnoreNew","MultipleInstancesPolicy");Require(Duration(V(settings,"ExecutionTimeLimit"))==TimeSpan.Zero,"ExecutionTimeLimit");
        Require(settings?.Element(N+"RestartOnFailure")==null&&settings?.Element(N+"MaintenanceSettings")==null,"automatic retry / maintenance");Require(!B(settings,"Volatile",false),"Volatile");
        if(requireEnabled)Require(Enabled(xml),"Enabled");
        return AutostartService.CommandFor(exe);
    }
}
