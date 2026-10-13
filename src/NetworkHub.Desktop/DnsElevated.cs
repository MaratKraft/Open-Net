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

internal sealed record DnsRequest(string Token,int ParentPid,long ParentStart,string Owner,string Language,string Action,DnsSnapshot Before,DnsSnapshot Target,DnsFamily[] Families,string ApprovedFingerprint) { public string ProfileId {get;init;}=""; public string ProfileName {get;init;}="";public SavedDnsProfile? CustomProfile {get;init;}public bool EncryptionChangeApproved {get;init;}public bool ClearCache {get;init;}=true; }
internal sealed record DnsResponse(bool Success,string Message) {public DnsOperationEvidence? Evidence {get;init;}}
internal static class DnsElevated
{
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr handle,uint access,out IntPtr token);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    internal static bool ValidRequest(DnsRequest r,string token)=>r.Token==token&&Guid.TryParseExact(token,"N",out _)&&r.Action is "apply" or "automatic" or "restore" or "flush" or "reapply"&&r.Before.AdapterId!=Guid.Empty&&r.Before.AdapterId==r.Target.AdapterId&&r.ParentPid>0&&r.ParentStart>0&&r.Owner.StartsWith("S-1-",StringComparison.Ordinal)&&r.ApprovedFingerprint.Length==64&&(r.Action!="reapply"||!r.ClearCache);
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
        string root=Path.GetDirectoryName(Environment.ProcessPath!)!;AppFiles? files=null;string? response=null;var evidence=new DnsOperationEvidence();
        try{
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
            files=new AppFiles(root,args[5]);string token=args[1];string request=Paths.Owned(files.Data,"Dns/Requests/"+token+"/request.json");response=Paths.Owned(files.Data,"Dns/Requests/"+token+"/response.json");
            Paths.NoReparseAncestors(request);if(File.Exists(response)||new FileInfo(request).Length>65536)throw new InvalidDataException("Invalid DNS request");
            byte[] payload=File.ReadAllBytes(request);if(!MatchesHash(payload,args[3]))throw new InvalidDataException("The approved DNS request was modified.");
            var r=JsonSerializer.Deserialize<DnsRequest>(payload)??throw new InvalidDataException("Invalid DNS request");
            evidence.Begin(r.Action,r.Before);
            if(!ValidRequest(r,token))throw new InvalidDataException("Invalid DNS request");L.Set(r.Language);
            var parent=ProcessIdentity.Read(r.ParentPid);
            if(!parent.Exists||!parent.IdentityReadable||parent.StartFileTime!=r.ParentStart||!string.Equals(parent.Executable,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase)||ParentOwner(r.ParentPid)!=r.Owner)throw new InvalidDataException("The DNS request does not belong to this Open Net instance.");
            using var gate=new Mutex(false,"Global\\OpenNet-DNS-"+r.Before.AdapterId.ToString("N"));bool locked=false;
            try{
                try{locked=gate.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
                if(!locked)throw new DnsException("Another DNS operation is in progress. Wait and retry.");
                var backend=new WindowsDnsSettings();var current=backend.Read(r.Before.AdapterId);
                if(current.Fingerprint()!=r.ApprovedFingerprint)throw new DnsException("DNS settings changed before application. Refresh and review them again.");
                if(r.Action=="flush"){evidence.CacheResult="requested";string cacheMessage=DnsCache.Clear(false);evidence.CacheResult="cleared";Paths.WriteNew(response,JsonSerializer.Serialize(new DnsResponse(true,L.T(cacheMessage)){Evidence=evidence}));return 0;}
                var store=DnsRecoveryStore.Open(r.Owner,true);DnsRecoveryStore.ValidateRecord(r.Before.AdapterId,r.Owner);
                DnsRecoveryStore.MarkUsed(r.Owner,r.Token);
                var transaction=new DnsTransaction(backend,backend,store,r.Owner,evidence);
                if(r.Action is "apply" or "reapply"){
                    if(r.Action=="reapply"){if(r.ClearCache)throw new DnsException("Repeat never clears the DNS cache. Use the separate cache action.");DnsTransaction.ValidateReapply(r.Before,r.Target,r.Families);}
                    DnsTransaction.Validate(r.Before,r.Target,r.Families,false,r.EncryptionChangeApproved);
                    if(r.ProfileId=="custom"){
                        if(r.CustomProfile==null)throw new DnsException("A validated custom profile is required.");DnsProfileStore.Validate(r.CustomProfile);
                        foreach(var f in r.Families){var addresses=f==DnsFamily.IPv4?r.CustomProfile.IPv4:r.CustomProfile.IPv6;if(!addresses.SequenceEqual(r.Target.Family(f).Servers))throw new DnsException("DNS addresses do not match the selected profile.");var expected=DnsProfiles.ProfilePlan(r.Before.Family(f),addresses,new("custom",r.CustomProfile.Name,r.CustomProfile.IPv4,r.CustomProfile.IPv6,r.CustomProfile.Doh,"","",true,""),r.CustomProfile.UseDoh,r.CustomProfile.Fallback);if(!expected.SameConfiguration(r.Target.Family(f)))throw new DnsException("DoH settings do not match the selected profile.");}
                    }
                    foreach(var f in r.Families)WindowsDnsSettings.ValidateDestination(r.Target.Family(f).Servers);
                    DnsProfiles.ValidateSelection(r.ProfileId,r.Target,r.Families);
                    if(r.Action=="reapply")transaction.Reapply(r.Before,r.Target,r.Families,r.ProfileId,r.ProfileName);else transaction.Apply(r.Before,r.Target,r.Families,r.ProfileId,r.EncryptionChangeApproved,r.ProfileName);
                }else if(r.Action=="automatic"){
                    DnsTransaction.Validate(r.Before,r.Target,r.Families,true,true);if(!r.Families.SequenceEqual(new[]{DnsFamily.IPv4}))throw new DnsException("Automatic DNS changes only IPv4 DNS assignment.");transaction.Automatic(r.Before);
                }else transaction.Restore(r.Before.AdapterId,r.ApprovedFingerprint);
                string message=L.T(r.Action=="reapply"?"Windows accepted the repeat request. DNS and DoH values are unchanged; a network-state change and service availability have not been confirmed.":r.Action=="apply"?"DNS profile applied. Connection has not been tested.":r.Action=="automatic"?"DNS automatically. The original configuration remains recoverable.":"The original DNS configuration was restored.");
                if(r.Action!="reapply"&&r.ClearCache)try{evidence.CacheResult="requested";message+="\n"+L.T(DnsCache.Clear(false));evidence.CacheResult="cleared";}catch(Exception){evidence.CacheResult="failed";message+="\n"+L.T("DNS configuration is verified, but cache clearing failed. Retry cache clearing separately.");}
                Paths.WriteNew(response,JsonSerializer.Serialize(new DnsResponse(true,message){Evidence=evidence}));return 0;
            }finally{if(locked)gate.ReleaseMutex();}
        }catch(Exception ex){if(evidence.CacheResult=="requested")evidence.CacheResult="failed";files?.Log("DNS helper: "+ex);try{if(response!=null&&!File.Exists(response))Paths.WriteNew(response,JsonSerializer.Serialize(new DnsResponse(false,ex is DnsException?L.T(ex.Message):L.Error(ex)){Evidence=evidence}));}catch(IOException){}catch(UnauthorizedAccessException){}return 1;}
#endif
    }
    internal static async Task<DnsResponse> Execute(AppFiles files,DnsRequest r,bool testMode)
    {
        if(testMode)throw new InvalidOperationException("Real DNS helper is forbidden in safe mode");
        string json=JsonSerializer.Serialize(r),hash=RequestHash(json);
        string directory=Paths.Owned(files.Data,"Dns/Requests/"+r.Token);Directory.CreateDirectory(directory);string request=Path.Combine(directory,"request.json");Paths.WriteNew(request,json);
        var plan=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        plan.Arguments="--dns-elevated "+r.Token+" --request-sha256 "+hash+" --data-root "+Quote(files.Data);
        Process? started;try{started=Process.Start(plan);}catch(Win32Exception ex)when(ex.NativeErrorCode==1223){throw new DnsException("Windows permission was declined. DNS settings were not changed.");}
        using var p=started??throw new DnsException("Windows did not start the DNS helper.");await p.WaitForExitAsync();
        string response=Path.Combine(directory,"response.json");if(!File.Exists(response))throw new DnsException("The DNS helper did not report completion. Refresh and review recovery before retrying.");
        if(new FileInfo(response).Length>65536)throw new InvalidDataException("Invalid DNS response");return JsonSerializer.Deserialize<DnsResponse>(File.ReadAllText(response))??throw new InvalidDataException("Invalid DNS response");
    }
    static string Quote(string value){if(value.Contains('"'))throw new InvalidDataException("Invalid data path");return "\""+value.TrimEnd('\\')+"\"";}
}
