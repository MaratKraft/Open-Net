using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetworkHub.App;
using NetworkHub.Adapters;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class Tests021
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    static void Check(bool condition){if(!condition)throw new Exception("0.2.1 assertion failed");}
    sealed class Handler:HttpMessageHandler
    {
        readonly Func<HttpRequestMessage,HttpResponseMessage> respond;
        public Handler(Func<HttpRequestMessage,HttpResponseMessage> respond)=>this.respond=respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(respond(request));}
    }
    public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("021 English default, immediate Russian in main/settings/tray, persisted selection and English fallback",async()=>{
            Check(new UserSettings().Language=="en"&&L.Language=="en");
            var window=shell.OpenSettings();var language=(ComboBox)window.FindName("Language");language.SelectedIndex=1;await shell.Refresh();
            window.UpdateLayout();SelfTests.Capture(window,Path.Combine(files.Data,"settings-ru.png"));
            Check(files.Load().Language=="ru"&&shell.TrayExit.Text==L.T("Exit")&&shell.Find<TextBlock>("SettingsCaption").Text==L.T("Settings")&&((Button)window.FindName("SaveSettings")).Content.ToString()==L.T("Save"));
            Check(L.T("Future English fallback")=="Future English fallback");
            Check(NetworkErrors.Message(new HttpRequestException("technical",new SocketException(10061))).Contains("отклонено"));
            language.SelectedIndex=0;window.UpdateLayout();SelfTests.Capture(window,Path.Combine(files.Data,"settings-en.png"));window.Close();await shell.Refresh();Check(files.Load().Language=="en"&&shell.TrayExit.Text=="Exit");
            L.Set("missing");Check(L.Language=="en"&&L.Russian.Count>=200&&L.Russian.All(x=>x.Key.Length>0&&x.Value.Length>0));
        });
        string bundle=Path.Combine(files.Root,"test-fixture-bundle");Directory.CreateDirectory(bundle);
        foreach(string name in new[]{"general.bat","ALT13.bat"}){using var input=File.OpenRead(Path.Combine(files.Root,"test-results","Fixtures",name));using var output=File.Create(Path.Combine(bundle,name=="ALT13.bat"?"general (ALT13).bat":name));input.CopyTo(output);}
        await test("021 exact ALT13 extraction preserves filters, repeats, TLS sequence, lists and no shell",()=>{
            string path=Path.Combine(bundle,"general (ALT13).bat"),lists=Path.Combine(files.Data,"alt13-lists");Check(ReviewedStrategy.Supported(bundle,"general (ALT13).bat"));
            var arguments=ReviewedStrategy.Extract(File.ReadAllText(path),bundle,lists);
            Check(arguments[0]=="--wf-tcp=80,443,2053,2083,2087,2096,8443,12"&&arguments[1]=="--wf-udp=443,19294-19344,50000-50100,12");
            Check(arguments.Count(x=>x=="--new")==8&&arguments.Count(x=>x.StartsWith("--dpi-desync-fake-tls="))==7);
            var repeated=arguments.Where(x=>x.StartsWith("--dpi-desync-fake-tls=")).Select(Path.GetFileName).ToArray();
            Check(repeated.SequenceEqual(new[]{"tls_clienthello_www_google_com.bin","tls_clienthello_sochi_park.bin","stun2.bin","tls_clienthello_sochi_park.bin","stun2.bin","tls_clienthello_sochi_park.bin","stun2.bin"}));
            Check(arguments.Where(x=>x.Contains("-user.txt")).All(x=>x.Contains(lists))&&arguments.All(x=>!x.Contains('%')));
            var plan=new LaunchPlan("zapret",Path.Combine(bundle,"bin","winws.exe"),bundle,arguments,Path.Combine(files.Data,"test.log"),true,"unverified");
            Check(plan.CreateStartInfo().CreateNoWindow&&!plan.CreateStartInfo().UseShellExecute&&plan.CreateStartInfo().ArgumentList.SequenceEqual(arguments));
            File.WriteAllText(Path.Combine(files.Data,"ALT13-arguments.json"),JsonSerializer.Serialize(arguments,new JsonSerializerOptions{WriteIndented=true}));return Task.CompletedTask;
        });
        await test("021 modified BAT, unknown BAT, path traversal and shell syntax are rejected",()=>{
            Check(!ReviewedStrategy.Supported(bundle,"../general.bat")&&!ReviewedStrategy.Supported(bundle,"general (ALT).bat"));
            string file=Path.Combine(bundle,"general (ALT13).bat"),original=File.ReadAllText(file);File.AppendAllText(file,"\r\nrem modified");Check(!ReviewedStrategy.Supported(bundle,Path.GetFileName(file)));File.WriteAllText(file,original);
            bool rejected=false;try{ReviewedStrategy.Extract(original+" & whoami",bundle,files.Data);}catch(InvalidDataException){rejected=true;}Check(rejected);return Task.CompletedTask;
        });
        await test("021 GameFilter parsed as data; valid ranges preserved; invalid or shell data rejected",()=>{
            Check(GameFilter.Read(bundle)==("12","12"));string utils=Path.Combine(bundle,"utils");Directory.CreateDirectory(utils);string path=Path.Combine(utils,"game_filter.enabled");
            File.WriteAllText(path,"mode=tcp\ntcp=1024-5000,6500\nudp=6000-7000\n");Check(GameFilter.Read(bundle)==("1024-5000,6500","12"));
            foreach(string value in new[]{"tcp=65536","tcp=5000-1024","tcp=443 & calc","mode=unknown"}){File.WriteAllText(path,value);bool rejected=false;try{GameFilter.Read(bundle);}catch(InvalidDataException){rejected=true;}Check(rejected);}File.Delete(path);return Task.CompletedTask;
        });
        await test("021 settings editor warns before unsupported selection and retains last confirmed strategy",()=>{
            var settings=files.Load();settings.Strategy="general (ALT13).bat";settings.LastWorkingStrategy="general.bat";settings.ZapretDirectory=bundle;shell.ApplySettings(settings);
            var window=shell.OpenModuleSettings("zapret");var strategies=(ComboBox)window.FindName("Strategy");Check(strategies.SelectedItem!.ToString()=="general (ALT13).bat");Check(((TextBlock)window.FindName("StrategySupport")).Text.StartsWith("Reviewed"));
            strategies.ItemsSource=new[]{"general (ALT13).bat","general (ALT).bat"};strategies.SelectedItem="general (ALT).bat";Check(((TextBlock)window.FindName("StrategySupport")).Text.StartsWith("Unsupported"));
            ((Button)window.FindName("SaveModuleSettings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(files.Load().Strategy=="general (ALT).bat"&&files.Load().LastWorkingStrategy=="general.bat");shell.ApplySettings(new UserSettings());return Task.CompletedTask;
        });
        await test("021 refusal retries exactly three times; no component state corruption; TLS and cancellation do not retry",async()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"network-tests"));int count=0;
            using(var installer=new ModuleInstaller(isolated,new Handler(_=>{count++;throw new HttpRequestException("refused",new SocketException(10061));}))){bool caught=false;try{await installer.Discover("tg");}catch(HttpRequestException ex){caught=NetworkErrors.Kind(ex)=="refused";}Check(caught&&count==3&&installer.Current("tg")==null);}
            count=0;using(var installer=new ModuleInstaller(isolated,new Handler(_=>{count++;throw new HttpRequestException("TLS",new AuthenticationException());}))){try{await installer.Discover("tg");}catch(HttpRequestException ex){Check(NetworkErrors.Kind(ex)=="tls");}Check(count==1);}
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();count=0;using(var installer=new ModuleInstaller(isolated,new Handler(_=>{count++;return new HttpResponseMessage(HttpStatusCode.OK);}))){bool caught=false;try{await installer.Discover("tg",cancelled.Token);}catch(OperationCanceledException ex){caught=NetworkErrors.Kind(ex,cancelled.Token)=="cancelled";}Check(caught&&count==0);}
        });
        await test("021 DNS, refusal, timeout, TLS, HTTP and cancellation have distinct localized remedies",()=>{
            var cases=new[]{(new HttpRequestException("dns",new SocketException(11001)) as Exception,"dns"),(new HttpRequestException("refused",new SocketException(10061)),"refused"),(new TaskCanceledException(),"timeout"),(new HttpRequestException("tls",new AuthenticationException()),"tls"),(new HttpRequestException("HTTP",null,HttpStatusCode.Forbidden),"http")};
            foreach(var item in cases){Check(NetworkErrors.Kind(item.Item1)==item.Item2);string english=NetworkErrors.Message(item.Item1);L.Set("ru");Check(NetworkErrors.Message(item.Item1)!=english);L.Set("en");}return Task.CompletedTask;
        });
        await test("021 successful catalog cached with timestamp; offline cache survives data copy; tampering blocked",async()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"cache-tests"));
            string json="[{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v1.11.1\",\"published_at\":\"2026-01-01T00:00:00Z\",\"html_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/tag/v1.11.1\",\"assets\":[{\"name\":\"TgWsProxy_windows.exe\",\"size\":1,\"browser_download_url\":\"https://github.com/Flowseal/tg-ws-proxy/releases/download/v1.11.1/TgWsProxy_windows.exe\"}]}]";
            using(var online=new ModuleInstaller(isolated,new Handler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)}))){await online.Discover("tg");Check(!online.UsingCache&&online.CatalogTimestamp!=null);}
            var moved=new AppFiles(files.Root,Path.Combine(files.Data,"cache-moved"));SelfTests.CopyTree(isolated.Data,moved.Data);
            using(var offline=new ModuleInstaller(moved,new Handler(_=>throw new HttpRequestException("refused",new SocketException(10061))))){var asset=await offline.Discover("tg");Check(asset.Version=="v1.11.1"&&offline.UsingCache&&offline.CatalogTimestamp!=null&&offline.LastCatalogFailure!=null);}
            string cache=Path.Combine(moved.Data,"Cache","releases-tg.json");File.WriteAllText(cache,File.ReadAllText(cache).Replace("github.com/Flowseal/","evil.example/Flowseal/"));
            using(var offline=new ModuleInstaller(moved,new Handler(_=>throw new HttpRequestException("refused",new SocketException(10061))))){bool rejected=false;try{await offline.Discover("tg");}catch(HttpRequestException){rejected=true;}Check(rejected);}
        });
        await test("021 Windows 10/no transparency/high contrast/battery saver select solid fallback",()=>{
            foreach(int build in new[]{14393,17763,19044,19045,22000})Check(WindowAppearance.Select(build,true,false,false,"mica")=="solid");
            Check(WindowAppearance.Select(22621,true,false,false,"mica")=="mica"&&WindowAppearance.Select(26200,true,false,false,"acrylic")=="acrylic");
            Check(WindowAppearance.Select(26200,false,false,false,"mica")=="solid"&&WindowAppearance.Select(26200,true,true,false,"mica")=="solid"&&WindowAppearance.Select(26200,true,false,true,"acrylic")=="solid");return Task.CompletedTask;
        });
        await test("021 WPF layout/render at 100/125/150/200 percent; capsule radius; focus and text bounds",()=>{
            foreach(double scale in new[]{1.0,1.25,1.5,2.0}){
                using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("NetworkHub.MainView.xaml")!;var preview=(Window)XamlReader.Load(stream);var content=(FrameworkElement)preview.Content;((Image)preview.FindName("Logo")).Source=IconFactory.Logo();
                content.LayoutTransform=new ScaleTransform(scale,scale);content.Measure(new Size(460*scale,710*scale));content.Arrange(new Rect(0,0,460*scale,710*scale));content.UpdateLayout();
                var primary=(Button)preview.FindName("Primary");primary.ApplyTemplate();content.UpdateLayout();var border=(CapsuleBorder)primary.Template.FindName("Bd",primary);Check(Math.Abs(border.CornerRadius.TopLeft-border.ActualHeight/2)<0.1&&primary.ActualWidth>300&&primary.Focusable);
                foreach(var text in LocalizedTree.Walk(content).OfType<TextBlock>())Check(text.ActualWidth>=0&&text.ActualHeight>=0);
                var bitmap=new RenderTargetBitmap((int)(460*scale),(int)(710*scale),96,96,PixelFormats.Pbgra32);bitmap.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(files.Data,"layout-"+(int)(scale*100)+".png"));png.Save(output);
            }return Task.CompletedTask;
        });
        await test("021 embedded original logo and multi-size ICO work on light/dark backgrounds",()=>{
            using var icon=IconFactory.Create();Check(icon.Width==32&&icon.Height==32&&IconFactory.Logo().PixelWidth>256);
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenNet.Icon")!;using var reader=new BinaryReader(stream);Check(reader.ReadUInt16()==0&&reader.ReadUInt16()==1&&reader.ReadUInt16()==9);return Task.CompletedTask;
        });
        await test("021 native Windows 11 Mica/Acrylic attributes applied; own-window snapshots only",async()=>{
            if(Environment.OSVersion.Version.Build<22621)return;
            shell.Restore();foreach(string material in new[]{"mica","acrylic"}){
                shell.Settings.Backdrop=material;shell.ApplySettings(shell.Settings);await Task.Delay(150);
                var hwnd=new System.Windows.Interop.WindowInteropHelper(shell.Window).Handle;Check(DwmGetWindowAttribute(hwnd,38,out var value,4)==0&&(material=="mica"?value==2:value is 1 or 3));
                OwnWindowCapture.Save(shell.Window,Path.Combine(files.Data,"native-"+material+".png"));
            }shell.Settings.Backdrop="mica";shell.ApplySettings(shell.Settings);
        });
        if(SelfTests.NetworkRequested)await test("021 REAL downloaded ALT13 plan validated without launching winws; installed components usable offline",async()=>{
            var settings=shell.Installer.ApplyManaged(files.Load());settings.Strategy="general (ALT13).bat";var plan=ZapretPreparation.Prepare(files,settings);Check(plan.Arguments.Count(x=>x=="--new")==8&&plan.CreateStartInfo().CreateNoWindow);
            string before=File.ReadAllText(shell.Installer.Manifest("zapret"));using var offline=new ModuleInstaller(files,new Handler(_=>throw new HttpRequestException("offline",new SocketException(10061))));await offline.Discover("zapret");Check(offline.UsingCache&&before==File.ReadAllText(shell.Installer.Manifest("zapret"))&&!new ZapretAdapter(files.ForZapret(settings)).Validate().Any(x=>x.Error)&&new TgExeAdapter(files).Validate(settings.TgExe)==null);
        });
    }
}








