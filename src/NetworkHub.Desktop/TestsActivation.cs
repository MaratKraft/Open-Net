using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace NetworkHub.Desktop;
public static class TestActivation
{
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool BringWindowToTop(IntPtr hwnd);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
 public static void Own(Window window){var hwnd=new WindowInteropHelper(window).Handle;uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out _),current=GetCurrentThreadId();bool attached=foreground!=0&&foreground!=current&&AttachThreadInput(current,foreground,true);try{window.Activate();BringWindowToTop(hwnd);SetForegroundWindow(hwnd);window.Focus();}finally{if(attached)AttachThreadInput(current,foreground,false);}}
}
