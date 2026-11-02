using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NetworkHub.Core;
namespace NetworkHub.App;

// Direct Windows child with its own log handle. No resident managed log pump.
// Only the two explicit standard handles are inherited by the child.
public static class DetachedToolProcess
{
    [StructLayout(LayoutKind.Sequential)] struct Security {public int Size;public IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Startup {
        public int Size;public IntPtr Reserved,Desktop,Title;public uint X,Y,XSize,YSize,XChars,YChars,Fill,Flags;public ushort Show,ReservedSize;public IntPtr ReservedBytes,Input,Output,Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct Extended {public Startup Startup;public IntPtr Attributes;}
    [StructLayout(LayoutKind.Sequential)] struct Created {public IntPtr Process,Thread;public uint Pid,Tid;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileW(string name,uint access,uint share,ref Security security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
    [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessW(string executable,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref Extended startup,out Created created);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    public static string Quote(string value){
        var text=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}text.Append('\\',slashes).Append(c);slashes=0;}
        return text.Append('\\',slashes*2).Append('"').ToString();
    }
    public static Process Start(LaunchPlan plan)
    {
        _=plan.CreateStartInfo(); // Same direct-executable contract as the normal path.
        Paths.NoReparseAncestors(plan.LogFile);Directory.CreateDirectory(Path.GetDirectoryName(plan.LogFile)!);
        var security=new Security{Size=Marshal.SizeOf<Security>(),Inherit=true};
        using var log=CreateFileW(plan.LogFile,4,3,ref security,4,0x80,IntPtr.Zero);
        if(log.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        using var input=CreateFileW("NUL",0x80000000,3,ref security,3,0x80,IntPtr.Zero);
        if(input.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr bytes=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref bytes);
        IntPtr attributes=Marshal.AllocHGlobal(bytes),handles=Marshal.AllocHGlobal(IntPtr.Size*2);bool initialized=false;
        try{
            if(!InitializeProcThreadAttributeList(attributes,1,0,ref bytes))throw new Win32Exception(Marshal.GetLastWin32Error());initialized=true;
            Marshal.WriteIntPtr(handles,log.DangerousGetHandle());Marshal.WriteIntPtr(handles,IntPtr.Size,input.DangerousGetHandle());
            if(!UpdateProcThreadAttribute(attributes,0,new IntPtr(0x20002),handles,new IntPtr(IntPtr.Size*2),IntPtr.Zero,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup=new Extended{Startup=new Startup{Size=Marshal.SizeOf<Extended>(),Flags=0x100,Input=input.DangerousGetHandle(),Output=log.DangerousGetHandle(),Error=log.DangerousGetHandle()},Attributes=attributes};
            var command=new StringBuilder(string.Join(" ",new[]{plan.Executable}.Concat(plan.Arguments).Select(Quote)));
            if(!CreateProcessW(plan.Executable,command,IntPtr.Zero,IntPtr.Zero,true,0x08000000|0x00080000,IntPtr.Zero,plan.WorkingDirectory,ref startup,out var created))throw new Win32Exception(Marshal.GetLastWin32Error());
            try{var process=Process.GetProcessById((int)created.Pid);_=process.Handle;return process;}
            finally{CloseHandle(created.Thread);CloseHandle(created.Process);}
        }finally{if(initialized)DeleteProcThreadAttributeList(attributes);Marshal.FreeHGlobal(handles);Marshal.FreeHGlobal(attributes);GC.KeepAlive(log);GC.KeepAlive(input);}
    }
}
