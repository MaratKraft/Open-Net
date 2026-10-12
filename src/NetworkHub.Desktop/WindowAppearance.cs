using System.Linq;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using NetworkHub.App;
namespace NetworkHub.Desktop;
public static class WindowAppearance
{
    static readonly System.Collections.Generic.Dictionary<Window,Action> updates=new();
    public static void Refresh(Window window){if(updates.TryGetValue(window,out var update))update();}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetWindowCompositionAttribute(IntPtr hwnd,ref CompositionData data);
    [StructLayout(LayoutKind.Sequential)] struct Accent {public int State,Flags;public uint Tint;public int Animation;}
    [StructLayout(LayoutKind.Sequential)] struct CompositionData {public int Attribute;public IntPtr Data;public UIntPtr Size;}
    static bool AccentBackdrop(IntPtr hwnd,bool enabled)
    {
        var policy=new Accent{State=enabled?4:0,Flags=2,Tint=ThemeManager.IsDark?0xB8181818u:0xB8EDEDEDu};
        int size=Marshal.SizeOf<Accent>();var buffer=Marshal.AllocHGlobal(size);
        try{Marshal.StructureToPtr(policy,buffer,false);var data=new CompositionData{Attribute=19,Data=buffer,Size=(UIntPtr)size};return SetWindowCompositionAttribute(hwnd,ref data);}
        catch(EntryPointNotFoundException){return false;}
        finally{Marshal.FreeHGlobal(buffer);}
    }
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hwnd,IntPtr rect,IntPtr region,uint flags);
    [StructLayout(LayoutKind.Sequential)] struct PowerStatus{public byte ACLineStatus,BatteryFlag,BatteryLifePercent,SystemStatusFlag;public uint BatteryLifeTime,BatteryFullLifeTime;}
    public static string Select(int build,bool transparency,bool highContrast,bool saver,string requested)=>
        requested!="solid"&&build>=22621&&transparency&&!highContrast&&!saver?(requested=="acrylic"?"acrylic":"mica"):"solid";
    static bool Transparency()
    {
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") is not int enabled||enabled!=0;
    }
    public static void Apply(Window window,AppFiles files,Func<string>? requested=null)
    {
        var hwnd=new WindowInteropHelper(window).Handle;var surface=(Border)window.FindName("WindowSurface");
        var original=LocalizedTree.Walk(window).OfType<Control>().Select(control=>(Control:control,Style:control.Style,Foreground:control.Foreground,Background:control.Background)).ToArray();
        var texts=LocalizedTree.Walk(window).OfType<TextBlock>().Select(text=>(Text:text,Foreground:text.Foreground)).ToArray();
        bool contrastApplied=false;
        string lastMode="";
        void Update()
        {
            try
            {
                ThemeManager.Refresh();
                int build=Environment.OSVersion.Version.Build;
                bool saver=GetSystemPowerStatus(out var power)&&power.SystemStatusFlag==1;
                string mode=Select(build,Transparency(),SystemParameters.HighContrast,saver,requested?.Invoke()??"mica");
                string signature=mode+"|"+(ThemeManager.IsDark?"dark":"light");
                bool transition=signature!=lastMode;
                // Detach desktop Acrylic before changing the HWND frame. The reverse order can
                // leave a stale accent surface which only disappears after a hide/show cycle.
                if(build>=22621&&transition){AccentBackdrop(hwnd,false);int clear=1;DwmSetWindowAttribute(hwnd,38,ref clear,4);}
                WindowFrame.MaterialFrame(hwnd,mode=="mica");
                HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor=Colors.Transparent;
                WindowFrame.Extend(hwnd);
                int rendering=0;DwmSetWindowAttribute(hwnd,2,ref rendering,4);
                if(build>=22000)
                {
                    int dark=ThemeManager.IsDark?1:0,corner=2,none=unchecked((int)0xFFFFFFFE);
                    DwmSetWindowAttribute(hwnd,20,ref dark,4);DwmSetWindowAttribute(hwnd,33,ref corner,4);DwmSetWindowAttribute(hwnd,34,ref none,4);
                    // Windows 11 accepts the no-color sentinel for a fully custom caption too.
                    // This value is only documented for borders; keep the compatibility limitation explicit.
                    int captionResult=DwmSetWindowAttribute(hwnd,35,ref none,4);
                    if(captionResult!=0)files.Log("Custom caption color unavailable: HRESULT "+captionResult.ToString("X8"));
                }
                bool applied=false;
                string mechanism="DWM";
                if(build>=22621)
                {
                    int backdrop=mode=="mica"?2:1;
                    applied=DwmSetWindowAttribute(hwnd,38,ref backdrop,4)==0&&mode=="mica";
                    if(mode=="acrylic")
                    {
                        applied=AccentBackdrop(hwnd,true);mechanism="desktop accent B8181818";
                        if(!applied){backdrop=3;applied=DwmSetWindowAttribute(hwnd,38,ref backdrop,4)==0;mechanism="DWM acrylic with dark tint";}
                    }
                }
                surface.Background=applied?Brushes.Transparent:SystemParameters.HighContrast?SystemColors.WindowBrush:ThemeManager.Brush("Surface");
                if(applied&&mechanism=="DWM acrylic with dark tint")surface.Background=new SolidColorBrush(ThemeManager.IsDark?Color.FromArgb(128,24,24,24):Color.FromArgb(128,237,237,237));
                window.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:ThemeManager.Brush("Text");
                if(SystemParameters.HighContrast){foreach(var value in texts)value.Text.Foreground=SystemColors.WindowTextBrush;foreach(var value in original){value.Control.Style=new Style(value.Control.GetType());value.Control.Foreground=SystemColors.WindowTextBrush;value.Control.Background=SystemColors.WindowBrush;}contrastApplied=true;}
                else if(contrastApplied){foreach(var value in texts)value.Text.Foreground=value.Foreground;foreach(var value in original){value.Control.Style=value.Style;value.Control.Foreground=value.Foreground;value.Control.Background=value.Background;}contrastApplied=false;}
                if(transition)
                {
                    files.Log("Window backdrop transition: "+lastMode+" -> "+(applied?mode:"solid fallback")+"; "+mechanism+"; build="+build+"; highContrast="+SystemParameters.HighContrast+"; saver="+saver);
                    lastMode=signature;
                    surface.InvalidateVisual();window.InvalidateVisual();RedrawWindow(hwnd,IntPtr.Zero,IntPtr.Zero,0x1|0x80|0x100|0x400);
                    // Reapply on the next render after WPF has committed the transparent surface.
                    // The HWND and visibility are preserved; this is not a hide/show workaround.
                    if(applied&&mode=="mica")window.Dispatcher.BeginInvoke(new Action(()=>{
                        if(!updates.ContainsKey(window)||!lastMode.StartsWith("mica|"))return;
                        HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor=Colors.Transparent;WindowFrame.Extend(hwnd);int mica=2;int result=DwmSetWindowAttribute(hwnd,38,ref mica,4);
                        RedrawWindow(hwnd,IntPtr.Zero,IntPtr.Zero,0x1|0x80|0x100|0x400);files.Log("Mica render reapply HRESULT="+result.ToString("X8"));
                    }),System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch(Exception ex)when(ex is DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException or System.Security.SecurityException)
            {surface.Background=ThemeManager.Brush("Surface");files.Log("Backdrop fallback: "+ex);}
        }
        updates[window]=Update;Update();
        bool pending=false;
        void Schedule(){if(pending)return;pending=true;window.Dispatcher.BeginInvoke(new Action(()=>{pending=false;if(updates.ContainsKey(window))Update();}),System.Windows.Threading.DispatcherPriority.Background);}
        window.SizeChanged+=(_,_)=>Schedule();window.DpiChanged+=(_,_)=>Schedule();window.ContentRendered+=(_,_)=>Schedule();
        if(Environment.OSVersion.Version.Build<22000)
        {
            void Region(){var dpi=VisualTreeHelper.GetDpi(window);int width=(int)Math.Round(window.ActualWidth*dpi.DpiScaleX),height=(int)Math.Round(window.ActualHeight*dpi.DpiScaleY);if(width<=0||height<=0)return;var region=CreateRoundRectRgn(0,0,width+1,height+1,(int)(32*dpi.DpiScaleX),(int)(32*dpi.DpiScaleY));if(region!=IntPtr.Zero&&SetWindowRgn(hwnd,region,true)==0)DeleteObject(region);}
            window.ContentRendered+=(_,_)=>Region();window.SizeChanged+=(_,_)=>Region();window.DpiChanged+=(_,_)=>Region();
        }
        var source=HwndSource.FromHwnd(hwnd);
        IntPtr Hook(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x1A||message==0x218||message==0x31A)Update();return IntPtr.Zero;}
        source.AddHook(Hook);window.Closed+=(_,_)=>{updates.Remove(window);source.RemoveHook(Hook);};
    }
}




