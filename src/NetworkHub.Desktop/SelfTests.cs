using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetworkHub.App;
using NetworkHub.Core;
using NetworkHub.Adapters;
namespace NetworkHub.Desktop;
public static class SelfTests
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
    public static bool NetworkRequested {get;set;}
    static void Require(bool value,string detail="Assertion failed"){if(!value)throw new Exception(detail);}
    static async Task Wait(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>15000)throw new TimeoutException();await Task.Delay(60);}}
    sealed class FakeHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage,HttpResponseMessage> reply;public FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> reply)=>this.reply=reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)=>Task.FromResult(reply(request));
    }
    public static async Task Run(Shell shell,AppFiles files,SingleInstance instance)
    {
        ThemeManager.SystemDarkForTests=()=>true;ThemeManager.SetMode(shell.Settings.Theme);
        int failures=0;var results=new List<object>();
        async Task Test(string name,Func<Task> action){try{await action();results.Add(new{Name=name,Passed=true});files.Log("PASS "+name);}catch(Exception ex){failures++;results.Add(new{Name=name,Passed=false,Error=ex.ToString()});files.Log("FAIL "+name+": "+ex);}}
        Task Sync(Action action){action();return Task.CompletedTask;}
        await shell.Refresh();
        await Test("Portable WPF window, tray, Open Net name, missing-module install buttons",()=>Sync(()=>{
            Require(shell.Window.IsVisible&&shell.Tray.Visible&&shell.Window.Title=="Open Net");
            Require(System.Windows.Automation.AutomationProperties.GetName(shell.Find<Button>("ZapretInstall"))==L.T("Install")&&System.Windows.Automation.AutomationProperties.GetName(shell.Find<Button>("TgInstall"))==L.T("Install"));Capture(shell.Window,Path.Combine(files.Data,"main.png"));
        }));
        await Test("Settings persist and round-trip managed relative paths",()=>Sync(()=>{
            var settings=new UserSettings{ZapretDirectory=Path.Combine(files.Modules,"zapret-discord-youtube-example"),TgExe=Path.Combine(files.Modules,"tg-ws-proxy-example","TgWsProxy_windows.exe"),ZapretSelected=false};files.Save(settings);
            string raw=File.ReadAllText(files.SettingsPath);Require(raw.Contains("@modules/")&&!raw.Contains(files.Root.Replace("\\","\\\\")));
            Require(files.Load().ZapretDirectory==settings.ZapretDirectory&&!files.Load().ZapretSelected);files.Save(new UserSettings());
        }));
        await Test("Corrupt configuration raises explicit error, original is preserved",()=>Sync(()=>{
            File.WriteAllText(files.SettingsPath,"{invalid");bool rejected=false;try{files.Load();}catch(JsonException){rejected=true;}Require(rejected&&File.ReadAllText(files.SettingsPath)=="{invalid");files.Save(new UserSettings());
        }));
        await Test("Current settings import backs up input and current settings",()=>Sync(()=>{
            string path=Path.Combine(files.Data,"legacy.json");File.WriteAllText(path,JsonSerializer.Serialize(new UserSettings{ZapretSelected=false}));PortableStartup.ImportSettings(files,path);
            Require(!files.Load().ZapretSelected&&Directory.GetFiles(Path.Combine(files.Data,"Backups","Imports"),"*.json",SearchOption.AllDirectories).Length>=2);files.Save(new UserSettings());
        }));
        await Test("Checkboxes and settings editor save actual events",()=>Sync(()=>{
            shell.Workspace.OpenManager();var manager=shell.Window.OwnedWindows.Cast<Window>().Single(w=>w.FindName("Groupzapret")!=null);((CheckBox)manager.FindName("Groupzapret")).IsChecked=false;Require(!files.Load().ZapretSelected);((CheckBox)manager.FindName("Groupzapret")).IsChecked=true;manager.Close();
            var window=shell.OpenSettings();((CheckBox)window.FindName("CloseToTray")).IsChecked=true;((Button)window.FindName("SaveSettings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(files.Load().CloseToTray);
        }));
        await Test("Real backend refuses unconfirmed network launch",async()=>{bool rejected=false;try{await new ToolRuntime(files,false).Start("tg",new UserSettings());}catch(InvalidOperationException){rejected=true;}Require(rejected);});
        await Test("Actual self-contained safe stubs start without external dotnet",async()=>{shell.Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>!shell.Busy&&shell.Observations.Count(x=>x.State==ObservedState.Running)==2);Require(shell.Runtime.LaunchCount==2);});
        await Test("Repeated start cannot duplicate running modules",async()=>{await shell.StartSelected();Require(shell.Runtime.LaunchCount==2);});
        await Test("Close hides taskbar window and preserves owned stubs",()=>Sync(()=>{shell.Window.Close();Require(!shell.Window.IsVisible&&!shell.Window.ShowInTaskbar&&shell.Tray.Visible);Require(shell.Observations.All(x=>ProcessIdentity.Read(x.Pid!.Value).Exists));}));
        await Test("Tray menu and double-click restore window",()=>Sync(()=>{shell.TrayOpen.PerformClick();Require(shell.Window.IsVisible);shell.Window.Close();typeof(System.Windows.Forms.NotifyIcon).GetMethod("OnDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(shell.Tray,new object[]{EventArgs.Empty});Require(shell.Window.IsVisible&&shell.Window.ShowInTaskbar);}));
        await Test("Second published EXE activates first and exits",async()=>{
            shell.Window.Close();var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};foreach(string arg in new[]{"--test-mode","--data-dir",files.Data})info.ArgumentList.Add(arg);
            using var second=Process.Start(info)!;var finished=second.WaitForExitAsync();Require(await Task.WhenAny(finished,Task.Delay(10000))==finished);Require(second.ExitCode==0);await Wait(()=>shell.Window.IsVisible);Require(instance.Activations>=1);
        });
        await Test("TaskbarCreated recovery message re-adds real icon; Explorer not restarted",()=>Sync(()=>{
            var type=typeof(System.Windows.Forms.NotifyIcon);var native=(System.Windows.Forms.NativeWindow)type.GetField("_window",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(shell.Tray)!;
            shell.Tray.Visible=false;
            // Reproduce the stored visible/stale-added state after Explorer loses its icon list.
            type.GetField("_visible",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(shell.Tray,true);
            type.GetField("_added",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(shell.Tray,true);
            SendMessage(native.Handle,RegisterWindowMessage("TaskbarCreated"),IntPtr.Zero,IntPtr.Zero);
            Require(shell.Tray.Visible&&(bool)type.GetField("_added",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(shell.Tray)!);files.Log("Delivered TaskbarCreated to own NotifyIcon window; Explorer process not touched.");
        }));
        await Test("Tray Stop stops only own stubs and factual states change",async()=>{shell.TrayStop.PerformClick();await Wait(()=>!shell.Busy&&shell.Observations.All(x=>x.State==ObservedState.Stopped));});
        await Test("Production stop retains native identities and ends own child tree",async()=>{
            using var supervisor=new StubSupervisor(Environment.ProcessPath!,Environment.ProcessPath!,Metadata.Hash(Environment.ProcessPath!),Path.Combine(files.Data,"tree-stop"),true);
            var owned=supervisor.Start(hang:true,child:true);await Task.Delay(500);var probe=ProcessIdentity.Read(owned.Pid);var wrong=new OwnedReceipt(Guid.NewGuid().ToString("N"),owned.Pid,probe.StartFileTime+1,probe.Executable!,"tg");
            bool rejected=false;try{OwnedStop.Stop(wrong,true);}catch(InvalidOperationException){rejected=true;}Require(rejected&&ProcessIdentity.Read(owned.Pid).Exists);
            int[] tree=supervisor.Tree(owned.Token);Require(tree.Length>=2);await Task.Run(()=>OwnedStop.Stop(wrong with{StartFileTime=probe.StartFileTime},true));Require(tree.All(pid=>!ProcessIdentity.Read(pid).Exists));
        });
        await Test("Driver cleanup cannot match another installation path",()=>Sync(()=>{string own=Path.Combine(files.Root,"test-driver","WinDivert64.sys");Require(DriverControl.MatchesPath(@"\??\"+own,own));Require(!DriverControl.MatchesPath(Path.Combine(files.Root,"other","WinDivert64.sys"),own));}));
        await Test("Fixed elevation rejects arbitrary execution arguments without UAC",async()=>{var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--elevated");using var process=Process.Start(info)!;await process.WaitForExitAsync();Require(process.ExitCode==2);});
        await Test("ZIP Slip and absolute paths rejected on real ZIP entries",()=>Sync(()=>{
            foreach(string name in new[]{"../escape.txt","C:/escape.txt","/escape.txt","safe/../../escape.txt","safe\\..\\escape.txt"}){
                string zipPath=Path.Combine(files.Data,Guid.NewGuid()+".zip");using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write("bad");}
                bool rejected=false;try{ModuleInstaller.ExtractZip(zipPath,Path.Combine(files.Data,"zip-stage"));}catch(InvalidDataException){rejected=true;}Require(rejected,name);
            }
        }));
        await Test("ZIP duplicate files, symlinks and corrupt archive rejected",()=>Sync(()=>{
            string zipPath=Path.Combine(files.Data,"duplicate.zip");using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){zip.CreateEntry("a.txt");zip.CreateEntry("A.txt");}
            bool rejected=false;try{ModuleInstaller.ExtractZip(zipPath,Path.Combine(files.Data,"duplicate-stage"));}catch(InvalidDataException){rejected=true;}Require(rejected);
            string linkZip=Path.Combine(files.Data,"link.zip");using(var zip=ZipFile.Open(linkZip,ZipArchiveMode.Create)){var link=zip.CreateEntry("link");link.ExternalAttributes=unchecked((int)0xA0000000);}rejected=false;try{ModuleInstaller.ExtractZip(linkZip,Path.Combine(files.Data,"link-stage"));}catch(InvalidDataException){rejected=true;}Require(rejected);
            string corrupt=Path.Combine(files.Data,"corrupt.zip");File.WriteAllText(corrupt,"not a ZIP");rejected=false;try{ModuleInstaller.ExtractZip(corrupt,Path.Combine(files.Data,"corrupt-stage"));}catch(InvalidDataException){rejected=true;}Require(rejected);
        }));
        await Test("GitHub HTTP 403/404/429 surface actionable errors",async()=>{
            foreach(int code in new[]{403,404,429}){using var installer=new ModuleInstaller(new AppFiles(files.Root,Path.Combine(files.Data,"http-fixture-"+Guid.NewGuid().ToString("N"))),new FakeHandler(_=>new HttpResponseMessage((HttpStatusCode)code)));bool rejected=false;try{await installer.Discover("tg");}catch(HttpRequestException ex){rejected=ex.StatusCode==(HttpStatusCode)code;}Require(rejected);}
        });
        await Test("Offline startup does not require GitHub; network failure is explicit",async()=>{
            using var installer=new ModuleInstaller(new AppFiles(files.Root,Path.Combine(files.Data,"http-fixture-"+Guid.NewGuid().ToString("N"))),new FakeHandler(_=>throw new HttpRequestException("DNS offline")));bool rejected=false;try{await installer.Discover("zapret");}catch(HttpRequestException){rejected=true;}Require(rejected&&shell.Window.IsVisible);
        });
        await Test("Latest published version differs from reviewed compatible version",async()=>{
            string json="[{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v99\",\"published_at\":\"2026-10-10T00:00:00Z\",\"assets\":[]},{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v1.11.1\",\"published_at\":\"2026-01-01T00:00:00Z\",\"html_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/tag/v1.11.1\",\"assets\":[{\"name\":\"TgWsProxy_windows.exe\",\"size\":1,\"browser_download_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/download/v1.11.1/TgWsProxy_windows.exe\"}]}]";
            using var installer=new ModuleInstaller(new AppFiles(files.Root,Path.Combine(files.Data,"http-fixture-"+Guid.NewGuid().ToString("N"))),new FakeHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)}));var asset=await installer.Discover("tg");Require(asset.Version=="v1.11.1"&&asset.Latest=="v99");
        });
        await Test("Interrupted download and wrong digest never publish installed manifest",async()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"failed-install"));
            foreach(string digest in new[]{"sha256:"+new string('0',64),""}){
                using var installer=new ModuleInstaller(isolated,new FakeHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{1,2,3})}));
                var asset=new ReleaseAsset("tg","v1.11.1","TgWsProxy_windows.exe","https://github.com/Flowseal/tg-ws-proxy/releases/download/v1.11.1/TgWsProxy_windows.exe",digest==""?4:3,digest==""?null:digest,"official","v1.11.1");bool rejected=false;try{await installer.Install(asset);}catch(InvalidDataException){rejected=true;}Require(rejected&&installer.Current("tg")==null);
            }
        });
        await Test("Broken active manifest reports error and permits explicit reinstall",()=>Sync(()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"broken-manifest"));using var catalog=new ModuleInstaller(isolated);
            string path=catalog.Manifest("tg");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,JsonSerializer.Serialize(new InstalledModule("tg","v1.11.1","Modules/tg/Versions/missing/TgWsProxy_windows.exe","unused","official","unused")));
            bool rejected=false;try{catalog.Current("tg");}catch(InvalidDataException){rejected=true;}Require(rejected&&catalog.RepairableCurrent("tg")==null&&File.Exists(path));
        }));
        await Test("Restart removes only abandoned installer staging, preserves active data",()=>Sync(()=>{
            string token=Guid.NewGuid().ToString("N"),folder=Path.Combine(files.Data,"Downloads",token);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"partial"),"partial");File.WriteAllText(Path.Combine(folder,"open-net-staging.json"),JsonSerializer.Serialize(new{Application="Open Net",Token=token}));string unknown=Path.Combine(files.Data,"Downloads",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(unknown);File.WriteAllText(Path.Combine(unknown,"user.txt"),"keep");shell.Installer.RecoverInterruptedDownloads();Require(!Directory.Exists(folder)&&File.Exists(Path.Combine(unknown,"user.txt"))&&File.Exists(files.SettingsPath));
        }));
        await Test("Logging access error is recorded without crashing UI",()=>Sync(()=>{
            string path=Path.Combine(files.Data,"Logs","hub.log");File.SetAttributes(path,File.GetAttributes(path)|FileAttributes.ReadOnly);
            try{files.Log("Read-only test");Require(files.LogFailure!=null);}finally{File.SetAttributes(path,FileAttributes.Normal);}files.Log("Log writable again");Require(files.LogFailure==null);
        }));
        await Test("Unsupported executable and strategy blocked, no implicit substitution",()=>Sync(()=>{
            string fake=Path.Combine(files.Data,"TgWsProxy_windows.exe");File.WriteAllText(fake,"not a PE");Require(new TgExeAdapter().Validate(fake)!=null);
            bool rejected=false;try{ZapretPreparation.Prepare(files,new UserSettings{Strategy="general (ALT).bat",ZapretDirectory=Path.Combine(files.Root,"external-preview")});}catch(NotSupportedException){rejected=true;}Require(rejected);
        }));
        if(NetworkRequested)
        {
            var live=files;using var installer=new ModuleInstaller(live);
            await Test("REAL official GitHub Zapret ZIP download, digest, extraction and adapter",async()=>{
                var asset=await installer.Discover("zapret");var item=await installer.Install(asset);Require(item.Version=="1.10.3");var settings=installer.ApplyManaged(live.Load());var plan=ZapretPreparation.Prepare(live,settings);Require(plan.Executable.EndsWith("winws.exe")&&plan.CreateStartInfo().CreateNoWindow);File.WriteAllText(Path.Combine(files.Data,"live-zapret-asset.json"),JsonSerializer.Serialize(asset));File.WriteAllText(Path.Combine(files.Data,"live-zapret-catalog.json"),JsonSerializer.Serialize(new {FromCache=installer.UsingCache,RetrievedUtc=installer.CatalogTimestamp}));
            });
            await Test("REAL official TG Windows EXE download, digest and PE validation",async()=>{
                var asset=await installer.Discover("tg");var item=await installer.Install(asset);var settings=installer.ApplyManaged(live.Load());Require(item.Version==asset.Version&&new TgExeAdapter(live).Validate(settings.TgExe)==null);live.Save(settings);File.WriteAllText(Path.Combine(files.Data,"live-tg-asset.json"),JsonSerializer.Serialize(asset));File.WriteAllText(Path.Combine(files.Data,"live-tg-catalog.json"),JsonSerializer.Serialize(new {FromCache=installer.UsingCache,RetrievedUtc=installer.CatalogTimestamp}));
            });
            await Test("REAL installed Modules and Data copy into Cyrillic path resolves portable modules",()=>Sync(()=>{
                string root=Path.Combine(files.Root,"moved-test","Перенос Open Net");Directory.CreateDirectory(root);CopyTree(live.Data,Path.Combine(root,"Data"));CopyTree(live.Modules,Path.Combine(root,"Modules"));var moved=new AppFiles(root);using var catalog=new ModuleInstaller(moved);var settings=catalog.ApplyManaged(moved.Load());Require(settings.TgExe.StartsWith(root)&&settings.ZapretDirectory.StartsWith(root));Require(new TgExeAdapter(moved).Validate(settings.TgExe)==null&&!new ZapretAdapter(moved.ForZapret(settings)).Validate().Any(x=>x.Error));
            }));
            await Test("REAL installed versions displayed by main UI; no network executable launched",async()=>{
                shell.ApplySettings(installer.ApplyManaged(live.Load()));await shell.Refresh();
                Require(shell.Find<TextBlock>("ZapretVersion").Text=="1.10.3"&&shell.Find<TextBlock>("TgVersion").Text==JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(Path.Combine(files.Data,"live-tg-asset.json")))!.Version&&shell.Runtime.LaunchCount==2);
                Capture(shell.Window,Path.Combine(files.Data,"main-installed.png"));File.WriteAllText(Path.Combine(files.Data,"no-network-launch.json"),"{\"OriginalToolsLaunchedByTest\":false}");
            });
            await Test("REAL TG same-version install preserves profile; corrupt download leaves active version intact",async()=>{
                var old=installer.Current("tg")!;string source=live.ModulePath(old.RelativePath);byte[] binary=File.ReadAllBytes(source);
                string profile=Path.Combine(Path.GetDirectoryName(source)!,"test-user-profile.json");File.WriteAllText(profile,"{\"test\":true}");
                var asset=JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(Path.Combine(files.Data,"live-tg-asset.json")))!;
                using(var broken=new ModuleInstaller(live,new FakeHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{1})}),_=>false)){bool failed=false;try{await broken.Install(asset);}catch(InvalidDataException){failed=true;}Require(failed&&installer.Current("tg")!.RelativePath==old.RelativePath);}
                using(var updater=new ModuleInstaller(live,new FakeHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(binary)}),_=>false)){var updated=await updater.Install(asset);Require(updated.RelativePath==old.RelativePath&&File.ReadAllText(profile)=="{\"test\":true}");}
                shell.ApplySettings(installer.ApplyManaged(live.Load()));
            });
        }
        await Tests021.Run(shell,files,Test);
        await Tests022.Run(shell,files,Test);
        await TestsFoundation.Run(shell,files,Test);
        await Tests030.Run(shell,files,Test);
        await Tests031.Run(shell,files,Test);
        await Tests032.Run(shell,files,Test);
        await Tests032Final.Run(shell,files,Test);
        await TestsFluent.Run(shell,files,Test);
        await TestsHb.Ui(shell,files,Test);await TestsR5.Ui(shell,files,Test);await Tests033.Ui(shell,files,Test);
        await TestsStartupReadback.Ui(files,Test);await TestsSettingsCompact.Ui(shell,files,Test);await TestsScrollbars.Ui(shell,files,Test);
        await Test("Tray Exit terminates shell and removes icon",()=>Sync(()=>{shell.TrayExit.PerformClick();Require(!shell.Tray.Visible);}));
        File.WriteAllText(Path.Combine(files.Data,"ui-tests.json"),JsonSerializer.Serialize(new{Total=results.Count,Failed=failures,ExecutableSHA256=Metadata.Hash(Environment.ProcessPath!),NetworkInstallation=NetworkRequested,Results=results},new JsonSerializerOptions{WriteIndented=true}));shell.ExitCode=failures==0?0:1;
    }
    public static void CopyTree(string source,string target){Directory.CreateDirectory(target);foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){Paths.NoReparseAncestors(file);string path=Paths.Owned(target,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.Copy(file,path,false);}}
    public static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);}
}







