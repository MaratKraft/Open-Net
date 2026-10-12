using System;
using System.Drawing;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace NetworkHub.Desktop;
public static class AppIdentity
{
    public const string Id="OpenNet.Desktop";
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    public static void Apply(Action<string> log){int result=SetCurrentProcessExplicitAppUserModelID(Id);if(result!=0)log("AppUserModelID HRESULT="+result.ToString("X8"));}
}
public static class IconFactory
{
    [DllImport("user32.dll")]static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]static extern int GetSystemMetricsForDpi(int index,uint dpi);
    [DllImport("user32.dll")]static extern IntPtr CreateIconFromResourceEx(byte[] data,uint length,bool icon,uint version,int width,int height,uint flags);
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    public static Icon Create(int size=32)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenNet.Icon")!;
        using var memory=new MemoryStream();stream.CopyTo(memory);byte[] data=memory.ToArray();int count=BitConverter.ToUInt16(data,4);
        for(int i=0;i<count;i++){int entry=6+i*16,width=data[entry]==0?256:data[entry];if(width!=size)continue;int length=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12);var payload=data.AsSpan(offset,length).ToArray();IntPtr handle=CreateIconFromResourceEx(payload,(uint)payload.Length,true,0x30000,size,size,0);if(handle==IntPtr.Zero)throw new InvalidDataException("Icon frame could not be loaded");try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}}
        using var original=new Icon(new MemoryStream(data),size,size);return (Icon)original.Clone();
    }
    public static Icon TrayIcon(uint dpi)
    {
        int size;try{size=GetSystemMetricsForDpi(49,dpi);}catch(EntryPointNotFoundException){size=GetSystemMetrics(49);}
        return Create(Math.Max(16,size));
    }
    public static BitmapFrame WindowIcon()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenNet.Icon")!;
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        // Keep the ICO decoder attached so WPF can choose a matching frame for small/big native icons.
        var frame=decoder.Frames[0];frame.Freeze();return frame;
    }
    public static BitmapImage Logo()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenNet.Logo")!;
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
}
