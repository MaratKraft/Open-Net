using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;

namespace NetworkHub.Desktop;

internal sealed record StartupRegistrationRequest(string Token,int ParentPid,long ParentStart,string Owner,string SettingsHash,UserSettings Settings,string? ApprovedTaskHash=null);
internal sealed record StartupRegistrationResponse(bool Success,string Message);
internal static class StartupRegistration
{
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static async Task Configure(AppFiles files,UserSettings settings,string? approvedTaskHash=null)
    {
        string token=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"StartupRequests/"+token);Directory.CreateDirectory(folder);
        var parent=ProcessIdentity.Read(Environment.ProcessId);if(!parent.IdentityReadable)throw new IOException("Open Net identity could not be verified.");
        string request=Path.Combine(folder,"request.json"),response=Path.Combine(folder,"response.json");
        var value=new StartupRegistrationRequest(token,Environment.ProcessId,parent.StartFileTime,WindowsIdentity.GetCurrent().User!.Value,Hash(files.SettingsPath),settings,approvedTaskHash);
        Paths.WriteNew(request,JsonSerializer.Serialize(value));
        var plan=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        foreach(string argument in new[]{"--startup-register",token,"--data-root",files.Data,"--hash",Hash(request)})plan.ArgumentList.Add(argument);
        using var helper=Process.Start(plan)??throw new IOException(L.T("The startup helper did not start."));
        // Do not time out and discard an elevated registration still in progress.
        await helper.WaitForExitAsync();
        var result=JsonSerializer.Deserialize<StartupRegistrationResponse>(File.ReadAllText(response));
        if(helper.ExitCode!=0||result?.Success!=true)throw new IOException(result?.Message??L.T("Windows startup registration failed."));
    }
    internal static int Run(string[] args)
    {
#if OPENNET_TESTING
        return 2; // A safe test executable must never register production startup.
#else
        if(args.Length!=6||args[2]!="--data-root"||args[4]!="--hash"||!Guid.TryParseExact(args[1],"N",out _)||args[5].Length!=64)return 2;
        AppFiles? files=null;string? response=null;
        try{
            files=new AppFiles(Path.GetDirectoryName(Environment.ProcessPath!)!,args[3]);
            string folder=Paths.Owned(files.Data,"StartupRequests/"+args[1]),request=Path.Combine(folder,"request.json");response=Path.Combine(folder,"response.json");
            Paths.NoReparseAncestors(request);
            if(new FileInfo(request).Length>65536||File.Exists(response)||Hash(request)!=args[5])throw new IOException("The startup request is invalid or already completed.");
            var r=JsonSerializer.Deserialize<StartupRegistrationRequest>(File.ReadAllText(request))??throw new IOException("Invalid startup request.");
            using var user=WindowsIdentity.GetCurrent();var parent=ProcessIdentity.Read(r.ParentPid);
            if(!new WindowsPrincipal(user).IsInRole(WindowsBuiltInRole.Administrator)||user.User?.Value!=r.Owner||r.Token!=args[1]||!parent.Exists||!parent.IdentityReadable||parent.StartFileTime!=r.ParentStart||!string.Equals(parent.Executable,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new IOException("Startup registration requires the same Windows administrator account and the confirmed Open Net process.");
            L.Set(r.Settings.Language);
            using var guard=new System.Threading.Mutex(false,"Local\\OpenNet-Startup-Registration-"+r.Owner);
            if(!guard.WaitOne(0))throw new IOException("Another startup registration is in progress.");
            try{
                if(Hash(files.SettingsPath)!=r.SettingsHash)throw new IOException("Settings changed before startup registration. Save again.");
                var store=new WindowsStartupStore(r.Settings.AutoStartTools,approvedTaskHash:r.ApprovedTaskHash);
                var service=new AutostartService(files,store,Environment.ProcessPath!);
                if(r.ApprovedTaskHash!=null){
                    string name=AutostartService.EntryName(files.Portable,r.Settings.StartupInstallationId);
                    if(!store.ApprovedRepair(name,r.ApprovedTaskHash))throw new IOException(L.T("Startup task changed after the repair confirmation. Nothing was replaced."));
                    string backup=Paths.Owned(files.Data,"Backups/Startup/"+Guid.NewGuid().ToString("N")+".xml");Directory.CreateDirectory(Path.GetDirectoryName(backup)!);Paths.WriteNew(backup,System.Xml.Linq.XDocument.Parse(store.BackupTask(name)).ToString());
                }
                service.Configure(r.Settings,()=>files.Save(r.Settings),r.ApprovedTaskHash);
                Paths.WriteNew(response,JsonSerializer.Serialize(new StartupRegistrationResponse(true,"")));return 0;
            }finally{guard.ReleaseMutex();}
        }catch(Exception ex){files?.Log("Startup registration: "+ex);if(response!=null&&!File.Exists(response))try{Paths.WriteNew(response,JsonSerializer.Serialize(new StartupRegistrationResponse(false,L.Error(ex))));}catch(IOException){}return 1;}
#endif
    }
}
