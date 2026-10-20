using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetworkHub.App;

namespace NetworkHub.Desktop;
internal static class TestsStartupReadback
{
    const string Sid="S-1-5-21-1-2-3-1001";
    static readonly XNamespace N="http://schemas.microsoft.com/windows/2004/02/mit/task";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Tasks:IScheduledStartupStore
    {
        internal string? Xml;internal int Writes;internal bool Denied;
        public string? Read(string name,string sid)=>Xml;
        public void Write(string name,string sid,string xml){if(Denied)throw new System.ComponentModel.Win32Exception(1223);Xml=Normalize(xml);Writes++;}
        public void Delete(string name,string sid){Xml=null;Writes++;}
    }
    static string Normalize(string xml)
    {
        var doc=XDocument.Parse(xml);foreach(var e in doc.Descendants().Where(e=>new[]{"Enabled","AllowStartOnDemand"}.Contains(e.Name.LocalName)&&e.Value=="true"||new[]{"Hidden","RunOnlyIfNetworkAvailable","StartWhenAvailable"}.Contains(e.Name.LocalName)&&e.Value=="false").ToArray())e.Remove();
        doc.Root!.Element(N+"Settings")!.Add(new XElement(N+"UseUnifiedSchedulingEngine",true));return doc.ToString();
    }
    internal static void Headless(Action<string,Action> test,AppFiles files)
    {
        test("033 readback: Windows account aliases defaults order and duration normalization",()=>{
            string command=AutostartService.CommandFor(Environment.ProcessPath!),xml=Normalize(WindowsStartupStore.XmlFor("OpenNet",command,Sid));var doc=XDocument.Parse(xml);doc.Descendants(N+"LogonTrigger").Single().Element(N+"UserId")!.Value="FIXTURE\\User";doc.Descendants(N+"Delay").Single().Value="PT0H0M8S";
            Check(StartupTaskContract.Validate(doc.ToString(),"OpenNet",Sid,value=>value=="FIXTURE\\User"?Sid:value)==command&&StartupTaskContract.Enabled(xml),"Valid Windows readback rejected");
        });
        test("033 readback: real action owner conditions and trigger tampering still fail closed",()=>{
            string xml=Normalize(WindowsStartupStore.XmlFor("OpenNet",AutostartService.CommandFor(Environment.ProcessPath!),Sid));
            foreach(var bad in new[]{xml.Replace("--autostart","--other"),xml.Replace(Sid,"S-1-5-21-1-2-3-1002"),xml.Replace("HighestAvailable","LeastPrivilege"),xml.Replace("PT8S","PT1S"),xml.Replace("<Settings>","<Settings><RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>"),xml.Replace("<LogonTrigger>","<LogonTrigger><Repetition><Interval>PT1M</Interval></Repetition>")}){bool rejected=false;try{StartupTaskContract.Validate(bad,"OpenNet",Sid);}catch(IOException ex){rejected=ex.Message.Length>0;}Check(rejected,"A meaningful change was accepted");}
        });
        test("033 state: saved intent disabled task ownership and actual runs stay separate",()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"readback-state"));var run=new IsolatedStartupStore();var tasks=new Tasks();var settings=new UserSettings{StartWithWindows=true,AutoStartTools=true};var service=new AutostartService(isolated,new WindowsStartupStore(true,run,tasks,Sid),Environment.ProcessPath!);service.Configure(settings,()=>isolated.Save(settings));
            var ready=service.Inspect(settings);Check(ready.Requested&&ready.SettingsSaved&&ready.Registered&&ready.Exists==true&&ready.Enabled==true&&ready.Owned==true&&ready.Ready&&ready.LastRunUtc==null,"Registration was mistaken for an actual logon");
            var doc=XDocument.Parse(tasks.Xml!);doc.Root!.Element(N+"Settings")!.Add(new XElement(N+"Enabled",false));tasks.Xml=doc.ToString();var disabled=service.Inspect(settings);Check(disabled.Registered&&disabled.Owned==true&&disabled.Enabled==false&&!disabled.Ready&&isolated.Load().StartWithWindows,"Disabled task reset intent or lost identity");
            int writes=tasks.Writes;bool refused=false;try{service.Configure(settings,()=>isolated.Save(settings));}catch(IOException){refused=true;}Check(refused&&tasks.Writes==writes,"Disabled task silently re-enabled");
        });
        test("033 recovery: orphan task requires exact approved hash and preserves user intent",()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"readback-orphan"));var settings=new UserSettings{StartWithWindows=true,AutoStartTools=true};isolated.Save(settings);var run=new IsolatedStartupStore();var tasks=new Tasks();string name=AutostartService.EntryName(isolated.Portable,settings.StartupInstallationId=Guid.NewGuid().ToString("N"));isolated.Save(settings);tasks.Xml=Normalize(WindowsStartupStore.XmlFor(name,AutostartService.CommandFor(Environment.ProcessPath!),Sid));
            var service=new AutostartService(isolated,new WindowsStartupStore(true,run,tasks,Sid),Environment.ProcessPath!);var orphan=service.Inspect(settings);Check(orphan.Exists==true&&orphan.Owned==false&&orphan.RepairAllowed&&!orphan.Ready,"Orphan trusted automatically");
            bool refused=false;try{service.Configure(settings,()=>{});}catch(InvalidOperationException){refused=true;}Check(refused&&tasks.Writes==0&&isolated.Load().StartWithWindows,"Orphan changed without approval");
            string hash=WindowsStartupStore.Hash(tasks.Xml);var approved=new AutostartService(isolated,new WindowsStartupStore(true,run,tasks,Sid,hash),Environment.ProcessPath!);approved.Configure(settings,()=>isolated.Save(settings),hash);Check(approved.Inspect(settings).Ready&&tasks.Writes==1,"Approved repair failed");
            tasks.Xml=tasks.Xml!.Replace("PT8S","PT9S");Check(!new WindowsStartupStore(true,run,tasks,Sid,hash).ApprovedRepair(name,hash),"Changed task reused old approval");
        });
        string live=Path.Combine(files.Data,"startup-live-readback.xml");if(File.Exists(live))test("033 private read-only actual Windows task fixture passes semantic inspection",()=>{
            string xml=File.ReadAllText(live);string command=StartupTaskContract.Validate(xml,"OpenNet",System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);Check(StartupTaskContract.Enabled(xml)&&command.EndsWith(" --autostart"),"Actual readback contract");File.WriteAllText(Path.Combine(files.Data,"startup-live-readback-result.json"),JsonSerializer.Serialize(new{Accepted=true,ReadOnly=true,TaskRegistered=false,NetworkToolsLaunched=false}));
        });
    }
    internal static async Task Ui(AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("033 UI: registration failure preserves saved intent and same-value retry succeeds",async()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"startup-ui-failure"));var tasks=new Tasks{Denied=true};using var instance=new SingleInstance(isolated.Data,isolated.Log);var shell=new Shell(isolated,true,instance,null,null,new WindowsStartupStore(true,new IsolatedStartupStore(),tasks,Sid));
            try{
                shell.RoutineConfirmationForTests=_=>true;var wanted=shell.Settings.Copy();wanted.StartWithWindows=wanted.AutoStartTools=true;bool denied=false;try{await shell.ApplyGeneralSettingsAsync(wanted);}catch(System.ComponentModel.Win32Exception){denied=true;}
                Check(denied&&isolated.Load().StartWithWindows&&isolated.Load().AutoStartTools&&shell.Settings.StartWithWindows,"Registration failure reset user intent");Check(!shell.Autostart.Inspect(shell.Settings).Ready&&tasks.Xml==null,"Failed registration reported ready");
                tasks.Denied=false;await shell.ApplyGeneralSettingsAsync(shell.Settings.Copy());Check(shell.Autostart.Inspect(shell.Settings).Ready,"Retry of saved intent did not register");
            }finally{shell.Dispose();shell.Window.Close();}
        });
    }
}
