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
public sealed record StartupReceipt(string Name,string Command,string Executable,string InstallationId);
public sealed record StartupStatus(bool Registered,bool NeedsRepair,string Message);
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
    string Name(UserSettings settings)
    {
        if(!files.Portable)return EntryName(false,"");
        if(!Guid.TryParseExact(settings.StartupInstallationId,"N",out _))throw new InvalidDataException(L.T("The portable startup identity is missing. Save the startup setting again."));
        return EntryName(true,settings.StartupInstallationId);
    }
    public static string EntryName(bool portable,string identity)=>portable?"OpenNet.Portable."+identity:"OpenNet";
    bool Ours(StartupReceipt? receipt,UserSettings settings,string name,string? command)=>receipt!=null&&receipt.Name==name&&receipt.InstallationId==(files.Portable?settings.StartupInstallationId:"")&&receipt.Command==command;
    public StartupStatus Inspect(UserSettings settings)
    {
        if(!settings.StartWithWindows)return new(false,false,L.T("Windows startup is off. Network tools never start automatically."));
        try{
            var receipt=Receipt();string name=Name(settings),value=store.Read(name)??"";
            if(!Ours(receipt,settings,name,value))return new(false,receipt!=null&&value=="",L.T("The startup entry is missing or was changed outside Open Net. Other entries are left unchanged."));
            if(receipt!.Command!=Command)return new(true,true,L.T("Open Net was moved. Repair its startup entry to use this location."));
            return new(true,false,L.T("Open Net will start in the tray. Network tools stay off."));
        }catch(Exception ex){return new(false,false,L.T("Could not check Windows startup: ")+L.Error(ex));}
    }
    public void Configure(UserSettings settings,Action persist)
    {
        string? oldReceipt=File.Exists(ReceiptPath)?File.ReadAllText(ReceiptPath):null;
        var receipt=oldReceipt==null?null:JsonSerializer.Deserialize<StartupReceipt>(oldReceipt);
        if(files.Portable&&settings.StartWithWindows&&!Guid.TryParseExact(settings.StartupInstallationId,"N",out _))settings.StartupInstallationId=Guid.NewGuid().ToString("N");
        string name=files.Portable&&settings.StartupInstallationId==""?"":Name(settings);
        string? previous=name==""?null:store.Read(name);
        if(previous!=null&&!Ours(receipt,settings,name,previous))throw new InvalidOperationException(L.T("The startup entry belongs to another registration. It was not changed."));
        // A portable copy at a second still-existing location must not hijack the first copy.
        if(settings.StartWithWindows&&files.Portable&&receipt!=null&&receipt.Executable!=Path.GetFullPath(executable)&&File.Exists(receipt.Executable))throw new InvalidOperationException(L.T("Another portable copy still owns this startup entry. Disable startup there before registering this copy."));
        bool changed=false;
        try{
            if(settings.StartWithWindows){store.Write(name,Command);changed=true;WriteReceipt(JsonSerializer.Serialize(new StartupReceipt(name,Command,Path.GetFullPath(executable),files.Portable?settings.StartupInstallationId:"")));}
            else{if(previous!=null){store.Delete(name);changed=true;}if(File.Exists(ReceiptPath))File.Delete(ReceiptPath);}
            persist();
        }catch{
            try{
                if(changed){string? current=store.Read(name);if(current==(settings.StartWithWindows?Command:null)){if(previous==null)store.Delete(name);else store.Write(name,previous);}}
                if(oldReceipt==null){if(File.Exists(ReceiptPath))File.Delete(ReceiptPath);}else WriteReceipt(oldReceipt);
            }catch(Exception ex){files.Log("Startup rollback: "+ex);}
            throw;
        }
    }
    void WriteReceipt(string content){Paths.NoReparseAncestors(ReceiptPath);Directory.CreateDirectory(Path.GetDirectoryName(ReceiptPath)!);string temp=ReceiptPath+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,content);File.Move(temp,ReceiptPath,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
