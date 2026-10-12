using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace NetworkHub.Desktop;
public static class SecondaryAppearance
{
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 public static void Attach(Window window){ThemeManager.Attach(window);void Update(){int build=Environment.OSVersion.Version.Build;if(build<17763)return;var hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;int dark=ThemeManager.IsDark?1:0;DwmSetWindowAttribute(hwnd,build<18985?19:20,ref dark,4);if(build>=22000){int ColorValue(string key){var c=((SolidColorBrush)ThemeManager.Brush(key)).Color;return c.R|(c.G<<8)|(c.B<<16);}int caption=ColorValue("Surface"),text=ColorValue("Text"),border=ColorValue("Line"),corner=2;DwmSetWindowAttribute(hwnd,35,ref caption,4);DwmSetWindowAttribute(hwnd,36,ref text,4);DwmSetWindowAttribute(hwnd,34,ref border,4);DwmSetWindowAttribute(hwnd,33,ref corner,4);}}
  window.SourceInitialized+=(_,_)=>Update();ThemeManager.Changed+=Update;window.Closed+=(_,_)=>ThemeManager.Changed-=Update;
 }
}
