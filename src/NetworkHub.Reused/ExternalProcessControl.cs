using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NetworkHub.Core;

namespace NetworkHub.App;

// External control is deliberately restricted to the selected, verified installation.
public static class ExternalProcessControl
{
    // External process termination is narrower than the version-flexible installer:
    // do not let a user-writable manifest alone turn this into a generic killer.
    public static bool ReviewedBinary(string id,string hash)=>id switch
    {
        "zapret"=>string.Equals(hash,"AFFB4F69D2EA302A7ABCCD5325D81826E140DDAE014F1E070BC4A6C0DD555188",StringComparison.OrdinalIgnoreCase),
        _=>false
    };
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenSCManagerW(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool CloseServiceHandle(IntPtr handle);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool EnumServicesStatusExW(IntPtr manager,int info,uint type,uint state,IntPtr buffer,uint size,out uint needed,out uint returned,ref uint resume,string? group);
    [StructLayout(LayoutKind.Sequential)] struct Status {public uint Type,State,Controls,Exit,SpecificExit,Checkpoint,WaitHint,ProcessId,Flags;}
    [StructLayout(LayoutKind.Sequential)] struct Entry {public IntPtr Name,DisplayName;public Status Status;}

    public static bool IsServiceProcess(int pid)
    {
        var manager=OpenSCManagerW(null,null,4);if(manager==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        const int capacity=256*1024;var buffer=Marshal.AllocHGlobal(capacity);
        try
        {
            uint resume=0;
            if(!EnumServicesStatusExW(manager,0,0x30,3,buffer,capacity,out _,out uint count,ref resume,null))throw new Win32Exception(Marshal.GetLastWin32Error());
            int size=Marshal.SizeOf<Entry>();
            if(count>capacity/size)throw new InvalidDataException("Invalid service enumeration");
            for(int i=0;i<count;i++)if(Marshal.PtrToStructure<Entry>(IntPtr.Add(buffer,i*size)).Status.ProcessId==(uint)pid)return true;
            return false;
        }
        finally{Marshal.FreeHGlobal(buffer);CloseServiceHandle(manager);}
    }

    public static OwnedReceipt Select(string id,string expected,ProcessObservation[] observed,Func<int,Probe> read,Func<int,bool> isService)
    {
        if(id is not ("zapret" or "tg")||observed.Length!=1||string.IsNullOrWhiteSpace(expected))throw new InvalidOperationException(L.T("The external instance cannot be identified uniquely. No process was stopped."));
        var item=observed[0];var current=read(item.Pid);
        if(!item.MetadataAccessible||!current.Exists||!current.IdentityReadable||string.IsNullOrWhiteSpace(current.Executable)||!string.Equals(Path.GetFullPath(expected),current.Executable,StringComparison.OrdinalIgnoreCase)||!string.Equals(item.Path,current.Executable,StringComparison.OrdinalIgnoreCase)||!item.StartedUtc.HasValue||item.StartedUtc.Value.ToFileTimeUtc()!=current.StartFileTime)
            throw new InvalidOperationException(L.T("The external instance cannot be identified uniquely. No process was stopped."));
        if(isService(item.Pid))throw new InvalidOperationException(L.T("This instance is managed by a Windows service. Open Net did not stop it; use its own service controls."));
        return new OwnedReceipt(Guid.NewGuid().ToString("N"),item.Pid,current.StartFileTime,current.Executable,id);
    }
    public static OwnedReceipt SelectInstance(string id,string expected,ProcessObservation[] observed)
    {
        if(id!="tg")return Select(id,expected,observed,ProcessIdentity.Read,IsServiceProcess);
        // Validate every member before considering a common parent. Two roots
        // remain ambiguous, even if they execute identical files.
        var members=observed.Select(p=>Select(id,expected,new[]{p},ProcessIdentity.Read,IsServiceProcess)).ToArray();
        var roots=members.Where(p=>!observed.Any(child=>child.Pid==p.Pid&&members.Any(parent=>parent.Pid!=p.Pid&&ProcessObserver.IsDirectChild(child,parent.Pid,parent.StartFileTime)))).ToArray();
        if(roots.Length!=1||members.Any(p=>p.Pid!=roots[0].Pid&&!ProcessObserver.IsDirectChild(observed.Single(o=>o.Pid==p.Pid),roots[0].Pid,roots[0].StartFileTime)))throw new InvalidOperationException(L.T("The external instance cannot be identified uniquely. No process was stopped."));
        return roots[0];
    }
}
