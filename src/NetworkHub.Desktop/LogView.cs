using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class LogView
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public static Window Create(Window owner,string content)
    {
        var view=new Window{Title=L.T("Log — Open Net"),Width=700,Height=460,MinWidth=400,MinHeight=240,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(25,25,25)),Foreground=Brushes.White,Icon=owner.Icon};view.NameScopeSetup();FocusCues.Attach(view);
        view.SourceInitialized+=(_,_)=>{int build=Environment.OSVersion.Version.Build;if(build<17763)return;var hwnd=new WindowInteropHelper(view).Handle;int dark=1;DwmSetWindowAttribute(hwnd,build<18985?19:20,ref dark,4);if(build>=22000){int caption=0x191919,text=0xF3F3F3,border=0x333333;DwmSetWindowAttribute(hwnd,35,ref caption,4);DwmSetWindowAttribute(hwnd,36,ref text,4);DwmSetWindowAttribute(hwnd,34,ref border,4);}};
        var field=new TextBox{Style=new Style(typeof(TextBox)),Text=content,IsReadOnly=true,TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(12),Padding=new Thickness(12),FontFamily=new FontFamily("Consolas"),FontSize=12,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(30,30,30)),BorderBrush=new SolidColorBrush(Color.FromRgb(51,51,51)),SelectionBrush=new SolidColorBrush(Color.FromRgb(71,107,136)),SelectionOpacity=0.8,CaretBrush=Brushes.White};view.RegisterName("LogText",field);
        field.Resources[SystemColors.ControlBrushKey]=new SolidColorBrush(Color.FromRgb(30,30,30));
        view.Content=field;return view;
    }
    public static async Task<Window> Open(Window owner,AppFiles files)
    {
        var view=Create(owner,L.T("Loading log…"));view.Show();
        try{string path=Paths.Owned(files.Data,"Logs/hub.log");string content=await Task.Run(()=>File.Exists(path)?LogSafety.Tail(path,256*1024):L.T("The log is empty."));if(view.IsVisible)((TextBox)view.FindName("LogText")).Text=content;}
        catch(Exception ex){if(view.IsVisible)((TextBox)view.FindName("LogText")).Text=L.Error(ex);files.Log("Log viewer: "+ex);}
        return view;
    }
}

