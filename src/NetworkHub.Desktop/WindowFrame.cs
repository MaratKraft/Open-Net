using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace NetworkHub.Desktop;

// One client surface, with Windows handling caption dragging and native corners.
public static class WindowFrame
{
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr DefWindowProc(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
    public static void Extend(IntPtr hwnd)
    {
        var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};
        Marshal.ThrowExceptionForHR(DwmExtendFrameIntoClientArea(hwnd,ref margins));
    }
    public static void MaterialFrame(IntPtr hwnd,bool mica)
    {
        long before=GetWindowLongPtr(hwnd,-16).ToInt64();long after=before&~(0xC00000L|0x40000L);
        if(mica)after|=0x40000;
        if(after==before)return;
        SetWindowLongPtr(hwnd,-16,new IntPtr(after));
        SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x20|0x1|0x2|0x4|0x10);
    }
    public static void Attach(Window window)
    {
        var hwnd=new WindowInteropHelper(window).Handle;var source=HwndSource.FromHwnd(hwnd);
        IntPtr Hook(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)
        {
            // Remove the native caption layout, rather than painting over its gray band.
            if(message==0x83){handled=true;return IntPtr.Zero;}
            // Activation must update Windows' state without classic GDI painting a second border.
            if(message==0x86&&window.WindowState!=WindowState.Minimized){handled=true;return DefWindowProc(handle,message,w,new IntPtr(-1));}
            if(message==0x84)
            {
                long position=l.ToInt64();var point=window.PointFromScreen(new Point((short)(position&65535),(short)((position>>16)&65535)));
                if(window.ResizeMode==ResizeMode.CanResize&&point.X>=0&&point.X<window.ActualWidth&&(point.Y<6||point.Y>window.ActualHeight-6)){handled=true;return new IntPtr(point.Y<6?12:15);}
                if(point.X>=0&&point.X<window.ActualWidth&&point.Y>=0&&point.Y<62)
                {
                    var hit=window.InputHitTest(point) as DependencyObject;
                    for(var node=hit;node!=null&&node!=window;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
                        if(node is Button){handled=true;return new IntPtr(1);}
                    handled=true;return new IntPtr(2); // HTCAPTION: native move loop, including negative screen coordinates.
                }
                if(point.X>=0&&point.X<window.ActualWidth&&point.Y>=0&&point.Y<window.ActualHeight){handled=true;return new IntPtr(1);}
            }
            return IntPtr.Zero;
        }
        source.AddHook(Hook);
        SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x20|0x1|0x2|0x4|0x10);
        window.Closed+=(_,_)=>source.RemoveHook(Hook);
    }
}
