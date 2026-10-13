using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;
using NetworkHub.Dns;

namespace NetworkHub.Desktop;

internal sealed record DnsRequest(string Token,int ParentPid,long ParentStart,string Owner,string Language,string Action,DnsSnapshot Before,DnsSnapshot Target,DnsFamily[] Families,string ApprovedFingerprint) { public string ProfileId {get;init;}=""; }
internal sealed record DnsResponse(bool Success,string Message);
internal static class DnsElevated
{
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr handle,uint access,out IntPtr token);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    internal static bool ValidRequest(DnsRequest r,string token)=>r.Token==token&&Guid.TryParseExact(token,"N",out _)&&r.Action is "apply" or "restore"&&r.Before.AdapterId!=Guid.Empty&&r.Before.AdapterId==r.Target.AdapterId&&r.ParentPid>0&&r.ParentStart>0&&r.Owner.StartsWith("S-1-",StringComparison.Ordinal)&&r.ApprovedFingerprint.Length==64;
    internal static string RequestHash(string json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    internal static bool MatchesHash(byte[] data,string hash)=>hash.Length==64&&hash.All(Uri.IsHexDigit)&&CryptographicOperations.FixedTimeEquals(SHA256.HashData(data),Convert.FromHexString(hash));
    static string ParentOwner(int pid){using var p=Process.GetProcessById(pid);if(!OpenProcessToken(p.Handle,8,out var token))throw new Win32Exception();try{using var identity=new WindowsIdentity(token);return identity.User!.Value;}finally{CloseHandle(token);}}
    public static int Run(string[] args)
    {
#if OPENNET_TESTING
        // The safe test executable cannot invoke a real elevated DNS writer under any arguments.
        return 2;
#else
        if(args.Length!=6||args[0]!="--dns-elevated"||args[2]!="--request-sha256"||args[4]!="--data-root"||!Guid.TryParseExact(args[1],"N",out _))return 2;
        string root=Path.GetDirectoryName(Environment.ProcessPath!)!;AppFiles? files=null;string? response=null;
        try{
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
            files=new AppFiles(root,args[5]);string token=args[1];string request=Paths.Owned(files.Data,"Dns/Requests/"+token+"/request.json");response=Paths.Owned(files.Data,"Dns/Requests/"+token+"/response.json");
            Paths.NoReparseAncestors(request);if(File.Exists(response)||new FileInfo(request).Length>65536)throw new InvalidDataException("Invalid DNS request");
            byte[] payload=File.ReadAllBytes(request);if(!MatchesHash(payload,args[3]))throw new InvalidDataException("The approved DNS request was modified.");
            var r=JsonSerializer.Deserialize<DnsRequest>(payload)??throw new InvalidDataException("Invalid DNS request");
            if(!ValidRequest(r,token))throw new InvalidDataException("Invalid DNS request");L.Set(r.Language);
            var parent=ProcessIdentity.Read(r.ParentPid);
            if(!parent.Exists||!parent.IdentityReadable||parent.StartFileTime!=r.ParentStart||!string.Equals(parent.Executable,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase)||ParentOwner(r.ParentPid)!=r.Owner)throw new InvalidDataException("The DNS request does not belong to this Open Net instance.");
            using var gate=new Mutex(false,"Global\\OpenNet-DNS-"+r.Before.AdapterId.ToString("N"));bool locked=false;
            try{
                try{locked=gate.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
                if(!locked)throw new DnsException("Another DNS operation is in progress. Wait and retry.");
                var backend=new WindowsDnsSettings();var current=backend.Read(r.Before.AdapterId);
                if(current.Fingerprint()!=r.ApprovedFingerprint)throw new DnsException("DNS settings changed before application. Refresh and review them again.");
                var store=DnsRecoveryStore.Open(r.Owner,true);DnsRecoveryStore.ValidateRecord(r.Before.AdapterId,r.Owner);
                DnsRecoveryStore.MarkUsed(r.Owner,r.Token);
                var transaction=new DnsTransaction(backend,backend,store,r.Owner);
                if(r.Action=="apply"){
                    DnsTransaction.Validate(r.Before,r.Target,r.Families);
                    // Availability restriction applies to built-in addresses even if a stale UI requests them.
                    if(r.Families.Any(f=>r.Target.Family(f).Servers.Any(s=>DnsProfiles.All.First(p=>p.Id=="xbox").IPv4.Concat(DnsProfiles.All.First(p=>p.Id=="xbox").IPv6).Contains(s))))throw new DnsException("The operator reports that Xbox DNS is unavailable. New application is blocked; existing settings are preserved.");
                    foreach(var f in r.Families)WindowsDnsSettings.ValidateDestination(r.Target.Family(f).Servers);
                    DnsProfiles.ValidateSelection(r.ProfileId,r.Target,r.Families);
                    transaction.Apply(r.Before,r.Target,r.Families,r.ProfileId);
                }else transaction.Restore(r.Before.AdapterId,r.ApprovedFingerprint);
                Paths.WriteNew(response,JsonSerializer.Serialize(new DnsResponse(true,L.T(r.Action=="apply"?"DNS profile applied. Connection has not been tested.":"The original DNS configuration was restored."))));return 0;
            }finally{if(locked)gate.ReleaseMutex();}
        }catch(Exception ex){files?.Log("DNS helper: "+ex);try{if(response!=null&&!File.Exists(response))Paths.WriteNew(response,JsonSerializer.Serialize(new DnsResponse(false,ex is DnsException?L.T(ex.Message):L.Error(ex))));}catch(IOException){}catch(UnauthorizedAccessException){}return 1;}
#endif
    }
    internal static async Task<DnsResponse> Execute(AppFiles files,DnsRequest r,bool testMode)
    {
        if(testMode)throw new InvalidOperationException("Real DNS helper is forbidden in safe mode");
        string json=JsonSerializer.Serialize(r),hash=RequestHash(json);
        string directory=Paths.Owned(files.Data,"Dns/Requests/"+r.Token);Directory.CreateDirectory(directory);string request=Path.Combine(directory,"request.json");Paths.WriteNew(request,json);
        var plan=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        plan.Arguments="--dns-elevated "+r.Token+" --request-sha256 "+hash+" --data-root "+Quote(files.Data);
        using var p=Process.Start(plan)??throw new DnsException("Windows did not start the DNS helper.");await p.WaitForExitAsync();
        string response=Path.Combine(directory,"response.json");if(!File.Exists(response))throw new DnsException("The DNS helper did not report completion. Refresh and review recovery before retrying.");
        if(new FileInfo(response).Length>65536)throw new InvalidDataException("Invalid DNS response");return JsonSerializer.Deserialize<DnsResponse>(File.ReadAllText(response))??throw new InvalidDataException("Invalid DNS response");
    }
    static string Quote(string value){if(value.Contains('"'))throw new InvalidDataException("Invalid data path");return "\""+value.TrimEnd('\\')+"\"";}
}
