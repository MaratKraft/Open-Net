using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace NetworkHub.Core;
public sealed record OwnedIdentity(Guid Token, int Pid, DateTime StartedUtc, string Executable, string PayloadDigest);
public sealed record StopResult(bool Forced, bool TreeExited, int? ExitCode);
// Test-only capability: this class cannot launch a network plan. Only the project's compiled stub is accepted.
// Stub waits for a gate before doing any work/creating children; job assignment precedes gate opening.
public sealed class StubSupervisor : IDisposable
{
    private readonly string host, stub, digest, root;
    private readonly bool portable;
    private readonly Dictionary<Guid, Entry> entries = new();
    private readonly Dictionary<Guid, ModuleState> states = new();
    public ModuleState State(Guid token)
    {
        if (!states.TryGetValue(token, out var state)) throw new InvalidOperationException("Unknown ownership token");
        if (entries.TryGetValue(token, out var e) && e.Process.HasExited)
            return e.Process.ExitCode == 0 ? ModuleState.Stopped : ModuleState.Failed;
        return state;
    }
    private sealed class Entry
    {
        public required Process Process; public required IntPtr Job; public required string Stop;
        public required StreamWriter Log; public required OwnedIdentity Identity;
    }
    public StubSupervisor(string dotnetHost, string stubDll, string expectedStubDigest, string workspace,bool portable=false)
    {
        this.portable=portable;
        host=Path.GetFullPath(dotnetHost); stub=Path.GetFullPath(stubDll); digest=expectedStubDigest; root=Path.GetFullPath(workspace);
        if(portable&&!string.Equals(host,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Portable stub must use this exact Open Net executable.");
        if ((!portable&&Path.GetFileName(host).Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase)==false) || !File.Exists(host) || !Metadata.X64Pe(host)
            || (!portable&&Path.GetFileName(stub)!="NetworkHub.Stub.dll") || (portable&&host!=stub) || Metadata.Hash(stub)!=digest)
            throw new InvalidDataException("Only explicit locally built dotnet stub capability is accepted");
        Paths.NoReparseAncestors(root);
    }
    public OwnedIdentity Start(bool hang=false, bool child=false, int exitCode=0)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!File.Exists(host) || !Metadata.X64Pe(host)) throw new InvalidDataException("Invalid stub host; no launch attempted");
        if (Metadata.Hash(stub)!=digest) throw new InvalidDataException("Stub changed since registration");
        Guid token=Guid.NewGuid(); string folder=Paths.Owned(root,token.ToString("N")); Directory.CreateDirectory(folder);
        string gate=Path.Combine(folder,"ready.gate"), stop=Path.Combine(folder,"stop.request");
        states[token]=ModuleState.Starting;
        IntPtr job=Jobs.Create(); Process? p=null; StreamWriter? log=null;
        try
        {
            log=new StreamWriter(Path.Combine(folder,"stdout.log")) { AutoFlush=true };
            var psi=new ProcessStartInfo(host) { UseShellExecute=false, WorkingDirectory=folder, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
            foreach (string a in new[] {portable?"--safe-stub":stub,"--gate",gate,"--stop",stop,"--exit-code",exitCode.ToString()}) psi.ArgumentList.Add(a);
            if(hang) psi.ArgumentList.Add("--hang"); if(child) psi.ArgumentList.Add("--child");
            p=Process.Start(psi) ?? throw new InvalidOperationException("Stub launch failed");
            var capturedLog=log;
            p.OutputDataReceived+=(_,e)=> { if(e.Data!=null) lock(capturedLog) capturedLog.WriteLine(LogSafety.Redact(e.Data)); };
            p.ErrorDataReceived+=(_,e)=> { if(e.Data!=null) lock(capturedLog) capturedLog.WriteLine(LogSafety.Redact(e.Data)); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if(!Jobs.AssignProcessToJobObject(job,p.Handle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Job assignment failed; gate remains closed");
            var identity=new OwnedIdentity(token,p.Id,p.StartTime.ToUniversalTime(),host,digest);
            entries.Add(token,new Entry { Process=p,Job=job,Stop=stop,Log=log,Identity=identity });
            Paths.WriteNew(gate,"ready"); states[token]=ModuleState.Running; return identity;
        }
        catch(Exception launchError)
        {
            Exception? cleanupError=null;
            if(p!=null) { try { if(!p.HasExited) p.Kill(); p.WaitForExit(2000); } catch(Exception ex){cleanupError=ex;}p.Dispose(); }
            Jobs.CloseHandle(job);log?.Dispose();entries.Remove(token);states[token]=ModuleState.Failed;
            if(cleanupError!=null)throw new AggregateException("Own stub launch and cleanup failed",launchError,cleanupError);
            throw;
        }
    }
    public int[] Tree(Guid token) => Jobs.Pids(Get(token).Job);
    private Entry Get(Guid token) => entries.TryGetValue(token,out var e) ? e : throw new InvalidOperationException("Unknown ownership token; foreign processes cannot be stopped");
    public StopResult Stop(Guid token, int gracefulMs=1500)
    {
        var e=Get(token); if(gracefulMs<0||gracefulMs>10000) throw new ArgumentOutOfRangeException(nameof(gracefulMs));
        states[token]=ModuleState.Stopping;
        if(!File.Exists(e.Stop)) Paths.WriteNew(e.Stop,"stop");
        var timer=Stopwatch.StartNew(); while(Jobs.Pids(e.Job).Length>0 && timer.ElapsedMilliseconds<gracefulMs) Thread.Sleep(25);
        bool forced=Jobs.Pids(e.Job).Length>0;
        if(forced && !Jobs.TerminateJobObject(e.Job,137)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        timer.Restart(); while(Jobs.Pids(e.Job).Length>0 && timer.ElapsedMilliseconds<3000) Thread.Sleep(25);
        bool exited=Jobs.Pids(e.Job).Length==0;
        int? code=null; if(e.Process.WaitForExit(3000)) { e.Process.WaitForExit(); code=e.Process.ExitCode; }
        if(!exited) throw new InvalidOperationException("Owned job failed to exit");
        entries.Remove(token); Jobs.CloseHandle(e.Job); e.Process.Dispose(); lock(e.Log) e.Log.Dispose();
        states[token]=code==0 ? ModuleState.Stopped : ModuleState.Failed;
        return new(forced,exited,code);
    }
    public OwnedIdentity Restart(Guid token, bool hang=false, bool child=false) { Stop(token); return Start(hang,child); }
    public void Dispose()
    {
        var errors = new List<Exception>();
        foreach(var token in new List<Guid>(entries.Keys))
        {
            try { Stop(token,0); }
            catch (Exception ex) { errors.Add(ex); }
            finally
            {
                if(entries.Remove(token,out var e))
                {
                    Jobs.CloseHandle(e.Job); // KILL_ON_JOB_CLOSE also protects cleanup failures.
                    try { if(e.Process.WaitForExit(3000)) e.Process.WaitForExit(); }
                    catch(Exception ex) { errors.Add(ex); }
                    finally { e.Process.Dispose(); }
                    lock(e.Log) e.Log.Dispose();
                }
            }
        }
        if(errors.Count>0) throw new AggregateException("Own jobs closed after cleanup errors",errors);
    }
    private static class Jobs
    {
        [StructLayout(LayoutKind.Sequential)] private struct Limits
        {
            public long ProcessTime,JobTime; public uint Flags; public UIntPtr Minimum,Maximum; public uint ActiveLimit;
            public UIntPtr Affinity; public uint Priority,Scheduling;
            public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;
            public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateJobObjectW(IntPtr attributes,string? name);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetInformationJobObject(IntPtr job,int cls,ref Limits info,int size);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool TerminateJobObject(IntPtr job,uint code);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool QueryInformationJobObject(IntPtr job,int cls,IntPtr info,int size,IntPtr returned);
        public static IntPtr Create()
        {
            var job=CreateJobObjectW(IntPtr.Zero,null); if(job==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var limits=new Limits { Flags=0x2000 }; // KILL_ON_JOB_CLOSE; no breakaway permission.
            if(!SetInformationJobObject(job,9,ref limits,Marshal.SizeOf<Limits>())) { int err=Marshal.GetLastWin32Error(); CloseHandle(job); throw new System.ComponentModel.Win32Exception(err); }
            return job;
        }
        public static int[] Pids(IntPtr job)
        {
            int size=8+IntPtr.Size*1024; var b=Marshal.AllocHGlobal(size);
            try
            {
                if(!QueryInformationJobObject(job,3,b,size,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                int count=Marshal.ReadInt32(b,4); if(count<0||count>1024) throw new InvalidOperationException("Job PID count invalid");
                var pids=new int[count]; for(int i=0;i<count;i++) pids[i]=checked((int)Marshal.ReadIntPtr(b,8+i*IntPtr.Size).ToInt64()); return pids;
            }
            finally { Marshal.FreeHGlobal(b); }
        }
    }
}



