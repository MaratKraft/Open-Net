using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace NetworkHub.Desktop;
public static class OwnWindowCapture
{
    [StructLayout(LayoutKind.Sequential)] struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rectangle);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
    public static void Save(Window window,string path)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        if(!GetWindowRect(hwnd,out var rectangle))throw new InvalidOperationException("Own window rectangle unavailable");
        using var bitmap=new Bitmap(rectangle.Right-rectangle.Left,rectangle.Bottom-rectangle.Top);using var graphics=Graphics.FromImage(bitmap);
        var dc=graphics.GetHdc();try{if(!PrintWindow(hwnd,dc,2))throw new InvalidOperationException("Own PrintWindow failed");}finally{graphics.ReleaseHdc(dc);}bitmap.Save(path,ImageFormat.Png);
    }
}
