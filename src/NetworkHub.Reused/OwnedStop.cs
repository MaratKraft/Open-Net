using NetworkHub.App;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace NetworkHub.App;
public static class OwnedStop
{
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr h,out long start,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h,uint flags,StringBuilder path,ref int size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateProcess(IntPtr h,uint code);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint timeout);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snapshot,ref Entry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool Process32NextW(IntPtr snapshot,ref Entry entry);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Entry
    {
        public uint Size,Usage,Pid; public UIntPtr Heap; public uint Module,Threads,Parent; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Name;
    }
    static (IntPtr Handle,long Start,string Path) Open(int pid)
    {
        var h=OpenProcess(0x1000|0x100000|1,false,pid);
        if(h==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var path=new StringBuilder(32768);int size=path.Capacity;
        if(!GetProcessTimes(h,out long start,out _,out _,out _)||!QueryFullProcessImageNameW(h,0,path,ref size))
        {CloseHandle(h);throw new InvalidOperationException(L.T("Could not verify the process for stopping. Check the diagnostic log."));}
        return(h,start,path.ToString());
    }
    public static void Stop(OwnedReceipt receipt,bool includeChildren,bool tryGraceful=false,OwnedReceipt[]? approvedMembers=null,Func<OwnedReceipt[]>? inspectMembers=null)
    {
        if(!ProcessIdentity.Read(receipt.Pid).Exists)return;
        var root=Open(receipt.Pid);var handles=new List<IntPtr>{root.Handle};
        try
        {
            if(root.Start!=receipt.StartFileTime||!string.Equals(root.Path,receipt.Executable,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L.T("Process ownership changed. Stop was cancelled."));
            if(includeChildren)
            {
                var snapshot=CreateToolhelp32Snapshot(2,0);
                if(snapshot==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
                var entries=new List<Entry>();
                try{var e=new Entry{Size=(uint)Marshal.SizeOf<Entry>(),Name=""};if(Process32FirstW(snapshot,ref e))do{entries.Add(e);}while(Process32NextW(snapshot,ref e));}
                finally{CloseHandle(snapshot);}
                var parents=new Dictionary<uint,long>{{(uint)receipt.Pid,root.Start}};
                bool added;
                do
                {
                    added=false;
                    foreach(var e in entries)
                    {
                        if(parents.ContainsKey(e.Pid)||!parents.TryGetValue(e.Parent,out long parentStart))continue;
                        // Hold identity-checked handles so a recycled PID cannot become a termination target.
                        if(!ProcessIdentity.Read((int)e.Pid).Exists)continue;
                        var child=Open((int)e.Pid);
                        if(child.Start<parentStart){CloseHandle(child.Handle);continue;}
                        if(tryGraceful&&!string.Equals(child.Path,receipt.Executable,StringComparison.OrdinalIgnoreCase)){CloseHandle(child.Handle);throw new InvalidOperationException(L.T("The external instance cannot be identified uniquely. No process was stopped."));}
                        handles.Add(child.Handle);parents[e.Pid]=child.Start;added=true;
                    }
                }while(added);
            }
            if(approvedMembers!=null)
            {
                var current=inspectMembers?.Invoke()??TgRuntimeControl.Inspect(receipt.Executable).Members;
                if(!TgRuntimeControl.SameMembers(approvedMembers,current)||handles.Count!=approvedMembers.Length)throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
            }
            // Close a visible TG window first. Tray-only applications may ignore WM_CLOSE.
            if(includeChildren||tryGraceful)
            {
                using var p=Process.GetProcessById(receipt.Pid);
                if(p.CloseMainWindow())WaitForSingleObject(root.Handle,1500);
            }
            if(approvedMembers!=null)
            {
                var remaining=inspectMembers?.Invoke()??(NetworkHub.Core.ProcessObserver.Find("TgWsProxy_windows").Length==0?Array.Empty<OwnedReceipt>():TgRuntimeControl.Inspect(receipt.Executable).Members);
                if(remaining.Any(p=>!approvedMembers.Any(a=>a.Pid==p.Pid&&a.StartFileTime==p.StartFileTime&&string.Equals(a.Executable,p.Executable,StringComparison.OrdinalIgnoreCase))))throw new InvalidOperationException(L.T("The external instance changed. No process was stopped."));
            }
            for(int i=handles.Count-1;i>=0;i--)
            {
                if(WaitForSingleObject(handles[i],0)==0)continue;
                if(!TerminateProcess(handles[i],0)&&WaitForSingleObject(handles[i],0)!=0)throw new Win32Exception(Marshal.GetLastWin32Error());
                if(WaitForSingleObject(handles[i],5000)!=0)throw new TimeoutException(L.T("The process did not confirm termination. Check the diagnostic log."));
            }
        }
        finally{foreach(var h in handles)CloseHandle(h);}
    }
}



