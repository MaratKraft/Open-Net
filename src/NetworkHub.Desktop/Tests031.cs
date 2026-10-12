using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class Tests031
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
 {
  await test("031 defaults and general Save preserve module settings and persist confirmation preference",()=>{
   var saved=shell.Settings.Copy();try{
    Check(new UserSettings().ConfirmStartStop&&!new UserSettings().StartWithWindows,"Safe defaults");
    var next=saved.Copy();next.Strategy="general (ALT13).bat";next.LastWorkingStrategy="general.bat";shell.ApplySettings(next);
    var view=shell.OpenSettings();Check(view.FindName("Strategy")==null&&view.FindName("TgPath")==null,"No module paths in general settings");
    ((CheckBox)view.FindName("ConfirmStartStop")).IsChecked=false;((Button)view.FindName("SaveSettings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var actual=files.Load();Check(!actual.ConfirmStartStop&&actual.Strategy==next.Strategy&&actual.TgExe==next.TgExe&&actual.LastWorkingStrategy==next.LastWorkingStrategy,"General save preserves module configuration");
   }finally{shell.ApplySettings(saved);}return Task.CompletedTask;
  });
  await test("031 enabled routine confirmation can cancel; disabled confirmation starts and stops only safe owned stubs",async()=>{
   var saved=shell.Settings.Copy();int prompts=0;try{
    var next=new UserSettings{ConfirmStartStop=true};shell.ApplySettings(next);shell.RoutineConfirmationForTests=_=>{prompts++;return false;};int launches=shell.Runtime.LaunchCount;
    await shell.StartSelected();Check(prompts==1&&shell.Runtime.LaunchCount==launches,"Cancellation prevents start");
    next.ConfirmStartStop=false;shell.ApplySettings(next);await shell.StartSelected();await shell.Refresh();Check(prompts==1&&shell.Runtime.LaunchCount==launches+2,"Off skips routine start prompt");
    await shell.StopSelected(true);await shell.Refresh();Check(prompts==1&&shell.Observations.All(x=>x.State!=ObservedState.Running),"Off skips routine stop prompt and stops owned stubs");
   }finally{shell.RoutineConfirmationForTests=null;await shell.StopSelected(true);shell.ApplySettings(saved);}
  });
  await test("031 per-module gear, action metadata and settings windows remain distinct and single-instance",()=>{
   var saved=shell.Settings.Copy();try{foreach(string language in new[]{"en","ru"}){
    var next=saved.Copy();next.Language=language;shell.ApplySettings(next);
    foreach(var definition in ModuleCatalog.All){var view=shell.OpenModuleSettings(definition.Id);Check(ReferenceEquals(view,shell.OpenModuleSettings(definition.Id)),"One window per module");
     Check(view.FindName(definition.Id=="zapret"?"ZapretPath":"TgPath") is TextBox,"Correct module path");Check(view.FindName("CheckUpdates") is Button&&view.FindName("RollbackModule") is Button&&view.FindName("ImportRelease") is Button,"Existing actions accessible");
     Check((view.FindName("Strategy")!=null)==(definition.Id=="zapret"),"Strategy belongs only to Zapret");Check(((TextBlock)view.FindName("ModuleDiagnostic")).Text.Length>0,"Diagnostics present");
     foreach(double scale in new[]{1.0,1.25,1.5,2.0}){view.LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);view.UpdateLayout();Check(((ScrollViewer)view.Content).ComputedHorizontalScrollBarVisibility!=Visibility.Visible,"No horizontal overflow");}
     view.LayoutTransform=System.Windows.Media.Transform.Identity;SelfTests.Capture(view,Path.Combine(files.Data,$"031-{definition.Id}-{language}.png"));view.Close();
    }
   }}finally{shell.ApplySettings(saved);}return Task.CompletedTask;
  });
  await test("031 ICO has all nine alpha frames, matching DPI handles and retained WPF multi-frame decoder",()=>{
   var frame=(BitmapFrame)shell.Window.Icon;int[] sizes={16,20,24,32,40,48,64,128,256};Check(frame.Decoder!.Frames.Select(x=>x.PixelWidth).OrderBy(x=>x).SequenceEqual(sizes),"All ICO sizes");
   foreach(int size in sizes){using var icon=IconFactory.Create(size);Check(icon.Width==size&&icon.Height==size,"Native matching frame "+size);}
   FocusCues.SetMode(false);Check(shell.Find<Button>("Settings").FocusVisualStyle==null&&shell.Window.FocusVisualStyle==null,"Mouse and containers have no outline");FocusCues.SetMode(true);Check(shell.Find<Button>("Settings").FocusVisualStyle!=null,"Keyboard focus retained");shell.Restore();Check(shell.Find<Button>("Settings").FocusVisualStyle==null,"Tray restoration clears mouse cue");return Task.CompletedTask;
  });
  await test("031 actual isolated HKCU registration, ownership, explicit repair and transaction rollback; production Run untouched",()=>{
   string key=@"Software\Open Net\Tests\"+Guid.NewGuid().ToString("N");var store=new RegistryStartupStore(key);var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"startup-test"));
   var settings=new UserSettings{StartWithWindows=true};string first=Path.Combine(files.Root,"Moved Cyrillic Пример","Open Net.exe"),second=Path.Combine(files.Root,"Moved again","Open Net.exe");var service=new AutostartService(isolated,store,first);
   try{
    Check(AutostartService.EntryName(false,"")=="OpenNet"&&AutostartService.EntryName(true,Guid.NewGuid().ToString("N")).StartsWith("OpenNet.Portable."),"Normal and portable entry naming");Check(!new AutostartService(isolated,store,first).Inspect(new UserSettings()).Registered,"Off is read-only");store.Write("Foreign","untouched");service.Configure(settings,()=>isolated.Save(settings));var receipt=System.Text.Json.JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(service.ReceiptPath))!;
    Check(store.Read(receipt.Name)=="\""+first+"\" --autostart"&&store.Read("Foreign")=="untouched","One quoted owned registration");
    var moved=new AutostartService(isolated,store,second);Check(moved.Inspect(settings).NeedsRepair&&store.Read(receipt.Name)==receipt.Command,"Move detected without update");moved.Configure(settings,()=>isolated.Save(settings));Check(!moved.Inspect(settings).NeedsRepair,"Explicit repair succeeds");
    var off=settings.Copy();off.StartWithWindows=false;bool rejected=false;try{moved.Configure(off,()=>throw new IOException("simulated save failure"));}catch(IOException){rejected=true;}Check(rejected&&store.Read(receipt.Name)==moved.Command&&File.Exists(moved.ReceiptPath),"Failed save rolls registry and receipt back");
    store.Write(receipt.Name,"foreign replaced value");rejected=false;try{moved.Configure(off,()=>{});}catch(InvalidOperationException){rejected=true;}Check(rejected&&store.Read(receipt.Name)=="foreign replaced value","Never remove another registration");store.Write(receipt.Name,moved.Command);
    moved.Configure(off,()=>isolated.Save(off));Check(store.Read(receipt.Name)==null&&store.Read("Foreign")=="untouched","Disable removes only own entry");
    bool longPath=false;try{AutostartService.CommandFor(Path.Combine(files.Root,new string('x',270),"app.exe"));}catch(IOException){longPath=true;}Check(longPath,"Long path explicitly rejected");
   }finally{Registry.CurrentUser.DeleteSubKeyTree(key,false);}return Task.CompletedTask;
  });
 }
}

