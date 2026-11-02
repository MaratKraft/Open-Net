using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace NetworkHub.Desktop;
public static class WindowSizing
{
    [StructLayout(LayoutKind.Sequential)] struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    public static void Fit(Window window)
    {
        var hwnd=new WindowInteropHelper(window).Handle;var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info))return;var dpi=VisualTreeHelper.GetDpi(window);
        double width=Math.Max(240,(info.Work.Right-info.Work.Left)/dpi.DpiScaleX-24),height=Math.Max(300,(info.Work.Bottom-info.Work.Top)/dpi.DpiScaleY-24);
        window.MinWidth=Math.Min(window.MinWidth,width);window.MinHeight=Math.Min(window.MinHeight,height);
        window.Width=Math.Min(window.Width,width);window.Height=Math.Min(window.Height,height);
        if(window.IsLoaded){double left=info.Work.Left/dpi.DpiScaleX,top=info.Work.Top/dpi.DpiScaleY;window.Left=Math.Clamp(window.Left,left,Math.Max(left,info.Work.Right/dpi.DpiScaleX-window.Width));window.Top=Math.Clamp(window.Top,top,Math.Max(top,info.Work.Bottom/dpi.DpiScaleY-window.Height));}
    }
}
