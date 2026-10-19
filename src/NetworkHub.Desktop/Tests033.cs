using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;

namespace NetworkHub.Desktop;
internal static class Tests033
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Tasks:IScheduledStartupStore
    {
        internal readonly Dictionary<string,string> Values=new();internal bool Denied;
        public string? Read(string name,string sid)=>Values.GetValueOrDefault(name);
        public void Write(string name,string sid,string xml){if(Denied)throw new System.ComponentModel.Win32Exception(1223);Values[name]=xml;}
        public void Delete(string name,string sid)=>Values.Remove(name);
    }
    internal static void Headless(Action<string,Action> test,AppFiles files)
    {
        test("033 migration: existing startup never opts into automatic tools; selection round-trips",()=>{
            var legacy=JsonSerializer.Deserialize<UserSettings>("{\"StartWithWindows\":true,\"ZapretSelected\":true,\"TgSelected\":false,\"Strategy\":\"general.bat\"}")!;
            Check(!legacy.AutoStartTools&&legacy.StartupInTray,"Legacy opt-in/default changed");var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"033-settings"));isolated.Save(legacy);var actual=isolated.Load();Check(actual.ZapretSelected&&!actual.TgSelected&&actual.Strategy=="general.bat"&&!actual.AutoStartTools,"Choice lost");
        });
        test("033 task XML: interactive user only, highest, one action, no password, bounded logon delay",()=>{
            string exe=Path.Combine(files.Root,"Кириллица с пробелами","Open Net.exe"),command=AutostartService.CommandFor(exe),sid="S-1-5-21-1-2-3-1001";
            var xml=WindowsStartupStore.XmlFor("OpenNet",command,sid);Check(WindowsStartupStore.ValidateXml(xml,"OpenNet",sid)==command&&xml.Contains("IgnoreNew")&&xml.Contains("PT8S")&&!xml.Contains("Password")&&!xml.Contains("RestartOnFailure")&&xml.Contains("<RunOnlyIfNetworkAvailable>false"),"Task contract");
            foreach(var bad in new[]{xml.Replace("--autostart","--other"),xml.Replace("HighestAvailable","LeastPrivilege"),xml.Replace("LogonTrigger","BootTrigger"),xml.Replace("Open Net startup:OpenNet","Foreign"),xml.Replace("PT8S","PT2S"),xml.Replace("IgnoreNew","Parallel")}){bool blocked=false;try{WindowsStartupStore.ValidateXml(bad,"OpenNet",sid);}catch(IOException){blocked=true;}Check(blocked,"Foreign task accepted");}
            // This file is for optional TASK_VALIDATE_ONLY; not a registered task.
            File.WriteAllText(Path.Combine(files.Data,"startup-task-validation.xml"),WindowsStartupStore.XmlFor("OpenNet.Validation",AutostartService.CommandFor(Environment.ProcessPath!),System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value));
        });
        test("033 Run/task migration, one registration, rollback and disable use only fake stores",()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"033-registration"));var run=new IsolatedStartupStore();var tasks=new Tasks();string sid="S-1-5-21-1-2-3-1001",exe=Path.Combine(files.Root,"Open Net.exe");var settings=new UserSettings{StartWithWindows=true};
            var plain=new AutostartService(isolated,new WindowsStartupStore(false,run,tasks,sid),exe);plain.Configure(settings,()=>isolated.Save(settings));string name=JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(plain.ReceiptPath))!.Name;
            var scheduled=new AutostartService(isolated,new WindowsStartupStore(true,run,tasks,sid),exe);settings.AutoStartTools=true;bool migrationFailed=false;try{scheduled.Configure(settings,()=>throw new IOException("persist failed after migration"));}catch(IOException){migrationFailed=true;}Check(migrationFailed&&tasks.Values.Count==0&&run.Read(name)==plain.Command&&JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(plain.ReceiptPath))!.Mode=="run","Migration rollback lost original registration");
            scheduled.Configure(settings,()=>isolated.Save(settings));Check(run.Read(name)==null&&tasks.Values.Count==1&&scheduled.Inspect(settings).Registered&&!scheduled.Inspect(settings).NeedsRepair,"Migration duplicated or not inspectable");
            scheduled.Configure(settings,()=>isolated.Save(settings));Check(tasks.Values.Count==1,"Duplicate task");
            var off=settings.Copy();off.StartWithWindows=false;bool failed=false;try{scheduled.Configure(off,()=>throw new IOException("persist failed"));}catch(IOException){failed=true;}Check(failed&&tasks.Values.Count==1&&scheduled.Inspect(settings).Registered,"Rollback lost task");
            settings.AutoStartTools=false;plain.Configure(settings,()=>isolated.Save(settings));Check(tasks.Values.Count==0&&run.Read(name)==plain.Command,"Task to Run migration failed");
            tasks.Denied=true;failed=false;try{scheduled.Configure(withAuto(),()=>{});}catch(System.ComponentModel.Win32Exception){failed=true;}Check(failed&&run.Read(name)==plain.Command&&tasks.Values.Count==0,"UAC refusal changed registration");
            UserSettings withAuto(){var next=settings.Copy();next.AutoStartTools=true;return next;}
            run.Write("Foreign","untouched");plain.Configure(off,()=>{});Check(run.Read(name)==null&&run.Read("Foreign")=="untouched"&&tasks.Values.Count==0,"Foreign entry changed on disable");
        });
        test("033 task ownership, duplicate detection and portable move refuse unsafe repair",()=>{
            var files2=new AppFiles(files.Root,Path.Combine(files.Data,"033-ownership"));var run=new IsolatedStartupStore();var tasks=new Tasks();var store=new WindowsStartupStore(true,run,tasks,"S-1-5-21-1-2-3-1001");var service=new AutostartService(files2,store,Path.Combine(files.Root,"first","Open Net.exe"));var settings=new UserSettings{StartWithWindows=true,AutoStartTools=true};service.Configure(settings,()=>{});string name=JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(service.ReceiptPath))!.Name;
            var moved=new AutostartService(files2,store,Path.Combine(files.Root,"second","Open Net.exe"));Check(moved.Inspect(settings).NeedsRepair,"Portable move hidden");
            string oldExe=Path.Combine(files.Root,"first","Open Net.exe");Directory.CreateDirectory(Path.GetDirectoryName(oldExe)!);File.WriteAllText(oldExe,"non-executable ownership fixture");bool copyBlocked=false;try{moved.Configure(settings,()=>{});}catch(InvalidOperationException){copyBlocked=true;}Check(copyBlocked&&tasks.Values.Count==1,"Copied Portable took over an existing installation");
            tasks.Values[name]=tasks.Values[name].Replace("--autostart","--foreign");bool blocked=false;try{service.Configure(settings,()=>{});}catch(IOException){blocked=true;}Check(blocked&&tasks.Values[name].Contains("--foreign"),"Foreign task overwritten");
        });
        test("033 startup selection, errors, external instances and one-shot policy are independent",()=>{
            foreach(var selected in new[]{(false,false),(true,false),(false,true),(true,true)}){
                var settings=new UserSettings{StartWithWindows=true,AutoStartTools=true,ZapretSelected=selected.Item1,TgSelected=selected.Item2};var controller=new WindowsStartupLaunch();var starts=new List<string>();
                ToolObservation Observe(string id)=>new(id,ObservedState.Stopped,"ready");Task<string> Start(string id){starts.Add(id);return Task.FromResult("started");}
                controller.Run(settings,Observe,Start,(_,_,_)=>{}).GetAwaiter().GetResult();controller.Run(settings,Observe,Start,(_,_,_)=>{}).GetAwaiter().GetResult();Check(starts.Count==(selected.Item1?1:0)+(selected.Item2?1:0),"Selection/one-shot failed");
            }
            foreach(var state in new[]{ObservedState.External,ObservedState.Running,ObservedState.Unknown,ObservedState.NotInstalled,ObservedState.Error,ObservedState.CleanupRequired}){int calls=0;new WindowsStartupLaunch().Run(new UserSettings{StartWithWindows=true,AutoStartTools=true},id=>new(id,state,"skip"),id=>{calls++;return Task.FromResult("");},(_,_,_)=>{}).GetAwaiter().GetResult();Check(calls==0,"Unsafe state started");}
            foreach(var settings in new[]{new UserSettings(),new UserSettings{StartWithWindows=true},new UserSettings{AutoStartTools=true}}){int calls=0;new WindowsStartupLaunch().Run(settings,id=>new(id,ObservedState.Stopped,"ready"),id=>{calls++;return Task.FromResult("");},(_,_,_)=>{}).GetAwaiter().GetResult();Check(calls==0,"Unapproved launch");}
            int other=0,failures=0;new WindowsStartupLaunch().Run(new UserSettings{StartWithWindows=true,AutoStartTools=true},id=>new(id,ObservedState.Stopped,"ready"),id=>id=="tg"?throw new IOException("simulated failure"):Task.FromResult((++other).ToString()),(_,_,failed)=>{if(failed)failures++;}).GetAwaiter().GetResult();Check(other==1&&failures==1,"One failure blocked another tool");
        });
    }
    internal static async Task Ui(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("033 settings explicit approval, tray default, selection and themes",async()=>{
            var saved=shell.Settings.Copy();try{
                var next=saved.Copy();next.Language="ru";next.StartWithWindows=true;next.AutoStartTools=true;next.ZapretSelected=next.TgSelected=true;shell.RoutineConfirmationForTests=_=>false;await shell.ApplyGeneralSettingsAsync(next);Check(!shell.Settings.AutoStartTools,"Cancelled approval persisted");
                shell.RoutineConfirmationForTests=_=>true;await shell.ApplyGeneralSettingsAsync(next);Check(files.Load().AutoStartTools&&files.Load().StartupInTray,"Opt-in lost");
                var view=shell.OpenSettings();Check(view.FindName("AutoStartTools") is CheckBox&&view.FindName("StartupInTray") is CheckBox,"Controls missing");foreach(string theme in new[]{"light","dark"}){ThemeManager.SetMode(theme);view.UpdateLayout();SelfTests.Capture(view,Path.Combine(files.Data,"033-settings-"+theme+".png"));}view.Close();
                await shell.RunWindowsStartup();await shell.Refresh();Check(shell.Observations.All(o=>o.State==ObservedState.Running),"Safe stubs not started");int count=shell.Runtime.LaunchCount;await shell.RunWindowsStartup();Check(shell.Runtime.LaunchCount==count,"Repeated startup duplicated stubs");await shell.StopSelected(true);
            }finally{shell.RoutineConfirmationForTests=null;await shell.StopSelected(true);var off=shell.Settings.Copy();off.StartWithWindows=false;off.AutoStartTools=false;await shell.ApplyGeneralSettingsAsync(off);shell.ApplySettings(saved);}
        });
    }
}
