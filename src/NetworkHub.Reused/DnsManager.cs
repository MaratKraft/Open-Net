using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetworkHub.Dns;

public enum DnsMode { Automatic, Manual, Unknown }
public enum DnsFamily { IPv4, IPv6 }
public enum DnsPhase { Prepared, Writing, Applied, Restoring, Restored, RecoveryRequired }
public sealed record DnsDohSettings(string Server,string? Template,ulong Flags);
public sealed record DnsFamilySettings(DnsMode Mode,string[] Servers,string[] EffectiveServers)
{
    public DnsDohSettings[] Encryption {get;init;}=Array.Empty<DnsDohSettings>();
    public bool SameConfiguration(DnsFamilySettings other)=>Mode==other.Mode &&
        (Mode==DnsMode.Automatic || Servers.SequenceEqual(other.Servers,StringComparer.OrdinalIgnoreCase))&&Encryption.SequenceEqual(other.Encryption);
}
public sealed record DnsSnapshot(Guid AdapterId,string AdapterName,string MachineId,DnsFamilySettings IPv4,DnsFamilySettings IPv6,string DependencyFingerprint,bool CanChange,string Limitation)
{
    public DnsFamilySettings Family(DnsFamily family)=>family==DnsFamily.IPv4?IPv4:IPv6;
    public DnsSnapshot With(DnsFamily family,DnsFamilySettings value)=>family==DnsFamily.IPv4?this with{IPv4=value}:this with{IPv6=value};
    public bool SameConfiguration(DnsSnapshot other)=>AdapterId==other.AdapterId&&MachineId==other.MachineId&&DependencyFingerprint==other.DependencyFingerprint&&IPv4.SameConfiguration(other.IPv4)&&IPv6.SameConfiguration(other.IPv6);
    public string Fingerprint()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{AdapterId,MachineId,DependencyFingerprint,V4=new{IPv4.Mode,Servers=IPv4.Mode==DnsMode.Manual?IPv4.Servers:Array.Empty<string>(),IPv4.Encryption},V6=new{IPv6.Mode,Servers=IPv6.Mode==DnsMode.Manual?IPv6.Servers:Array.Empty<string>(),IPv6.Encryption}}))));
}
public sealed record DnsAdapter(Guid Id,string Name,string Kind,bool Connected,bool Virtual);
public sealed record DnsProfile(string Id,string Name,string[] IPv4,string[] IPv6,string? Doh,string Source,string CheckedOn,bool Available,string Warning);
public static class DnsProfiles
{
    public static void ValidateEncryption(DnsFamilySettings settings)
    {
        if(settings.Encryption==null||settings.Encryption.Length>settings.Servers.Length||settings.Encryption.Select(p=>p.Server).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=settings.Encryption.Length)throw new DnsException("Invalid per-adapter DoH configuration.");
        foreach(var item in settings.Encryption){
            if(!settings.Servers.Contains(item.Server,StringComparer.OrdinalIgnoreCase)||(item.Flags&~15UL)!=0||(item.Flags&3)==3||(item.Flags&3)==0&&(item.Flags&12)!=0)throw new DnsException("Invalid per-adapter DoH configuration.");
            if((item.Flags&1)!=0&&item.Template!=null)throw new DnsException("Invalid per-adapter DoH configuration.");
            if((item.Flags&2)!=0&&(!Uri.TryCreate(item.Template,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Fragment.Length>0||System.Net.IPAddress.TryParse(uri.Host,out var host)&&!host.Equals(System.Net.IPAddress.Parse(item.Server))))throw new DnsException("Invalid per-adapter DoH configuration.");
        }
    }
    public static DnsFamilySettings ManualPlan(DnsFamilySettings before,string[] servers,DnsProfile profile)
    {
        var target=new DnsFamilySettings(DnsMode.Manual,servers,servers);
        var active=before.Encryption.Where(p=>(p.Flags&3)!=0).ToArray();
        if(active.Length==0)return target;
        if(active.Length!=before.Servers.Length||active.Select(p=>p.Flags&12).Distinct().Count()!=1||profile.Doh==null)throw new DnsException("This profile cannot preserve the adapter's DNS encryption policy.");
        return target with{Encryption=servers.Select(s=>new DnsDohSettings(s,profile.Doh,2UL|(active[0].Flags&12))).ToArray()};
    }
    public static void ValidateSelection(string id,DnsSnapshot target,DnsFamily[] families)
    {
        if(id=="")return; // Older recovery records do not contain an owned profile identity.
        var profile=All.FirstOrDefault(p=>p.Id==id)??throw new DnsException("Invalid DNS profile identity");
        if(id!="custom"&&families.Any(f=>!target.Family(f).Servers.SequenceEqual(f==DnsFamily.IPv4?profile.IPv4:profile.IPv6)))throw new DnsException("DNS addresses do not match the selected profile.");
        if(id!="custom"&&families.SelectMany(f=>target.Family(f).Encryption).Any(p=>(p.Flags&3)!=0&&(p.Template!=profile.Doh||(p.Flags&2)==0)))throw new DnsException("DoH settings do not match the selected profile.");
    }
    public static IReadOnlyList<DnsProfile> All {get;}=new[]{
        new DnsProfile("comss","ComssDNS",new[]{"83.220.169.155","212.109.195.93"},Array.Empty<string>(),"https://dns.comss.one/dns-query","https://www.comss.ru/page.php?id=7315","2026-10-12",true,""),
        new DnsProfile("xbox","Xbox DNS",new[]{"111.88.96.54","111.88.96.55"},new[]{"2a00:ab00:1233:26::50","2a00:ab00:1233:26::51"},"https://xbox-dns.ru/dns-query","https://xbox-dns.ru/","2026-10-12",false,"The operator reports that Xbox DNS is unavailable. New application is blocked; existing settings are preserved."),
        new DnsProfile("custom","Custom DNS",Array.Empty<string>(),Array.Empty<string>(),null,"","",true,"")
    };
    public static string[] Validate(string text,DnsFamily family)
    {
        string[] values=text.Split(new[]{',',';',' ','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries);
        if(values.Length is <1 or >4)throw new DnsException("Enter one to four DNS addresses for each selected family.");
        var result=new List<string>();
        foreach(string value in values){
            if(!IPAddress.TryParse(value,out var ip)||ip.AddressFamily!=(family==DnsFamily.IPv4?AddressFamily.InterNetwork:AddressFamily.InterNetworkV6))throw new DnsException("Enter valid IP addresses of the selected family, without ports or URLs.");
            byte[] bytes=ip.GetAddressBytes();
            if(IPAddress.IsLoopback(ip)||ip.Equals(IPAddress.Any)||ip.Equals(IPAddress.IPv6Any)||ip.IsIPv6Multicast||ip.IsIPv6LinkLocal||ip.ScopeIdSafe()!=0||(family==DnsFamily.IPv4&&(bytes[0]>=224||bytes[0]==0)))throw new DnsException("Loopback, unspecified, multicast and scoped DNS addresses are unsupported in this alpha.");
            string normalized=ip.ToString();if(result.Contains(normalized,StringComparer.OrdinalIgnoreCase))throw new DnsException("Duplicate DNS addresses are not allowed.");result.Add(normalized);
        }
        return result.ToArray();
    }
    private static long ScopeIdSafe(this IPAddress address)=>address.AddressFamily==AddressFamily.InterNetworkV6?address.ScopeId:0;
}
public sealed class DnsException : Exception {public DnsException(string message):base(message){}}
public interface IDnsSettingsReader {IReadOnlyList<DnsAdapter> Adapters();DnsSnapshot Read(Guid adapter);}
public interface IDnsSettingsWriter {void Write(Guid adapter,DnsFamily family,DnsFamilySettings settings);}
public sealed record DnsJournal(int Schema,Guid Id,string Owner,DnsSnapshot Before,DnsSnapshot Expected,DnsSnapshot Target,DnsFamily[] Families,DnsPhase Phase,DnsFamily? Pending,string CreatedUtc,string Message)
{
    public string ProfileId {get;init;}="";
    public bool Active=>Phase!=DnsPhase.Restored;
}
public interface IDnsJournalStore {DnsJournal? Load(Guid adapter);void Save(DnsJournal journal);}

// Only this controller writes settings; its reader/writer/store are injected for isolated tests.
// Persistent intent precedes every write, including compensating writes.
public sealed class DnsTransaction
{
    readonly IDnsSettingsReader reader;readonly IDnsSettingsWriter writer;readonly IDnsJournalStore store;readonly string owner;
    public DnsTransaction(IDnsSettingsReader reader,IDnsSettingsWriter writer,IDnsJournalStore store,string owner){this.reader=reader;this.writer=writer;this.store=store;this.owner=owner;}
    public DnsJournal Apply(DnsSnapshot approved,DnsSnapshot target,DnsFamily[] families,string profileId="")
    {
        Validate(approved,target,families);
        DnsProfiles.ValidateSelection(profileId,target,families);
        var previous=store.Load(approved.AdapterId);if(previous?.Active==true)throw new DnsException("Restore the existing DNS transaction before applying another profile.");
        var current=reader.Read(approved.AdapterId);
        if(!current.CanChange||!current.SameConfiguration(approved))throw new DnsException("DNS settings changed before application. Refresh and review them again.");
        var j=new DnsJournal(1,Guid.NewGuid(),owner,current,current,target,families,DnsPhase.Prepared,null,DateTime.UtcNow.ToString("O"),""){ProfileId=profileId};
        store.Save(j); // No system write is allowed if the durable baseline could not be saved.
        var verified=store.Load(current.AdapterId);
        if(verified==null||verified.Id!=j.Id||verified.Owner!=owner||verified.ProfileId!=profileId||!verified.Before.SameConfiguration(current)||!verified.Expected.SameConfiguration(current)||!verified.Target.SameConfiguration(target)||!verified.Families.SequenceEqual(families)||verified.Phase!=DnsPhase.Prepared||verified.Pending!=null)throw new DnsException("The saved DNS backup did not pass verification. No settings were changed.");
        try{
            foreach(var family in families){
                EnsureExpected(j);
                j=j with{Phase=DnsPhase.Writing,Pending=family};store.Save(j);
                writer.Write(current.AdapterId,family,target.Family(family));
                var expected=j.Expected.With(family,target.Family(family));
                var observed=reader.Read(current.AdapterId);
                if(!observed.SameConfiguration(expected))throw new DnsException("DNS verification failed. Review the recovery record before restoring.");
                j=j with{Expected=observed,Pending=null};store.Save(j);
            }
            j=j with{Phase=DnsPhase.Applied};store.Save(j);return j;
        }catch(Exception){
            // A persisted pending write may have succeeded even when the API or disk reported failure.
            // Never guess or erase the intent. Explicit recovery re-reads and compares it.
            throw new DnsException("DNS application did not finish. The saved recovery record must be reviewed; do not reset DNS blindly.");
        }
    }
    public DnsJournal Restore(Guid adapter,string approvedFingerprint)
    {
        var j=store.Load(adapter)??throw new DnsException("No DNS backup exists for this adapter.");
        if(!j.Active)throw new DnsException("The original configuration has already been restored.");
        if(j.Schema!=1||j.Owner!=owner||j.Before.AdapterId!=adapter)throw new DnsException("The recovery record does not belong to this user or adapter.");
        var observed=reader.Read(adapter);
        if(observed.Fingerprint()!=approvedFingerprint)throw new DnsException("DNS settings changed before restoration. Refresh and review them again.");
        if(!Recoverable(j,observed))throw new DnsException("DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.");
        if(!observed.CanChange)throw new DnsException("This adapter cannot be safely changed with the current DNS backend.");
        j=j with{Expected=observed,Pending=null,Phase=DnsPhase.Restoring};store.Save(j);
        foreach(var family in j.Families.Reverse()){
            EnsureExpected(j);
            if(j.Expected.Family(family).SameConfiguration(j.Before.Family(family)))continue;
            j=j with{Pending=family};store.Save(j);
            writer.Write(adapter,family,j.Before.Family(family));
            var expected=j.Expected.With(family,j.Before.Family(family));var actual=reader.Read(adapter);
            if(!actual.SameConfiguration(expected))throw new DnsException("Restoration verification failed. The recovery record has been preserved.");
            j=j with{Expected=actual,Pending=null};store.Save(j);
        }
        j=j with{Phase=DnsPhase.Restored};store.Save(j);return j;
    }
    public static bool Recoverable(DnsJournal j,DnsSnapshot current)
    {
        if(j.Expected.SameConfiguration(current))return true;
        if(j.Pending is not DnsFamily family)return false;
        var destination=j.Phase==DnsPhase.Restoring?j.Before:j.Target;
        return j.Expected.With(family,destination.Family(family)).SameConfiguration(current);
    }
    void EnsureExpected(DnsJournal j){if(!reader.Read(j.Before.AdapterId).SameConfiguration(j.Expected))throw new DnsException("DNS settings were changed outside Open Net. The operation was stopped.");}
    public static void Validate(DnsSnapshot before,DnsSnapshot target,DnsFamily[] families)
    {
        if(!before.CanChange)throw new DnsException(before.Limitation.Length>0?before.Limitation:"The original DNS configuration could not be determined safely.");
        if(before.IPv4.Mode==DnsMode.Unknown||before.IPv6.Mode==DnsMode.Unknown)throw new DnsException("The original DNS configuration could not be determined safely.");
        if(families.Length is <1 or >2||families.Distinct().Count()!=families.Length||families.Any(f=>!Enum.IsDefined(f)))throw new DnsException("Select IPv4 and/or IPv6 explicitly.");
        if(before.AdapterId!=target.AdapterId||before.MachineId!=target.MachineId||before.DependencyFingerprint!=target.DependencyFingerprint)throw new DnsException("The DNS plan does not match the original adapter configuration.");
        foreach(var family in Enum.GetValues<DnsFamily>()){
            var desired=target.Family(family);
            DnsProfiles.ValidateEncryption(before.Family(family));DnsProfiles.ValidateEncryption(desired);
            if(!families.Contains(family)){if(!before.Family(family).SameConfiguration(desired))throw new DnsException("The plan changes an unselected address family.");continue;}
            if(desired.Mode!=DnsMode.Manual||!DnsProfiles.Validate(string.Join(' ',desired.Servers),family).SequenceEqual(desired.Servers))throw new DnsException("Only validated manual DNS profiles can be applied in this alpha.");
            var active=before.Family(family).Encryption.Where(p=>(p.Flags&3)!=0).ToArray();
            if(active.Length>0&&(desired.Encryption.Count(p=>(p.Flags&3)!=0)!=desired.Servers.Length||active.Any(p=>(p.Flags&4)==0)&&desired.Encryption.Any(p=>(p.Flags&4)!=0)))throw new DnsException("The DNS plan would weaken encryption. Application is blocked.");
            if(active.Length>0&&(active.Length!=before.Family(family).Servers.Length||active.Select(p=>p.Flags&12).Distinct().Count()!=1||desired.Encryption.Any(p=>(p.Flags&12)!=(active[0].Flags&12))))throw new DnsException("This profile cannot preserve the adapter's DNS encryption policy.");
        }
    }
}

// File persistence is used only under a protected, fixed recovery root in production.
public sealed class DnsFileJournalStore : IDnsJournalStore
{
    readonly string root;readonly string owner;readonly Action<string>? protect;readonly Action<Guid>? validateRead;
    public DnsFileJournalStore(string root,string owner,Action<string>? protect=null,Action<Guid>? validateRead=null){this.root=root;this.owner=owner;this.protect=protect;this.validateRead=validateRead;}
    public DnsJournal? Load(Guid adapter){string path=Path.Combine(root,adapter.ToString("D")+".json");if(!File.Exists(path))return null;NetworkHub.Core.Paths.NoReparseAncestors(path);validateRead?.Invoke(adapter);if(new FileInfo(path).Length>131072)throw new InvalidDataException("Oversized DNS recovery record");var j=JsonSerializer.Deserialize<DnsJournal>(File.ReadAllText(path))??throw new InvalidDataException("Invalid DNS recovery record");if(j.Schema!=1||j.Owner!=owner||j.Before.AdapterId!=adapter||j.Target.AdapterId!=adapter||j.Expected.AdapterId!=adapter)throw new InvalidDataException("Invalid DNS recovery identity");return j;}
    public void Save(DnsJournal journal){Directory.CreateDirectory(root);string path=Path.Combine(root,journal.Before.AdapterId.ToString("D")+".json");NetworkHub.Core.Paths.NoReparseAncestors(path);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){protect?.Invoke(temporary);JsonSerializer.Serialize(file,journal);file.Flush(true);}File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
}
