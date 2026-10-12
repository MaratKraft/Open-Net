using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class Tests022
{
    [StructLayout(LayoutKind.Sequential)] struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out Rect r);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect r);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    static void Check(bool value,string detail){if(!value)throw new Exception(detail);}
    static async Task Wait(Shell shell){for(int n=0;shell.Busy&&n<250;n++)await Task.Delay(60);Check(!shell.Busy,"Operation did not finish");await shell.Refresh();}
    static void Screen(Window window,string path)
    {
        TestActivation.Own(window);
        var hwnd=new WindowInteropHelper(window).Handle;
        Check(GetForegroundWindow()==hwnd,"Only the foreground test window may be captured");
        Check(GetWindowRect(hwnd,out var r),"Window rectangle");
        using var bitmap=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);
        using var graphics=System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(r.Left,r.Top,0,0,bitmap.Size);
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
    public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("022 external processes never offer Stop; mixed and cleanup states choose actionable operation",()=>{
            var external=new ToolObservation("tg",ObservedState.External,"external",123);
            Check(!PrimaryAction.Stop(new[]{external},new[]{"tg"})&&PrimaryAction.ExternalOnly(new[]{external},new[]{"tg"}),"External only");
            Check(!PrimaryAction.Stop(new[]{external,new ToolObservation("zapret",ObservedState.Stopped,"ready")},new[]{"tg","zapret"}),"Mixed external/stopped must start");
            Check(PrimaryAction.Stop(new[]{external,new ToolObservation("zapret",ObservedState.Running,"owned")},new[]{"zapret","tg"}),"Owned running");
            Check(PrimaryAction.Stop(new[]{new ToolObservation("zapret",ObservedState.CleanupRequired,"driver")},new[]{"zapret"}),"Cleanup actionable");
            Check(!PrimaryAction.Stop(new[]{external},Array.Empty<string>()),"Empty selection");return Task.CompletedTask;
        });
        await test("022 main Stop button really ends selected process, preserves unselected process, resets UI and permits restart",async()=>{
            shell.Find<CheckBox>("ZapretCheck").IsChecked=true;shell.Find<CheckBox>("TgCheck").IsChecked=true;
            await shell.StartSelected();await shell.Refresh();var running=shell.Observations.ToArray();Check(running.All(x=>x.State==ObservedState.Running),"Own stubs running");
            shell.Find<CheckBox>("ZapretCheck").IsChecked=false;
            shell.Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);await Wait(shell);
            Check(!ProcessIdentity.Read(running.Single(x=>x.Id=="tg").Pid!.Value).Exists,"TG stub must actually exit");
            Check(ProcessIdentity.Read(running.Single(x=>x.Id=="zapret").Pid!.Value).Exists,"Unselected stub preserved");
            Check(shell.Primary.Content.ToString()==L.T("Start selected")&&shell.Primary.IsEnabled,"Button refreshed after stop");
            shell.Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);await Wait(shell);Check(shell.Observations.All(x=>x.State==ObservedState.Running),"Restart works");
            shell.Find<CheckBox>("ZapretCheck").IsChecked=true;shell.Primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);await Wait(shell);
            Check(shell.Observations.All(x=>x.State==ObservedState.Stopped)&&!shell.TrayStop.Enabled,"Both stopped and tray Stop disabled");
            await shell.StopSelected(false);Check(shell.Find<TextBlock>("Message").Text==L.T("The process is not running."),"Repeated stop feedback");
            shell.Find<CheckBox>("ZapretCheck").IsChecked=false;shell.Find<CheckBox>("TgCheck").IsChecked=false;await shell.StopSelected(false);Check(!shell.Primary.IsEnabled,"Empty selection disabled");
            shell.Find<CheckBox>("ZapretCheck").IsChecked=true;shell.Find<CheckBox>("TgCheck").IsChecked=true;
        });
        await test("022 refresh callers await same observation rather than publishing stale state after Stop",async()=>{
            var first=shell.Refresh();var second=shell.Refresh();Check(ReferenceEquals(first,second),"Concurrent refresh shared");await Task.WhenAll(first,second);
        });
        await test("022 actual external-only UI disables Stop and displays ownership guidance; mixed selection offers Start",async()=>{
            var property=typeof(Shell).GetProperty(nameof(Shell.Observations))!;var render=typeof(Shell).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic)!;var saved=shell.Observations;
            try{
                property.SetValue(shell,new[]{new ToolObservation("zapret",ObservedState.Stopped,"ready"),new ToolObservation("tg",ObservedState.External,"external",123)});
                shell.Find<CheckBox>("ZapretCheck").IsChecked=false;render.Invoke(shell,null);
                Check(!shell.Primary.IsEnabled&&shell.Primary.Content.ToString()==L.T("External instance")&&!shell.TrayStop.Enabled,"External process cannot offer a no-op Stop");
                Check(shell.Primary.ToolTip?.ToString()==L.T("Close external tools using their own window or tray. Open Net manages only processes it launched."),"Explicit ownership guidance");
                shell.Find<CheckBox>("ZapretCheck").IsChecked=true;render.Invoke(shell,null);Check(shell.Primary.IsEnabled&&shell.Primary.Content.ToString()==L.T("Start selected"),"Mixed external/stopped offers Start");
            }finally{property.SetValue(shell,saved);await shell.Refresh();}
        });
        await test("022 full client frame, native caption drag hit tests, clickable buttons, aligned header, minimize/restore",async()=>{
            shell.Restore();shell.Window.UpdateLayout();var hwnd=new WindowInteropHelper(shell.Window).Handle;
            GetWindowRect(hwnd,out var wr);GetClientRect(hwnd,out var cr);
            files.Log("Frame style="+GetWindowLongPtr(hwnd,-16).ToInt64().ToString("X")+" client="+(cr.Right-cr.Left)+"x"+(cr.Bottom-cr.Top)+" window="+(wr.Right-wr.Left)+"x"+(wr.Bottom-wr.Top));
            Check(cr.Bottom-cr.Top==wr.Bottom-wr.Top,"No reserved native caption height");
            int Hit(FrameworkElement element){var screen=element.PointToScreen(new Point(element.ActualWidth/2,element.ActualHeight/2));long packed=((long)(ushort)(short)screen.Y<<16)|(ushort)(short)screen.X;return (int)SendMessage(hwnd,0x84,IntPtr.Zero,new IntPtr(packed));}
            Check(Hit(shell.Find<Image>("Logo"))==2,"Header remains HTCAPTION");Check(Hit(shell.Find<Button>("Minimize"))==1&&Hit(shell.Find<Button>("Close"))==1,"Buttons remain HTCLIENT");
            double Center(string name){var e=shell.Find<FrameworkElement>(name);return e.TranslatePoint(new Point(0,e.ActualHeight/2),shell.Window).Y;}
            Check(new[]{"Logo","AppName","Minimize","Close"}.Select(Center).Max()-new[]{"Logo","AppName","Minimize","Close"}.Select(Center).Min()<0.1,"Header centers aligned");
            shell.Find<Button>("Minimize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(shell.Window.WindowState==WindowState.Minimized,"Minimize works");shell.Restore();await Task.Delay(100);
        });
        await test("022 compositor screenshots of Mica and dark Acrylic above only our colored test window",async()=>{
            string language=shell.Settings.Language;shell.Settings.Language="ru";shell.ApplySettings(shell.Settings);
            var backdrop=new Window{Title="Open Net owned material test",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Left=shell.Window.Left-30,Top=shell.Window.Top-30,Width=shell.Window.ActualWidth+60,Height=shell.Window.ActualHeight+60};
            var gradient=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
            gradient.GradientStops.Add(new GradientStop(Colors.Red,0));gradient.GradientStops.Add(new GradientStop(Colors.Red,0.5));gradient.GradientStops.Add(new GradientStop(Colors.Blue,0.5));gradient.GradientStops.Add(new GradientStop(Colors.Blue,1));
            backdrop.Background=gradient;backdrop.Show();
            try{
                foreach(string material in new[]{"mica","acrylic"}){
                    shell.Settings.Backdrop=material;shell.ApplySettings(shell.Settings);shell.Restore();TestActivation.Own(shell.Window);await Task.Delay(600);shell.Window.UpdateLayout();Screen(shell.Window,Path.Combine(files.Data,"screen-"+material+".png"));
                    double width=shell.Window.Width,height=shell.Window.Height;shell.Window.Width=width+12;shell.Window.Height=height+12;await Task.Delay(150);shell.Window.Width=width;shell.Window.Height=height;await Task.Delay(150);
                    Screen(shell.Window,Path.Combine(files.Data,"screen-"+material+"-resized.png"));
                }
                backdrop.Background=new SolidColorBrush(Colors.Green);backdrop.UpdateLayout();await Task.Delay(250);shell.Settings.Backdrop="acrylic";shell.ApplySettings(shell.Settings);shell.Restore();TestActivation.Own(shell.Window);await Task.Delay(600);Screen(shell.Window,Path.Combine(files.Data,"screen-acrylic-green.png"));
                if(Environment.OSVersion.Version.Build>=22621)
                {
                    using var mica=new System.Drawing.Bitmap(Path.Combine(files.Data,"screen-mica.png"));using var acrylic=new System.Drawing.Bitmap(Path.Combine(files.Data,"screen-acrylic.png"));using var green=new System.Drawing.Bitmap(Path.Combine(files.Data,"screen-acrylic-green.png"));
                    var top=mica.GetPixel(200,10);var body=mica.GetPixel(200,70);
                    for(int y=6;y<45;y++){var p=mica.GetPixel(200,y-1);var q=mica.GetPixel(200,y);Check(Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B)<6,"Mica must have no abrupt native title band boundary");}
                    var a=acrylic.GetPixel(150,70);var g=green.GetPixel(150,70);
                    Check((a.R+a.G+a.B)/3.0<50,"Acrylic must remain dark");
                    Check(Math.Abs(a.R-g.R)+Math.Abs(a.G-g.G)+Math.Abs(a.B-g.B)>15,"Acrylic must sample the real window behind it");
                    var middle=acrylic.GetPixel(acrylic.Width/2,70);Check(middle.R>5&&middle.B>5,"Sharp red/blue background edge must be blurred into mixed colors");
                    File.WriteAllText(Path.Combine(files.Data,"material-pixels.json"),System.Text.Json.JsonSerializer.Serialize(new{MicaTop=top.ToArgb(),MicaBody=body.ToArgb(),AcrylicRed=a.ToArgb(),AcrylicGreen=g.ToArgb(),BlurredEdge=middle.ToArgb()}));
                }
            }finally{backdrop.Close();shell.Settings.Language=language;shell.Settings.Backdrop="mica";shell.ApplySettings(shell.Settings);}
        });
    }
}


