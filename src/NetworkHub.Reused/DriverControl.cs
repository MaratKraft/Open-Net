using NetworkHub.App;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NetworkHub.Core;

namespace NetworkHub.App;
public static class DriverControl
{
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenSCManagerW(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenServiceW(IntPtr scm,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool CloseServiceHandle(IntPtr handle);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceConfigW(IntPtr handle,IntPtr buffer,uint size,out uint needed);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceStatus(IntPtr handle,out Status status);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool ControlService(IntPtr handle,uint control,out Status status);
    [StructLayout(LayoutKind.Sequential)] struct Status {public uint Type,State,Controls,Exit,SpecificExit,Checkpoint,WaitHint;}
    [StructLayout(LayoutKind.Sequential)] struct Config {public uint Type,Start,Error;public IntPtr Binary,Group;public uint Tag;public IntPtr Dependencies,Account,Display;}
    public static bool MatchesPath(string actual,string expected)
    {
        actual=actual.Trim().Trim('"');if(actual.StartsWith(@"\??\"))actual=actual.Substring(4);
        return string.Equals(Path.GetFullPath(actual),Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase);
    }
    static IntPtr Open(bool stop,out IntPtr scm)
    {
        scm=OpenSCManagerW(null,null,1);if(scm==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        var service=OpenServiceW(scm,"WinDivert",1|4|(stop?32u:0));
        if(service==IntPtr.Zero&&Marshal.GetLastWin32Error()!=1060){int error=Marshal.GetLastWin32Error();CloseServiceHandle(scm);throw new Win32Exception(error);}
        return service;
    }
    static bool Check(IntPtr service,string expected)
    {
        QueryServiceConfigW(service,IntPtr.Zero,0,out uint needed);
        if(needed==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer=Marshal.AllocHGlobal((int)needed);
        try
        {
            if(!QueryServiceConfigW(service,buffer,needed,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
            var config=Marshal.PtrToStructure<Config>(buffer);
            return config.Type==1&&MatchesPath(Marshal.PtrToStringUni(config.Binary)!,expected);
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }
    public static bool Loaded(string zapretDirectory)
    {
        var service=Open(false,out var scm);
        try
        {
            if(service==IntPtr.Zero)return false;
            if(!Check(service,Path.Combine(zapretDirectory,"bin","WinDivert64.sys")))return false;
            if(!QueryServiceStatus(service,out var status))throw new Win32Exception(Marshal.GetLastWin32Error());
            return status.State!=1;
        }
        finally{if(service!=IntPtr.Zero)CloseServiceHandle(service);CloseServiceHandle(scm);}
    }
    public static void Stop(string zapretDirectory,string? verifiedDriverHash=null)
    {
        if(ProcessObserver.Find("winws").Length>0)throw new InvalidOperationException(L.T("Another winws is running; the shared driver was left unchanged."));
        string expected=Path.Combine(zapretDirectory,"bin","WinDivert64.sys");
        if(Metadata.Hash(expected)!=(verifiedDriverHash??"8DA085332782708D8767BCACE5327A6EC7283C17CFB85E40B03CD2323A90DDC2"))throw new InvalidDataException(L.T("The driver file was modified. Reinstall the official release."));
        var service=Open(true,out var scm);
        try
        {
            if(service==IntPtr.Zero)return;
            if(!Check(service,expected))throw new InvalidOperationException(L.T("WinDivert belongs to another build; it was left unchanged."));
            if(!QueryServiceStatus(service,out var status))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(status.State==1)return;
            // Ordinary SCM stop only. No deletion, forced unload or dependent-service shutdown.
            if(status.State!=3&&!ControlService(service,1,out status)&&Marshal.GetLastWin32Error()!=1062)throw new Win32Exception(Marshal.GetLastWin32Error(),L.T("Windows did not stop WinDivert; it may be used by another application."));
            var timer=Stopwatch.StartNew();
            do
            {
                if(!QueryServiceStatus(service,out status))throw new Win32Exception(Marshal.GetLastWin32Error());
                if(status.State==1)return;
                System.Threading.Thread.Sleep(100);
            }while(timer.Elapsed<TimeSpan.FromSeconds(8));
            throw new TimeoutException(L.T("The process stopped but the WinDivert driver is still loaded."));
        }
        finally{if(service!=IntPtr.Zero)CloseServiceHandle(service);CloseServiceHandle(scm);}
    }
}



