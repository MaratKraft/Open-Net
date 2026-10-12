using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class TestsFoundation
{
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    static void Check(bool value,string detail){if(!value)throw new Exception(detail);}
    sealed class Handler(byte[] binary):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(binary)});}
    static void Screen(Window window,string file,IntPtr foreground)
    {
        var activation=Application.Current.Windows.Cast<Window>().First(x=>new WindowInteropHelper(x).Handle==foreground);TestActivation.Own(activation);
        Check(GetForegroundWindow()==foreground,"Own foreground only");var hwnd=new WindowInteropHelper(window).Handle;Check(GetWindowRect(hwnd,out var r),"Own bounds");
        using var bitmap=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using var graphics=System.Drawing.Graphics.FromImage(bitmap);graphics.CopyFromScreen(r.Left,r.Top,0,0,bitmap.Size);bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);
        // A native one-pixel shadow edge is permissible; a thick white client border is not.
        foreach(int d in new[]{2,3,4,5})foreach(var point in new[]{(d,100),(bitmap.Width-d-1,100),(200,d),(200,bitmap.Height-d-1)}){var p=bitmap.GetPixel(point.Item1,point.Item2);Check(!(p.R>235&&p.G>235&&p.B>235),"Thick white border at "+point);}
    }
    static void Visual(FrameworkElement element,string file,double scale=1,Window? window=null)
    {
        element.UpdateLayout();Check(element.ActualWidth>0&&element.ActualHeight>0,"Visual layout exists");
        System.Windows.Media.Imaging.BitmapSource evidence;
        if(window==null){var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(element);evidence=image;}
        else{window.UpdateLayout();var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*scale),(int)Math.Ceiling(window.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(window);var point=element.TranslatePoint(new Point(0,0),window);evidence=new System.Windows.Media.Imaging.CroppedBitmap(image,new Int32Rect((int)Math.Round(point.X*scale),(int)Math.Round(point.Y*scale),(int)Math.Floor(element.ActualWidth*scale),(int)Math.Floor(element.ActualHeight*scale)));}
        byte[] sample=new byte[evidence.PixelWidth*evidence.PixelHeight*4];evidence.CopyPixels(sample,evidence.PixelWidth*4,0);Check(Enumerable.Range(0,sample.Length/4).Count(i=>sample[i*4+3]>0)>sample.Length/40,"Rendered evidence contains actual control pixels");
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(evidence));using var output=File.Create(file);encoder.Save(output);
    }
    public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        string fixture=Path.Combine(files.Root,"foundation fixtures","Стратегии с пробелами");Directory.CreateDirectory(fixture);
        await test("022 foundation natural BAT order includes future numbers, only existing files",()=>{
            foreach(string name in new[]{"general (ALT13).bat","general (ALT2).bat","general (ALT).bat","general (ALT15).bat","general.bat","general (ALT1).bat","general (ALT10).bat","general (ALT14).bat"})File.WriteAllText(Path.Combine(fixture,name),"test data, never executed");
            Check(StrategyList.Read(fixture).SequenceEqual(new[]{"general.bat","general (ALT).bat","general (ALT1).bat","general (ALT2).bat","general (ALT10).bat","general (ALT13).bat","general (ALT14).bat","general (ALT15).bat"}),"Natural numeric ordering");return Task.CompletedTask;
        });
        await test("022 foundation ALT13 default, explicit choice persists, missing BAT never silently replaced",()=>{
            var saved=shell.Settings;
            try{
                Check(new UserSettings().Strategy==StrategyList.Preferred,"Default ALT13");shell.ApplySettings(new UserSettings{ZapretDirectory=fixture});var view=shell.OpenModuleSettings("zapret");var combo=(ComboBox)view.FindName("Strategy");Check(combo.SelectedItem?.ToString()==StrategyList.Preferred,"Available default");view.Close();
                shell.ApplySettings(new UserSettings{ZapretDirectory=fixture,Strategy="general (ALT2).bat"});Check(files.Load().Strategy=="general (ALT2).bat","Restart persistence");view=shell.OpenModuleSettings("zapret");Check(((ComboBox)view.FindName("Strategy")).SelectedItem?.ToString()=="general (ALT2).bat","Preserve explicit choice");view.Close();
                File.Delete(Path.Combine(fixture,"general (ALT13).bat"));shell.ApplySettings(new UserSettings{ZapretDirectory=fixture});view=shell.OpenModuleSettings("zapret");Check(((ComboBox)view.FindName("Strategy")).SelectedItem==null,"Missing default needs choice");view.Close();
                shell.ApplySettings(new UserSettings{ZapretDirectory=fixture,Strategy="general (ALT99).bat"});view=shell.OpenModuleSettings("zapret");Check(((ComboBox)view.FindName("Strategy")).SelectedItem==null,"Missing user choice needs choice");view.Close();
            }finally{shell.ApplySettings(saved);}return Task.CompletedTask;
        });
        await test("022 foundation four settings actions in one unclipped capsule row, English/Russian at four render scales",()=>{
            string saved=shell.Settings.Language;
            try{foreach(string language in new[]{"en","ru"}){
                shell.Settings.Language=language;shell.ApplySettings(shell.Settings);var view=shell.OpenSettings();view.UpdateLayout();var actions=(Grid)view.FindName("SettingsActions");
                var buttons=new[]{"OpenData","OpenModules","OpenLogs","Licenses"}.Select(name=>(Button)view.FindName(name)).ToArray();
                for(int i=0;i<buttons.Length;i++){var b=buttons[i];Check(Grid.GetColumn(b)==i%2&&b.Height==36,"Single aligned row");var text=new FormattedText(b.Content.ToString()!,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(b.FontFamily,b.FontStyle,b.FontWeight,b.FontStretch),b.FontSize,Brushes.White,VisualTreeHelper.GetDpi(b).PixelsPerDip);Check(text.Width+b.Padding.Left+b.Padding.Right<=b.ActualWidth,"Action caption fits: "+b.Content);}
                Check(!buttons.Any(b=>b.Content.ToString()!.Contains("0.1")),"Current settings import only");
                foreach(double scale in new[]{1.0,1.25,1.5,2.0})Visual(actions,Path.Combine(files.Data,$"actions-{language}-{scale*100}.png"),scale,view);
                SelfTests.Capture(view,Path.Combine(files.Data,"foundation-settings-"+language+".png"));view.Close();
            }}finally{shell.Settings.Language=saved;shell.ApplySettings(shell.Settings);}return Task.CompletedTask;
        });
        await test("022 foundation Browse offers exactly three localized Zapret actions; folder plan targets chosen folder",async()=>{
            var saved=shell.Settings;shell.ApplySettings(new UserSettings{ZapretDirectory=fixture});var view=shell.OpenModuleSettings("zapret");
            try{var button=(Button)view.FindName("ZapretPathBrowse");TestActivation.Own(view);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var menu=button.ContextMenu!;Check(menu.IsOpen&&menu.Items.Count==3,"Browse menu");Check(menu.Items.Cast<MenuItem>().Select(x=>x.Header.ToString()).SequenceEqual(new[]{L.T("Open Zapret folder"),L.T("Configure Zapret"),L.T("Select folder")}),"Menu labels");var plan=ZapretActions.FolderPlan(fixture);Check(plan.FileName==fixture&&plan.UseShellExecute&&plan.Verb=="open","Explorer chosen directory plan");Visual(menu,Path.Combine(files.Data,"zapret-browse-menu.png"));menu.IsOpen=false;var combo=(ComboBox)view.FindName("Strategy");combo.IsDropDownOpen=true;await Task.Delay(100);Visual((FrameworkElement)((System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup",combo)).Child,Path.Combine(files.Data,"strategy-dropdown.png"));combo.IsDropDownOpen=false;}finally{view.Close();shell.ApplySettings(saved);}
        });
        await test("022 foundation original service confirmation cancellation and approved plan use only safe local stub",async()=>{
            using(var input=File.OpenRead(Path.Combine(files.Root,"test-results","Fixtures","service.bat"))){using var output=File.Create(Path.Combine(fixture,"service.bat"));input.CopyTo(output);}
            string digest=Metadata.Hash(Path.Combine(fixture,"service.bat"));int launches=0;
            Check(!await ZapretActions.Configure(fixture,()=>false,_=>{launches++;return Task.CompletedTask;})&&launches==0,"Cancellation without launch");
            Check(await ZapretActions.Configure(fixture,()=>true,async plan=>{
                launches++;Check(plan.UseShellExecute&&plan.Verb=="runas"&&plan.WindowStyle==ProcessWindowStyle.Normal&&plan.WorkingDirectory==fixture,"Visible original UAC console plan");Check(plan.Arguments=="/d /v:off /s /c \"\""+Path.Combine(fixture,"service.bat")+"\" admin\"","Exact documented interactive entry; no menu selection");
                // Deliberately never execute the real plan. Run this exact application's safe stub instead.
                string dir=Path.Combine(files.Data,"console-stub");Directory.CreateDirectory(dir);string gate=Path.Combine(dir,"gate"),stop=Path.Combine(dir,"stop");File.WriteAllText(gate,"");File.WriteAllText(stop,"");var safe=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};foreach(string arg in new[]{"--safe-stub","--gate",gate,"--stop",stop})safe.ArgumentList.Add(arg);using var child=Process.Start(safe)!;await child.WaitForExitAsync();Check(child.ExitCode==0,"Safe child closed");await shell.Refresh();
            })&&launches==1&&Metadata.Hash(Path.Combine(fixture,"service.bat"))==digest,"Original BAT unchanged");
            File.AppendAllText(Path.Combine(fixture,"service.bat"),"modified");bool rejected=false;try{ZapretActions.ServicePlan(fixture);}catch(InvalidDataException){rejected=true;}Check(rejected,"Modified console rejected");
        });
        await test("022 foundation Fluent vector gear and caption focus have no generic blue capsule",()=>{
            var gear=shell.Find<System.Windows.Shapes.Path>("SettingsGear");Check(gear.Data.Bounds.Width>15&&gear.Data.Bounds.Height>15,"Fluent geometry present");var close=shell.Find<Button>("Close");close.ApplyTemplate();var surface=(Border)close.Template.FindName("CaptionSurface",close);FocusCues.SetMode(true);close.Focus();Check(surface.BorderThickness==new Thickness(0)&&surface.CornerRadius.TopLeft==5&&close.FocusVisualStyle!=null,"Separate caption template; accessible keyboard focus");return Task.CompletedTask;
        });
        await test("022 foundation cross hides even with close preference disabled; tools remain owned and running",async()=>{
            var saved=shell.Settings;shell.Settings.CloseToTray=false;shell.ApplySettings(shell.Settings);shell.Find<CheckBox>("ZapretCheck").IsChecked=true;shell.Find<CheckBox>("TgCheck").IsChecked=true;await shell.StartSelected();await shell.Refresh();var pids=shell.Observations.Select(x=>x.Pid!.Value).ToArray();
            shell.Find<Button>("Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!shell.Window.IsVisible&&!shell.Window.ShowInTaskbar&&pids.All(x=>ProcessIdentity.Read(x).Exists),"Cross preserved own stubs");shell.Restore();await shell.StopSelected(false);shell.ApplySettings(saved);
        });
        await test("022 foundation native activation, redraw, focus, tray restore, position and material switching show no thick white border",async()=>{
            var hwnd=new WindowInteropHelper(shell.Window).Handle;double left=shell.Window.Left,top=shell.Window.Top;string material=shell.Settings.Backdrop;
            var other=new Window{Title="Open Net owned activation target",Width=120,Height=80,Left=0,Top=0,ShowInTaskbar=false,Background=Brushes.DarkGray};other.Show();
            try{
                foreach(string mode in new[]{"mica","acrylic"}){
                    shell.Settings.Backdrop=mode;shell.ApplySettings(shell.Settings);shell.Restore();await Task.Delay(300);
                    for(int cycle=0;cycle<3;cycle++){
                        TestActivation.Own(other);await Task.Delay(150);Check(GetForegroundWindow()==new WindowInteropHelper(other).Handle,"Own deactivation target");Screen(shell.Window,Path.Combine(files.Data,$"frame-{mode}-{cycle}-inactive.png"),new WindowInteropHelper(other).Handle);
                        shell.Restore();shell.Find<Button>("Close").Focus();SendMessage(hwnd,0x85,new IntPtr(1),IntPtr.Zero);SendMessage(hwnd,0x86,new IntPtr(1),IntPtr.Zero);await Task.Delay(100);Screen(shell.Window,Path.Combine(files.Data,$"frame-{mode}-{cycle}-active.png"),hwnd);
                        shell.HideToTray();shell.Restore();shell.Window.WindowState=WindowState.Minimized;shell.Restore();shell.Window.Left=left+10;await Task.Delay(100);Check(Math.Abs(shell.Window.Left-left-10)<0.1,"Window position changes");shell.Window.Left=left;
                    }
                }
                // wParam=FALSE uses RECT rather than NCCALCSIZE_PARAMS and must also remain full client.
                Check(GetWindowRect(hwnd,out var rect),"Own rect");IntPtr buffer=Marshal.AllocHGlobal(Marshal.SizeOf<Rect>());try{Marshal.StructureToPtr(rect,buffer,false);SendMessage(hwnd,0x83,IntPtr.Zero,buffer);Check(Marshal.PtrToStructure<Rect>(buffer).Left==rect.Left,"NCCALCSIZE false no classic inset");}finally{Marshal.FreeHGlobal(buffer);}
            }finally{other.Close();shell.Window.Left=left;shell.Window.Top=top;shell.Settings.Backdrop=material;shell.ApplySettings(shell.Settings);shell.Restore();}
        });
        if(SelfTests.NetworkRequested)
        {
            await test("022 foundation actual UI install buttons download official modules, refresh versions, default ALT13; no auto launch",async()=>{
                string root=Path.Combine(files.Root,"UI install fixture","Чистая установка");Directory.CreateDirectory(root);var isolated=new AppFiles(root);using var instance=new SingleInstance(isolated.Data,isolated.Log);var ui=new Shell(isolated,true,instance);ui.Window.Show();
                try{
                    ui.InstallConfirmationForTests=text=>{Check(text.Contains("Tools will not start automatically."),"Explicit file-only confirmation");return true;};
                    foreach(string id in new[]{"zapret","tg"}){ui.Find<Button>(id=="zapret"?"ZapretInstall":"TgInstall").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(int n=0;ui.Busy&&n<2000;n++)await Task.Delay(100);Check(!ui.Busy&&ui.Installer.Current(id)!=null,"Actual install button completed: "+id);}
                    await ui.Refresh();Check(ui.Runtime.LaunchCount==0&&ui.Settings.Strategy==StrategyList.Preferred&&isolated.Load().Strategy==StrategyList.Preferred,"No automatic tool launch; ALT13 saved");Check(ui.Find<TextBlock>("ZapretVersion").Text=="1.10.3"&&ui.Find<TextBlock>("TgVersion").Text==ui.Installer.Current("tg")!.Version,"Version labels updated");SelfTests.Capture(ui.Window,Path.Combine(files.Data,"actual-ui-installed.png"));
                    var view=ui.OpenModuleSettings("zapret");Check(((ComboBox)view.FindName("Strategy")).SelectedItem?.ToString()==StrategyList.Preferred,"Fresh installed ALT13 selected in settings");view.Close();
                    File.WriteAllText(Path.Combine(files.Data,"actual-ui-install.json"),JsonSerializer.Serialize(new{InstallerButtonsTriggered=true,Confirmation="internal safe-mode injection; production dialog unchanged",OriginalToolsLaunched=false,Zapret="1.10.3",TG=ui.Installer.Current("tg")!.Version,Strategy=isolated.Load().Strategy}));
                }finally{typeof(Shell).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ui,true);ui.Window.Close();ui.Runtime.CloseShell();ui.Dispose();Application.Current.Resources.MergedDictionaries.Remove(ui.Window.Resources);shell.Restore();}
            });
            await test("022 foundation full official ZIP byte-for-byte payload preserved in flat readable Modules folder",async()=>{
                var item=shell.Installer.Current("zapret")!;string root=files.ModulePath(item.RelativePath);Check(item.RelativePath=="zapret-discord-youtube-1.10.3"&&File.Exists(Path.Combine(root,"service.bat"))&&Directory.Exists(Path.Combine(root,"bin"))&&Directory.Exists(Path.Combine(root,"lists")),"Readable flat module root");
                var asset=JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(Path.Combine(files.Data,"live-zapret-asset.json")))!;using var http=new HttpClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("Open-Net/0.2.2");byte[] bytes=await http.GetByteArrayAsync(asset.Url);Check(Convert.ToHexString(SHA256.HashData(bytes))==item.SHA256,"Official ZIP SHA");string archive=Path.Combine(files.Data,"official-zapret-verification.zip");File.WriteAllBytes(archive,bytes);using var zip=ZipFile.OpenRead(archive);string prefix=zip.Entries.Single(x=>x.FullName.EndsWith("/general.bat")).FullName[..^"general.bat".Length];int count=0;
                foreach(var entry in zip.Entries.Where(x=>x.FullName.StartsWith(prefix)&&!x.FullName.EndsWith('/'))){string path=Paths.Owned(root,entry.FullName[prefix.Length..]);using var input=entry.Open();Check(File.Exists(path)&&Convert.ToHexString(SHA256.HashData(input))==Metadata.Hash(path),"Original payload changed: "+entry.FullName);count++;}
                Check(count==Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length,"Every original file retained, no nesting or additions");File.WriteAllText(Path.Combine(files.Data,"full-zapret-payload.json"),JsonSerializer.Serialize(new{OriginalFiles=count,AllBytesIdentical=true,Root=item.RelativePath,SHA256=item.SHA256,ServiceExecuted=false}));
            });
            await test("022 foundation different-version transaction fixture retains prior TG, profiles and supports rollback; collision/running guard preserve files",async()=>{
                var current=shell.Installer.Current("tg")!;byte[] binary=File.ReadAllBytes(files.ModulePath(current.RelativePath));var asset=JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(Path.Combine(files.Data,"live-tg-asset.json")))!;
                string root=Path.Combine(files.Root,"version transaction fixture");Directory.CreateDirectory(root);var isolated=new AppFiles(root);using var installer=new ModuleInstaller(isolated,new Handler(binary),_=>false);
                string oldPath="tg-ws-proxy-fixture-previous/TgWsProxy_windows.exe";Directory.CreateDirectory(Path.GetDirectoryName(isolated.ModulePath(oldPath))!);File.WriteAllBytes(isolated.ModulePath(oldPath),binary);File.WriteAllText(Path.Combine(Path.GetDirectoryName(isolated.ModulePath(oldPath))!,"user-profile.json"),"keep profile");Directory.CreateDirectory(Path.GetDirectoryName(installer.Manifest("tg"))!);var previous=current with{Version="fixture-previous",RelativePath=oldPath};File.WriteAllText(installer.Manifest("tg"),JsonSerializer.Serialize(previous));
                var installed=await installer.Install(asset);Check(File.Exists(isolated.ModulePath(oldPath))&&installed.RelativePath=="tg-ws-proxy-"+asset.Version+"/TgWsProxy_windows.exe","Previous version retained");Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(isolated.ModulePath(installed.RelativePath))!,"user-profile.json"))=="keep profile","Profile preserved");installer.Rollback("tg");Check(installer.Current("tg")!.RelativePath==previous.RelativePath&&File.Exists(isolated.ModulePath(installed.RelativePath)),"Rollback preserves both versions");
                bool collision=false;try{await installer.Install(asset);}catch(InvalidDataException){collision=true;}Check(collision&&installer.Current("tg")!.RelativePath==previous.RelativePath,"Unknown/inactive canonical folder not overwritten");
                using var running=new ModuleInstaller(isolated,new Handler(binary),_=>true);bool blocked=false;try{await running.Install(asset);}catch(InvalidOperationException){blocked=true;}Check(blocked&&installer.Current("tg")!.RelativePath==previous.RelativePath,"Running module not replaced");
                var explicitSettings=new UserSettings{ZapretDirectory=fixture,Strategy="general (ALT2).bat",TgExe=Path.Combine(root,"external.exe")};Check(installer.ApplyManaged(explicitSettings).TgExe==explicitSettings.TgExe,"External choice retained");
            });
        }
    }
}




