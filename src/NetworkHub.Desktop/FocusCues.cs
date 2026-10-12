using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Documents;
using System.Windows.Controls;
namespace NetworkHub.Desktop;
// Explicit input mode avoids a stale WPF focus adorner after native dialogs or tray restore.
public static class FocusCues
{
    static readonly List<Window> windows=new();
    public static bool KeyboardMode {get;private set;}
    public static void SetMode(bool keyboard)
    {
        KeyboardMode=keyboard;
        foreach(var window in windows.ToArray())foreach(var item in LocalizedTree.Walk(window))
            if(item is Control control && item is not Window && item is not ScrollViewer){control.FocusVisualStyle=keyboard?(Style?)window.TryFindResource("KeyboardFocus"):null;if(!keyboard){var layer=AdornerLayer.GetAdornerLayer(control);var adorners=layer?.GetAdorners(control);if(adorners!=null)foreach(var adorner in adorners)if(adorner.GetType().Name=="FocusVisualAdorner")layer!.Remove(adorner);}}
    }
    public static void Attach(Window window)
    {
        windows.Add(window);
        window.Loaded+=(_,_)=>SetMode(KeyboardMode);
        window.PreviewMouseDown+=(_,_)=>SetMode(false);
        window.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Enter or Key.Space)SetMode(true);};
        HwndSource? source=null;
        IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message is 0x21 or 0xA1)SetMode(false);return IntPtr.Zero;}
        window.SourceInitialized+=(_,_)=>{source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source.AddHook(Hook);};
        window.Closed+=(_,_)=>{windows.Remove(window);source?.RemoveHook(Hook);};
    }
}

