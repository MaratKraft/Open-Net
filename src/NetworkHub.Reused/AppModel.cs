using NetworkHub.App;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NetworkHub.Core;
using NetworkHub.Adapters;

namespace NetworkHub.App;
public sealed class UserSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "system";
    public string Backdrop { get; set; } = "mica";
    public string LastWorkingStrategy { get; set; } = "";
    public string ZapretDirectory { get; set; } = "";
    public string TgExe { get; set; } = "";
    public string Strategy { get; set; } = "general (ALT13).bat";
    public bool ZapretSelected { get; set; } = true;
    public bool TgSelected { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ConfirmStartStop { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool AutoStartTools { get; set; } = false;
    public bool? StartupZapret {get;set;}
    public bool? StartupTg {get;set;}
    public bool StartupInTray { get; set; } = true;
    public WorkspaceSettings Workspace {get;set;}=new();
    public string PendingStrategy {get;set;}="";
    public string DnsProfileName {get;set;}="ComssDNS";
    public string DnsProfileId {get;set;}="comss";
    public string DnsAdapterId {get;set;}="";
    public string StartupInstallationId { get; set; } = "";
    public UserSettings Copy()=>JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(this))!;
    public ModulePreferences ForModule(string id)=>id switch {
        "zapret"=>new(ZapretDirectory,Strategy,ZapretSelected),
        "tg"=>new(TgExe,"",TgSelected),
        _=>throw new NotSupportedException(id)
    };
    public void SetModule(string id,ModulePreferences value)
    {
        if(id=="zapret"){ZapretDirectory=value.Path;Strategy=value.Strategy;ZapretSelected=value.Selected;}
        else if(id=="tg"){TgExe=value.Path;TgSelected=value.Selected;}
        else throw new NotSupportedException(id);
    }
}
public sealed record ModulePreferences(string Path,string Strategy,bool Selected);
public sealed class AppFiles
{
    public string? LogFailure {get;private set;}
    public string Root { get; }
    public string Data { get; }
    public string ModuleRoot => Paths.Within(Data,Root)?Root:Data;
    public bool Portable => Paths.Within(Data,Root);
    public string Modules => Paths.Owned(ModuleRoot,"Modules");
    public string ModulePath(string relative)=>Paths.Owned(Modules,relative);
    public string SettingsPath => Paths.Owned(Data,"Config/settings.json");
    public AppFiles(string root,string? data=null)
    {
        Root=Path.GetFullPath(root);Data=Path.GetFullPath(data??Path.Combine(Root,"Data"));
        if(!Paths.Within(Data,Root)&&!Paths.Overlap(Data,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Open Net")))throw new InvalidDataException(L.T("The data folder is not allowed. Choose a user folder."));
        Paths.NoReparseAncestors(Data); Directory.CreateDirectory(Data);
    }
    public UserSettings Defaults()
    {
        // Recommended first-install preferences; existing JSON keeps its own values.
        // Registration still requires explicit approval and Windows UAC.
        return new(){StartWithWindows=true,AutoStartTools=true};
    }
    public UserSettings Load()
    {
        if(!File.Exists(SettingsPath)) return Defaults();
        string json=File.ReadAllText(SettingsPath);
        using var document=JsonDocument.Parse(json);
        var settings=JsonSerializer.Deserialize<UserSettings>(json) ?? throw new InvalidDataException(L.T("Settings are empty. Save them again or restore a backup."));
        if(!document.RootElement.EnumerateObject().Any(x=>string.Equals(x.Name,"Theme",StringComparison.OrdinalIgnoreCase)))settings.Theme="dark";
        // Older saved profiles without this preference retain their previous behavior.
        if(!document.RootElement.TryGetProperty("ConfirmStartStop",out _))settings.ConfirmStartStop=true;
        if(settings.Theme is not ("system" or "light" or "dark"))settings.Theme="system";
        settings.ZapretDirectory=Resolve(settings.ZapretDirectory);settings.TgExe=Resolve(settings.TgExe);return settings;
    }
    public string Resolve(string value)=>string.IsNullOrWhiteSpace(value)?"":value.StartsWith("@modules/",StringComparison.Ordinal)?ModulePath(value.Substring(9)):Path.GetFullPath(value,Root);
    public void Save(UserSettings settings)
    {
        Paths.NoReparseAncestors(SettingsPath);Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var persisted=JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        string Relative(string value)=>value!=""&&Paths.Within(value,Modules)?"@modules/"+Path.GetRelativePath(Modules,value).Replace('\\','/'):value!=""&&Paths.Within(value,Root)?Path.GetRelativePath(Root,value).Replace('\\','/'):value;
        persisted.ZapretDirectory=Relative(persisted.ZapretDirectory);persisted.TgExe=Relative(persisted.TgExe);
        string temporary=SettingsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,JsonSerializer.Serialize(persisted,new JsonSerializerOptions {WriteIndented=true}),new UTF8Encoding(false)); File.Move(temporary,SettingsPath,true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Log(string message)
    {
        try
        {
            string path=Paths.Owned(Data,"Logs/hub.log");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lock(this)File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+LogSafety.Redact(message)+Environment.NewLine);LogFailure=null;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {LogFailure=ex.Message;System.Diagnostics.Trace.WriteLine("Open Net log failure: "+ex);}
    }
    public HubConfig ForZapret(UserSettings s) => new() { Strategy=s.Strategy, ZapretDirectory=s.ZapretDirectory, TgExe=s.TgExe, Workspace=Paths.Owned(Data,"work"),TrustedFiles=s.ZapretDirectory==""?new():ModuleTrust.ZapretHashes(this,s.ZapretDirectory).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase) };
    public void ValidateIsolation(UserSettings s)
    {
        if(string.IsNullOrWhiteSpace(s.ZapretDirectory)) throw new InvalidDataException(L.T("Choose the Zapret folder in Settings."));
        bool managedZap=Paths.Within(s.ZapretDirectory,Modules);
        bool managedTg=s.TgExe!=""&&Paths.Within(s.TgExe,Modules);
        if((Paths.Overlap(Data,s.ZapretDirectory)&&!managedZap)||(s.TgExe!=""&&Paths.Within(s.TgExe,Data)&&!managedTg))throw new InvalidDataException(L.T("Choose a separate external build or an installed module from Modules."));
        Paths.NoReparseAncestors(Data);
    }
}
public enum ObservedState { Stopped, Starting, Running, Error, External, Unknown, CleanupRequired, NotInstalled }
public sealed record ToolObservation(string Id,ObservedState State,string Detail,int? Pid=null);
public sealed record OwnedReceipt(string Token,int Pid,long StartFileTime,string Executable,string Module)
{
    public string Profile {get;init;}="";
}
public sealed record Probe(bool Exists,bool IdentityReadable,long StartFileTime,string? Executable);
public static class ProcessIdentity
{
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryFullProcessImageNameW(IntPtr process,uint flags,StringBuilder path,ref int size);
    public static Probe Read(int pid)
    {
        IntPtr h=OpenProcess(0x1000|0x100000,false,pid);
        if(h==IntPtr.Zero) return Marshal.GetLastWin32Error()==87 ? new(false,true,0,null) : new(true,false,0,null);
        try
        {
            if(WaitForSingleObject(h,0)==0) return new(false,true,0,null);
            var path=new StringBuilder(32768); int size=path.Capacity;
            if(!GetProcessTimes(h,out var start,out _,out _,out _) || !QueryFullProcessImageNameW(h,0,path,ref size)) return new(true,false,0,null);
            return new(true,true,start,path.ToString());
        }
        finally {CloseHandle(h);}
    }
    public static ToolObservation ObserveOwned(OwnedReceipt r)
    {
        var p=Read(r.Pid);
        if(!p.Exists) return new(r.Module,ObservedState.Stopped,L.T("The process exited."));
        if(!p.IdentityReadable) return new(r.Module,ObservedState.Unknown,L.T("Could not verify process ownership. Check the diagnostic log."),r.Pid);
        if(p.StartFileTime!=r.StartFileTime || !string.Equals(p.Executable,r.Executable,StringComparison.OrdinalIgnoreCase))
            return new(r.Module,ObservedState.Stopped,L.T("The previous process exited; its PID no longer belongs to it."));
        return new(r.Module,ObservedState.Running,L.T("The process is running. Network connectivity has not been verified."),r.Pid);
    }
}
public sealed class TgExeAdapter
{
    readonly AppFiles? files;
    public TgExeAdapter(AppFiles? files=null)=>this.files=files;
    public const string Digest="B11232956820B449348E51452C2D9EA2E441679B2E40053825B8E878689F51AB";
    public string? Validate(string path)
    {
        if(!File.Exists(path)) return L.T("TG WS Proxy was not found. Set its path in Settings.");
        if(!Path.GetFileName(path).Equals("TgWsProxy_windows.exe",StringComparison.OrdinalIgnoreCase)) return L.T("Choose the original TgWsProxy_windows.exe.");
        if(!Metadata.X64Pe(path)||!(Metadata.Hash(path)==Digest||files!=null&&ModuleTrust.Matches(files,"tg",path))) return L.T("This TG file has no matching verified official release record. Install or import the official release first.");
        return null;
    }
    public ProcessStartInfo Plan(string path)
    {
        string? error=Validate(path); if(error!=null) throw new InvalidDataException(error);
        return new ProcessStartInfo(Path.GetFullPath(path)) {UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(path))!,CreateNoWindow=true};
    }
}
public sealed record ZapretRequest(string Token,UserSettings Settings,int ParentPid,long ParentStart,string Action="start");
public sealed record ZapretResponse(bool Success,string Message,OwnedReceipt? Receipt);
public static class ZapretPreparation
{
    public static LaunchPlan Prepare(AppFiles files,UserSettings settings)
    {
        files.ValidateIsolation(settings);
        if(!ReviewedStrategy.Supported(settings.ZapretDirectory,settings.Strategy,files.ForZapret(settings).TrustedFiles)) throw new NotSupportedException(L.T("This strategy format is unsupported or its BAT was modified. Choose a compatible official strategy; no automatic substitution."));
        return new ZapretAdapter(files.ForZapret(settings)).Prepare();
    }
}




