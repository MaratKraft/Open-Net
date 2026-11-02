using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace NetworkHub.Desktop;

// Retain live windows and their existing controls; never recreate a closed panel.
internal sealed class OwnedWindowRestorer
{
    readonly Window main;readonly List<Window> saved=new();Window? active;bool restoring;
    internal OwnedWindowRestorer(Window main)
    {
        this.main=main;main.SourceInitialized+=(_,_)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(main).Handle);IntPtr Hook(IntPtr h,int m,IntPtr w,IntPtr l,ref bool handled){if(m==0x112&&(w.ToInt64()&0xfff0)==0xf020)Capture();return IntPtr.Zero;}source.AddHook(Hook);main.Closed+=(_,_)=>source.RemoveHook(Hook);};
        main.StateChanged+=(_,_)=>{if(main.WindowState==WindowState.Minimized)Capture();else if(main.WindowState==WindowState.Normal&&main.IsVisible)Restore();};
    }
    IEnumerable<Window> Children(Window window){foreach(Window child in window.OwnedWindows){yield return child;foreach(var nested in Children(child))yield return nested;}}
    internal void Capture(){if(restoring||saved.Count>0)return;foreach(var child in Children(main).Where(w=>w.IsVisible)){saved.Add(child);if(child.IsActive)active=child;child.Closed+=Closed;}}
    void Closed(object? sender,EventArgs e){if(sender is Window child){saved.Remove(child);if(active==child)active=null;child.Closed-=Closed;}}
    internal void Hide(){Capture();foreach(var child in saved.ToArray())child.Hide();}
    internal void Restore()
    {
        if(restoring||saved.Count==0)return;restoring=true;main.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{try{foreach(var child in saved.ToArray()){child.Show();if(child.WindowState==WindowState.Minimized)child.WindowState=WindowState.Normal;child.Closed-=Closed;}var last=active??saved.LastOrDefault();saved.Clear();active=null;last?.Activate();}finally{restoring=false;}}));
    }
}
