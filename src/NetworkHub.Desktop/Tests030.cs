using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NetworkHub.App;
using NetworkHub.Adapters;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class Tests030
{
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out Rect r);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    static void Check(bool value,string detail){if(!value)throw new Exception(detail);}
    sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(response(request));}
    static void Screen(Window view,string path){TestActivation.Own(view);var hwnd=new WindowInteropHelper(view).Handle;Check(GetForegroundWindow()==hwnd,"Own foreground window only");Check(GetWindowRect(hwnd,out var r),"Own rectangle");using var image=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using var graphics=System.Drawing.Graphics.FromImage(image);graphics.CopyFromScreen(r.Left,r.Top,0,0,image.Size);image.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("030 marker-only portable storage; no LocalAppData fallback on write failure",()=>{
            string root=Path.Combine(files.Root,"storage fixture","Портативная папка");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"portable.flag"),"");var portable=PortableStartup.Open(root);Check(portable.Data==Path.Combine(root,"Data")&&portable.Modules==Path.Combine(root,"Modules")&&portable.Portable,"Marker selects portable");portable.Save(new UserSettings{ZapretDirectory=Path.Combine(portable.Modules,"zapret-test"),Strategy="general.bat"});Check(portable.Load().Strategy=="general.bat","Stored user choice");
            string moved=Path.Combine(files.Root,"storage fixture","Перенос с пробелами");SelfTests.CopyTree(root,moved);var reopened=PortableStartup.Open(moved);Check(reopened.Load().ZapretDirectory.StartsWith(Path.Combine(moved,"Modules")),"Relative path moved");
            string denied=Path.Combine(files.Root,"storage fixture","Нельзя записать");Directory.CreateDirectory(denied);File.WriteAllText(Path.Combine(denied,"portable.flag"),"");File.WriteAllText(Path.Combine(denied,"Data"),"deliberate failure");bool failed=false;try{PortableStartup.Open(denied);}catch(IOException ex){failed=ex.Message.Contains("no AppData fallback");}Check(failed,"Portable failure explicit, no silent fallback");return Task.CompletedTask;
        });
        await test("030 neutral keyboard-only button focus; main ScrollViewer has no dotted container rectangle",()=>{
            FocusCues.SetMode(true);var button=shell.Find<Button>("Settings");button.Focus();button.ApplyTemplate();var border=(CapsuleBorder)button.Template.FindName("Bd",button);Check(border.BorderThickness==new Thickness(0)&&button.Focusable&&button.FocusVisualStyle!=null,"No blue border; keyboard access retained");Check(shell.Find<ScrollViewer>("MainScroll").FocusVisualStyle==null&&shell.Window.FocusVisualStyle==null,"No default container focus adorners");return Task.CompletedTask;
        });
        await test("030 immediate repeated Acrylic to Mica keeps HWND visible and restores real backdrop without hide or resize",async()=>{
            if(Environment.OSVersion.Version.Build<22621)return;string saved=shell.Settings.Backdrop;shell.Settings.Backdrop="mica";shell.ApplySettings(shell.Settings);shell.Restore();TestActivation.Own(shell.Window);await Task.Delay(400);var hwnd=new WindowInteropHelper(shell.Window).Handle;double width=shell.Window.Width,height=shell.Window.Height;Screen(shell.Window,Path.Combine(files.Data,"030-mica-before.png"));
            try{for(int n=0;n<4;n++){
                shell.Settings.Backdrop="acrylic";shell.ApplySettings(shell.Settings);await Task.Delay(250);shell.Settings.Backdrop="mica";shell.ApplySettings(shell.Settings);await Task.Delay(400);
                Check(shell.Window.IsVisible&&new WindowInteropHelper(shell.Window).Handle==hwnd&&shell.Window.Width==width&&shell.Window.Height==height,"No hide, recreate, or resize");Check(DwmGetWindowAttribute(hwnd,38,out int value,4)==0&&value==2,"Mica selected immediately");Screen(shell.Window,Path.Combine(files.Data,$"030-mica-return-{n}.png"));
                using var baseline=new System.Drawing.Bitmap(Path.Combine(files.Data,"030-mica-before.png"));using var after=new System.Drawing.Bitmap(Path.Combine(files.Data,$"030-mica-return-{n}.png"));var p=baseline.GetPixel(200,70);var q=after.GetPixel(200,70);Check(Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B)<20&&q.R+q.G+q.B>15,"Actual Mica pixels restored; no black stale surface");
            }}finally{shell.Settings.Backdrop=saved;shell.ApplySettings(shell.Settings);}
        });
        await test("030 dark log, native dark title, UTF-8 Cyrillic, selection/copy and bounded asynchronous large-file tail",async()=>{
            string path=Path.Combine(files.Data,"large-log-fixture.log");File.WriteAllText(path,string.Concat(Enumerable.Repeat(new string('x',5000)+"\n",850))+"\nКириллица — проверка UTF-8\nlong: "+new string('y',5000));string text=await Task.Run(()=>LogSafety.Tail(path,256*1024));Check(text.Length<=256*1024+10&&text.Contains("Кириллица"),"Bounded UTF-8 tail");var view=LogView.Create(shell.Window,text);view.Show();TestActivation.Own(view);await Task.Delay(300);
            try{var field=(TextBox)view.FindName("LogText");Check(((SolidColorBrush)field.Background).Color.R==30&&((SolidColorBrush)field.Foreground).Color==Colors.White&&field.IsReadOnly&&field.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto,"Dark readable selectable log");field.Select(text.IndexOf("Кириллица"),9);Check(field.SelectedText=="Кириллица"&&System.Windows.Input.ApplicationCommands.Copy.CanExecute(null,field),"Copy command available, clipboard untouched");field.ScrollToEnd();field.ScrollToHorizontalOffset(500);view.UpdateLayout();Check(field.VerticalOffset>0&&field.HorizontalOffset>0,"Both log scroll axes work");Check(Notices.Text().Contains("MIT"),"Runtime/Fluent notices embedded");Screen(view,Path.Combine(files.Data,"030-log-dark.png"));}finally{view.Close();shell.Restore();}
        });
        await test("030 future version selected by official artifact format, not hardcoded tag",async()=>{
            string json="[{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v7.8.9\",\"published_at\":\"2026-01-01T00:00:00Z\",\"html_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/tag/v7.8.9\",\"assets\":[{\"name\":\"TgWsProxy_windows.exe\",\"size\":10,\"browser_download_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/download/v7.8.9/TgWsProxy_windows.exe\"}]}]";var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"future-catalog"));using var installer=new ModuleInstaller(isolated,new Handler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)}));Check((await installer.Discover("tg")).Version=="v7.8.9","Future stable Windows artifact discovered");
        });
        await test("030 changing official compatible BAT accepted by format; unknown option and tampering blocked",()=>{
            string folder=Path.Combine(files.Root,"format fixture");Directory.CreateDirectory(folder);string name="general (ALT14).bat",path=Path.Combine(folder,name);string original=File.ReadAllText(Path.Combine(files.Root,"test-results","Fixtures","ALT13.bat"));File.WriteAllText(path,"rem future official release\n"+original);var hashes=ModuleTrust.HashPayload(folder);Check(ReviewedStrategy.Supported(folder,name,hashes),"Compatible new BAT without precompiled SHA");File.AppendAllText(path," & calc");Check(!ReviewedStrategy.Supported(folder,name,hashes),"Changed file rejected");File.WriteAllText(path,original.Replace("--dpi-desync-repeats=", "--unknown-option="));hashes=ModuleTrust.HashPayload(folder);Check(!ReviewedStrategy.Supported(folder,name,hashes),"Unknown future option requires adapter review");return Task.CompletedTask;
        });
        if(SelfTests.NetworkRequested)await test("030 real latest TG official SHA, native launch plan without execution, manual offline import and actual version rollback",async()=>{
            var latest=shell.Installer.Current("tg")!;string exe=files.ModulePath(latest.RelativePath);Check(new TgExeAdapter(files).Validate(exe)==null,"New official TG trusted without compiled digest");Check(latest.Version!="v1.11.1","Real update beyond old version");var plan=new TgExeAdapter(files).Plan(exe);Check(plan.ArgumentList.Count==0&&plan.WorkingDirectory==Path.GetDirectoryName(exe),"Native GUI contract, no listener started");
            byte[] binary=File.ReadAllBytes(exe);string root=Path.Combine(files.Root,"offline import fixture");Directory.CreateDirectory(root);var isolated=new AppFiles(root);var asset=JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(Path.Combine(files.Data,"live-tg-asset.json")))!;using var installer=new ModuleInstaller(isolated,new Handler(_=>throw new HttpRequestException("offline")),_=>false);Directory.CreateDirectory(Path.Combine(isolated.Data,"Cache"));File.WriteAllText(Path.Combine(isolated.Data,"Cache","releases-tg.json"),JsonSerializer.Serialize(new ReleaseCache(DateTimeOffset.UtcNow,asset)));var cached=await installer.Discover("tg");Check(installer.UsingCache,"Explicit offline catalog");var imported=await installer.Import(cached,exe);Check(imported.Version==asset.Version&&new TgExeAdapter(isolated).Validate(isolated.ModulePath(imported.RelativePath))==null,"Official manual import verified offline");
            string wrong=Path.Combine(root,"corrupted.exe");File.WriteAllBytes(wrong,binary[..^1]);bool rejected=false;try{await installer.Import(cached,wrong);}catch(InvalidDataException){rejected=true;}Check(rejected&&installer.Current("tg")!.SHA256==latest.SHA256,"Corrupted import leaves active intact");
            // A real old official release is downloaded and imported; never executed.
            var oldAsset=new ReleaseAsset("tg","v1.11.1","TgWsProxy_windows.exe","https://github.com/Flowseal/tg-ws-proxy/releases/download/v1.11.1/TgWsProxy_windows.exe",22122820,"sha256:"+TgExeAdapter.Digest,"https://github.com/Flowseal/tg-ws-proxy/releases/tag/v1.11.1","v1.11.1");using var http=new HttpClient();byte[] old=await http.GetByteArrayAsync(oldAsset.Url);string oldFile=Path.Combine(root,"original-previous.exe");File.WriteAllBytes(oldFile,old);await installer.Import(oldAsset,oldFile);Check(installer.Current("tg")!.Version=="v1.11.1","Previous official artifact installed");installer.Rollback("tg");Check(installer.Current("tg")!.Version==latest.Version&&File.Exists(isolated.ModulePath(imported.RelativePath)),"Actual different release rollback preserved latest");
        });
    }
}




