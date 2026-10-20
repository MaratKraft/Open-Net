using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public interface IStartupStore {string? Read(string name);void Write(string name,string command);void Delete(string name);}
public sealed class RegistryStartupStore(string keyPath=@"Software\Microsoft\Windows\CurrentVersion\Run"):IStartupStore
{
    public string? Read(string name){using var key=Registry.CurrentUser.OpenSubKey(keyPath);var value=key?.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value!=null&&value is not string)throw new IOException(L.T("The startup entry was changed outside Open Net."));return value as string;}
    public void Write(string name,string command){using var key=Registry.CurrentUser.CreateSubKey(keyPath,true)??throw new IOException(L.T("Windows startup settings are not writable."));key.SetValue(name,command,RegistryValueKind.String);}
    public void Delete(string name){using var key=Registry.CurrentUser.OpenSubKey(keyPath,true);key?.DeleteValue(name,false);}
}
public sealed class IsolatedStartupStore:IStartupStore
{
    readonly Dictionary<string,string> values=new();
    public string? Read(string name)=>values.TryGetValue(name,out var value)?value:null;
    public void Write(string name,string command)=>values[name]=command;
    public void Delete(string name)=>values.Remove(name);
}
public sealed record StartupReceipt(string Name,string Command,string Executable,string InstallationId){public string Mode {get;init;}="run";}
public sealed record StartupStatus(bool Registered,bool NeedsRepair,string Message)
{
    public bool Requested {get;init;}public bool SettingsSaved {get;init;}public bool? Exists {get;init;}public bool? Enabled {get;init;}public bool? Owned {get;init;}public bool ConfigurationValid {get;init;}public bool Ready {get;init;}public bool RepairAllowed {get;init;}
    public string Mode {get;init;}="";public string Command {get;init;}="";public string? TaskHash {get;init;}public string TaskName {get;init;}="";public string? LastRunUtc {get;init;}public int? LastTaskResult {get;init;}public int? TaskState {get;init;}public bool? HistoryEnabled {get;init;}
}
// Registration changes happen only after Save/Repair, never during ordinary startup.
public sealed class AutostartService(AppFiles files,IStartupStore store,string executable)
{
    public string ReceiptPath=>Paths.Owned(files.Data,"Config/startup-registration.json");
    public string Command=>CommandFor(executable);
    public static string CommandFor(string executable)
    {
        executable=Path.GetFullPath(executable);
        if(executable.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException(L.T("The application path cannot be used for Windows startup."));
        string command="\""+executable+"\" --autostart";
        if(command.Length>260)throw new IOException(L.T("The application path is too long for Windows startup. Move Open Net to a shorter path."));
        return command;
    }
    StartupReceipt? Receipt()=>File.Exists(ReceiptPath)?JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(ReceiptPath)):null;
    internal bool HasScheduledReceipt=>Receipt()?.Mode=="task";
    string Name(UserSettings settings)
    {
        if(!files.Portable)return EntryName(false,"");
        if(!Guid.TryParseExact(settings.StartupInstallationId,"N",out _))throw new InvalidDataException(L.T("The portable startup identity is missing. Save the startup setting again."));
        return EntryName(true,settings.StartupInstallationId);
    }
    public static string EntryName(bool portable,string identity)=>portable?"OpenNet.Portable."+identity:"OpenNet";
    bool Ours(StartupReceipt? receipt,UserSettings settings,string name,string? command)=>receipt!=null&&receipt.Name==name&&receipt.InstallationId==(files.Portable?settings.StartupInstallationId:"")&&string.Equals(receipt.Command,command,StringComparison.OrdinalIgnoreCase);
    public StartupStatus Inspect(UserSettings settings)
    {
        bool saved=false;try{var stored=files.Load();saved=File.Exists(files.SettingsPath)&&stored.StartWithWindows==settings.StartWithWindows&&stored.AutoStartTools==settings.AutoStartTools&&stored.StartupInTray==settings.StartupInTray&&stored.StartupInstallationId==settings.StartupInstallationId&&stored.ZapretSelected==settings.ZapretSelected&&stored.TgSelected==settings.TgSelected;}catch(Exception){}
        try{
            if(files.Portable&&settings.StartupInstallationId==""&&!settings.StartWithWindows)return new(false,false,L.T("Windows startup is off. Network tools never start automatically.")){Requested=false,SettingsSaved=saved,Exists=false,Enabled=false,Owned=false};
            var receipt=Receipt();string name=Name(settings);var actual=store is WindowsStartupStore windows?windows.Observe(name):new StartupRegistrationObservation(store.Read(name)!=null,true,true,store.Read(name),"run","",false,false,null,null);
            bool owned=Ours(receipt,settings,name,actual.Command),registered=actual.Exists&&owned&&actual.ConfigurationValid;
            bool modeMatches=store is not WindowsStartupStore||actual.Mode==(settings.AutoStartTools?"task":"run");bool pathMatches=string.Equals(actual.Command,Command,StringComparison.OrdinalIgnoreCase);
            bool ready=settings.StartWithWindows&&registered&&actual.Enabled&&modeMatches&&pathMatches;
            string reason=!actual.ConfigurationValid?actual.Reason:actual.Exists&&!owned?L.T("No confirmed registration receipt exists. Review and repair startup explicitly."):actual.Exists&&!actual.Enabled?L.T("Startup task is disabled. Review and repair the registration explicitly."):actual.Exists&&owned&&!pathMatches?L.T("Open Net was moved. Repair its startup entry to use this location."):actual.Exists&&!modeMatches?L.T("Save startup settings again to repair the registration mode."):!settings.StartWithWindows?L.T(actual.Exists?"Startup preference is off, but a Windows registration still exists.":"Windows startup is off. Network tools never start automatically."):!actual.Exists?L.T("The startup entry is missing or was changed outside Open Net. Other entries are left unchanged."):L.T("Startup registration is ready. Actual logon and tool launch are reported separately.");
            bool repair=actual.Exists&&(!owned||!actual.ConfigurationValid||!actual.Enabled||!pathMatches||!modeMatches||!settings.StartWithWindows)||settings.StartWithWindows&&!actual.Exists;
            return new(registered,repair,reason){Requested=settings.StartWithWindows,SettingsSaved=saved,Exists=actual.Exists,Enabled=actual.Enabled,Owned=owned,ConfigurationValid=actual.ConfigurationValid,Ready=ready,RepairAllowed=actual.Mode=="task"?actual.IdentityMatches&&!actual.Duplicate:owned||!actual.Exists,Mode=actual.Mode,Command=actual.Command??"",TaskHash=actual.TaskHash,TaskName=actual.Runtime?.TaskName??"",LastRunUtc=actual.Runtime?.LastRunUtc,LastTaskResult=actual.Runtime?.LastResult,TaskState=actual.Runtime?.State,HistoryEnabled=actual.Runtime?.HistoryEnabled};
        }catch(Exception ex){return new(false,true,L.T("Could not check Windows startup: ")+L.Error(ex)){Requested=settings.StartWithWindows,SettingsSaved=saved};}
    }
    public void Configure(UserSettings settings,Action persist,string? approvedTaskHash=null)
    {
        string? oldReceipt=File.Exists(ReceiptPath)?File.ReadAllText(ReceiptPath):null;
        var receipt=oldReceipt==null?null:JsonSerializer.Deserialize<StartupReceipt>(oldReceipt);
        if(files.Portable&&settings.StartWithWindows&&!Guid.TryParseExact(settings.StartupInstallationId,"N",out _))settings.StartupInstallationId=Guid.NewGuid().ToString("N");
        string name=files.Portable&&settings.StartupInstallationId==""?"":Name(settings);
        string? previous=name==""?null:store.Read(name);
        bool approvedRepair=store is WindowsStartupStore windows&&windows.ApprovedRepair(name,approvedTaskHash);
        if(previous!=null&&!Ours(receipt,settings,name,previous)&&!approvedRepair)throw new InvalidOperationException(L.T("The startup entry belongs to another registration. It was not changed."));
        // A portable copy at a second still-existing location must not hijack the first copy.
        if(settings.StartWithWindows&&files.Portable&&receipt!=null&&receipt.Executable!=Path.GetFullPath(executable)&&File.Exists(receipt.Executable))throw new InvalidOperationException(L.T("Another portable copy still owns this startup entry. Disable startup there before registering this copy."));
        bool changed=false;
        object? snapshot=name!=""&&store is ITransactionalStartupStore transaction?transaction.Capture(name):null;
        try{
            if(settings.StartWithWindows){changed=true;store.Write(name,Command);WriteReceipt(JsonSerializer.Serialize(new StartupReceipt(name,Command,Path.GetFullPath(executable),files.Portable?settings.StartupInstallationId:""){Mode=store is ITransactionalStartupStore backend?backend.Mode:"run"}));}
            else{if(previous!=null){store.Delete(name);changed=true;}if(File.Exists(ReceiptPath))File.Delete(ReceiptPath);}
            persist();
        }catch{
            try{
                if(snapshot!=null&&store is ITransactionalStartupStore transactional)transactional.Rollback(name,snapshot);
                else if(changed){string? current=store.Read(name);if(current==(settings.StartWithWindows?Command:null)){if(previous==null)store.Delete(name);else store.Write(name,previous);}}
                if(oldReceipt==null){if(File.Exists(ReceiptPath))File.Delete(ReceiptPath);}else WriteReceipt(oldReceipt);
            }catch(Exception ex){files.Log("Startup rollback: "+ex);}
            throw;
        }
    }
    void WriteReceipt(string content){Paths.NoReparseAncestors(ReceiptPath);Directory.CreateDirectory(Path.GetDirectoryName(ReceiptPath)!);string temp=ReceiptPath+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,content);File.Move(temp,ReceiptPath,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
