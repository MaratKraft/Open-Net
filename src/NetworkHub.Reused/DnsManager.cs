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
public enum DnsPhase { Prepared, Writing, Applied, Restoring, Restored, RecoveryRequired, RollingBack }
public sealed record DnsDohSettings(string Server,string? Template,ulong Flags);
public sealed record DnsFamilySettings(DnsMode Mode,string[] Servers,string[] EffectiveServers)
{
    public DnsDohSettings[] Encryption {get;init;}=Array.Empty<DnsDohSettings>();
    // Native readback can omit an empty, disabled DoH property. Preserve raw backup data.
    public DnsDohSettings[] ComparableEncryption()=>Encryption.Where(p=>p.Flags!=0||p.Template!=null).ToArray();
    public bool SameConfiguration(DnsFamilySettings other)=>Mode==other.Mode &&
        (Mode==DnsMode.Automatic || Servers.SequenceEqual(other.Servers,StringComparer.OrdinalIgnoreCase))&&ComparableEncryption().SequenceEqual(other.ComparableEncryption());
}
public sealed record DnsSnapshot(Guid AdapterId,string AdapterName,string MachineId,DnsFamilySettings IPv4,DnsFamilySettings IPv6,string DependencyFingerprint,bool CanChange,string Limitation)
{
    public bool SupportsDoh {get;init;}=true;
    public DnsFamilySettings Family(DnsFamily family)=>family==DnsFamily.IPv4?IPv4:IPv6;
    public DnsSnapshot With(DnsFamily family,DnsFamilySettings value)=>family==DnsFamily.IPv4?this with{IPv4=value}:this with{IPv6=value};
    public bool SameConfiguration(DnsSnapshot other)=>AdapterId==other.AdapterId&&MachineId==other.MachineId&&DependencyFingerprint==other.DependencyFingerprint&&IPv4.SameConfiguration(other.IPv4)&&IPv6.SameConfiguration(other.IPv6);
    public string Fingerprint()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{AdapterId,MachineId,DependencyFingerprint,V4=new{IPv4.Mode,Servers=IPv4.Mode==DnsMode.Manual?IPv4.Servers:Array.Empty<string>(),Encryption=IPv4.ComparableEncryption()},V6=new{IPv6.Mode,Servers=IPv6.Mode==DnsMode.Manual?IPv6.Servers:Array.Empty<string>(),Encryption=IPv6.ComparableEncryption()}}))));
}
public sealed record DnsAdapter(Guid Id,string Name,string Kind,bool Connected,bool Virtual);
public sealed record DnsProfile(string Id,string Name,string[] IPv4,string[] IPv6,string? Doh,string Source,string CheckedOn,bool Available,string Warning);
public static class DnsProfiles
{
    public static DnsFamilySettings ProfilePlan(DnsFamilySettings before,string[] servers,DnsProfile profile,bool useDoh,bool fallback)
    {
        var value=new DnsFamilySettings(DnsMode.Manual,servers,servers);
        if(!useDoh)return before.Encryption.Length==0?value:value with{Encryption=servers.Select(s=>new DnsDohSettings(s,null,0)).ToArray()};
        if(string.IsNullOrWhiteSpace(profile.Doh))throw new DnsException("Enter an HTTPS DoH endpoint or explicitly choose plain DNS.");
        value=value with{Encryption=servers.Select(s=>new DnsDohSettings(s,profile.Doh,2UL|(fallback?4UL:0))).ToArray()};ValidateEncryption(value);return value;
    }
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
        if(id=="custom")return;
        var profile=All.FirstOrDefault(p=>p.Id==id)??throw new DnsException("Invalid DNS profile identity");
        if(id!="custom"&&families.Any(f=>!target.Family(f).Servers.SequenceEqual(f==DnsFamily.IPv4?profile.IPv4:profile.IPv6)))throw new DnsException("DNS addresses do not match the selected profile.");
        if(id!="custom"&&families.SelectMany(f=>target.Family(f).Encryption).Any(p=>(p.Flags&3)!=0&&(p.Template!=profile.Doh||(p.Flags&2)==0)))throw new DnsException("DoH settings do not match the selected profile.");
    }
    public static IReadOnlyList<DnsProfile> All {get;}=new[]{
        new DnsProfile("comss","ComssDNS",new[]{"83.220.169.155","212.109.195.93"},Array.Empty<string>(),"https://dns.comss.one/dns-query","https://www.comss.ru/page.php?id=7315","2026-10-12",true,""),
        new DnsProfile("xbox","Xbox DNS",new[]{"111.88.96.54","111.88.96.55"},new[]{"2a00:ab00:1233:26::50","2a00:ab00:1233:26::51"},"https://xbox-dns.ru/dns-query","https://xbox-dns.ru/","2026-10-13",false,"The operator reports temporary Xbox DNS disruption. This is an availability warning, not a measurement of your connection. Apply only after reviewing the confirmation."),
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
public sealed class DnsException : Exception {public DnsException(string message,Exception? inner=null):base(message,inner){}}
public interface IDnsSettingsReader {IReadOnlyList<DnsAdapter> Adapters();DnsSnapshot Read(Guid adapter);}
public interface IDnsSettingsWriter {void Write(Guid adapter,DnsFamily family,DnsFamilySettings settings);}
public sealed record DnsJournal(int Schema,Guid Id,string Owner,DnsSnapshot Before,DnsSnapshot Expected,DnsSnapshot Target,DnsFamily[] Families,DnsPhase Phase,DnsFamily? Pending,string CreatedUtc,string Message)
{
    public string ProfileId {get;init;}="";
    public string ProfileName {get;init;}="";
    public string Action {get;init;}="apply";
    public DnsSnapshot? RollbackTarget {get;init;}
    public bool ContinuedSession {get;init;}
    public DnsFamily[] OperationFamilies {get;init;}=Array.Empty<DnsFamily>();
    public DnsHistory[] History {get;init;}=Array.Empty<DnsHistory>();
    public bool Active=>Phase!=DnsPhase.Restored;
}
public sealed record DnsHistory(string Utc,string Action,string ProfileName,DnsSnapshot Before,DnsSnapshot After,string Result);
public interface IDnsJournalStore {DnsJournal? Load(Guid adapter);void Save(DnsJournal journal);}
public interface IDnsArchiveStore : IDnsJournalStore {void Archive(DnsJournal journal);}

// Only this controller writes settings; its reader/writer/store are injected for isolated tests.
// Persistent intent precedes every write, including compensating writes.
public sealed class DnsTransaction
{
    readonly IDnsSettingsReader reader;readonly IDnsSettingsWriter writer;readonly IDnsJournalStore store;readonly string owner;readonly DnsOperationEvidence evidence;
    public DnsTransaction(IDnsSettingsReader reader,IDnsSettingsWriter writer,IDnsJournalStore store,string owner,DnsOperationEvidence? evidence=null){this.reader=reader;this.writer=writer;this.store=store;this.owner=owner;this.evidence=evidence??new();}
    public DnsJournal AcceptCurrent(DnsSnapshot approved)
    {
        if(store is not IDnsArchiveStore archive)throw new DnsException("Recovery archival is unavailable.");
        var current=reader.Read(approved.AdapterId);
        if(!current.CanChange||!current.SameConfiguration(approved))throw new DnsException("DNS settings changed before application. Refresh and review them again.");
        var old=store.Load(current.AdapterId);
        if(old!=null){if(old.Owner!=owner||old.Before.MachineId!=current.MachineId||old.Before.AdapterId!=current.AdapterId)throw new DnsException("The recovery record does not belong to this user or adapter.");archive.Archive(old);}
        if(!reader.Read(current.AdapterId).SameConfiguration(current))throw new DnsException("DNS settings changed before application. Refresh and review them again.");
        var next=new DnsJournal(1,Guid.NewGuid(),owner,current,current,current,new[]{DnsFamily.IPv4},DnsPhase.Restored,null,DateTime.UtcNow.ToString("O"),"Current settings accepted; previous recovery record archived."){Action="adopt"};
        store.Save(next);var saved=store.Load(current.AdapterId);
        if(saved==null||saved.Id!=next.Id||!saved.Before.SameConfiguration(current))throw new DnsException("The recovery record could not be verified.");return saved;
    }
    public DnsJournal Apply(DnsSnapshot approved,DnsSnapshot target,DnsFamily[] families,string profileId="",bool encryptionChangeApproved=false,string profileName="")=>Change(approved,target,families,profileId,profileName,"apply",encryptionChangeApproved);
    public DnsJournal Reapply(DnsSnapshot approved,DnsSnapshot target,DnsFamily[] families,string profileId,string profileName="")=>Change(approved,target,families,profileId,profileName,"reapply",true);
    public static void ValidateReapply(DnsSnapshot before,DnsSnapshot target,DnsFamily[] families){Validate(before,target,families,false,true);if(!before.SameConfiguration(target)||families.Any(f=>before.Family(f).Mode!=DnsMode.Manual))throw new DnsException("Repeat requires the selected manual DNS and DoH to match the current configuration exactly.");}
    public DnsJournal Automatic(DnsSnapshot approved)=>Change(approved,approved.With(DnsFamily.IPv4,new(DnsMode.Automatic,Array.Empty<string>(),Array.Empty<string>())),new[]{DnsFamily.IPv4},"","","automatic",true);
    DnsJournal Change(DnsSnapshot approved,DnsSnapshot target,DnsFamily[] families,string profileId,string profileName,string action,bool encryptionChangeApproved)
    {
        evidence.Begin(action,approved);
        Validate(approved,target,families,action=="automatic",encryptionChangeApproved);
        if(action=="reapply")ValidateReapply(approved,target,families);
        DnsProfiles.ValidateSelection(profileId,target,families);
        var previous=store.Load(approved.AdapterId);
        bool completedIntent=action=="reapply"&&previous!=null&&CanCompletePendingWrite(previous,approved);
        if(previous?.Active==true&&(previous.Owner!=owner||!completedIntent&&(previous.Phase!=DnsPhase.Applied||!previous.Expected.SameConfiguration(approved))))throw new DnsException("The managed DNS session needs recovery review before another change.");
        var current=reader.Read(approved.AdapterId);
        if(!current.CanChange||!current.SameConfiguration(approved))throw new DnsException("DNS settings changed before application. Refresh and review them again.");
        if(completedIntent){
            if(!CanCompletePendingWrite(previous!,current)||previous!.History.Length>=127)throw new DnsException("The managed DNS session needs recovery review before another change.");
            // Reconcile only a fully observed persisted write destination, preserving the first backup.
            previous=previous with{Expected=current,Pending=null,Phase=DnsPhase.Applied,History=previous.History.Append(new DnsHistory(DateTime.UtcNow.ToString("O"),previous.Action,previous.ProfileName,previous.RollbackTarget??previous.Expected,current,"verified after interrupted readback")).ToArray()};
        }
        if(action!="reapply"&&current.SameConfiguration(target)){evidence.WriteResult="not needed";evidence.VerificationResult="verified";evidence.AfterFingerprint=current.Fingerprint();evidence.ValuesChanged=false;return previous?.Active==true?previous:new DnsJournal(1,Guid.NewGuid(),owner,current,current,current,Array.Empty<DnsFamily>(),DnsPhase.Restored,null,DateTime.UtcNow.ToString("O"),"No DNS change was needed.");}
        bool continuing=previous?.Active==true;
        var j=continuing?previous! with{Expected=current,Target=target,Families=previous!.Families.Union(families).ToArray(),Phase=DnsPhase.Prepared,Pending=null}:new DnsJournal(1,Guid.NewGuid(),owner,current,current,target,families,DnsPhase.Prepared,null,DateTime.UtcNow.ToString("O"),"");
        j=j with{ProfileId=profileId,ProfileName=profileName,Action=action,RollbackTarget=current,ContinuedSession=continuing,OperationFamilies=families};
        if(j.History.Length>=128)throw new DnsException("Restore the current DNS session before starting another change.");
        if(!continuing&&previous!=null&&store is IDnsArchiveStore archive)archive.Archive(previous);
        store.Save(j); // No system write is allowed if the durable baseline could not be saved.
        var verified=store.Load(current.AdapterId);
        if(verified==null||JsonSerializer.Serialize(verified)!=JsonSerializer.Serialize(j))throw new DnsException("The saved DNS backup did not pass verification. No settings were changed.");
        try{
            foreach(var family in families){
                EnsureExpected(j);
                j=j with{Phase=DnsPhase.Writing,Pending=family};store.Save(j);
                evidence.RequestedWrites++;evidence.WriteResult="requested";
                writer.Write(current.AdapterId,family,target.Family(family));
                evidence.AcceptedWrites++;evidence.WriteResult="accepted";evidence.VerificationResult="requested";
                var expected=j.Expected.With(family,target.Family(family));
                var observed=reader.Read(current.AdapterId);
                if(!observed.SameConfiguration(expected))throw new DnsException("DNS verification failed. Review the recovery record before restoring.");
                evidence.VerifiedWrites++;evidence.VerificationResult="verified";evidence.AfterFingerprint=observed.Fingerprint();evidence.ValuesChanged=!current.SameConfiguration(observed);
                j=j with{Expected=observed,Pending=null};store.Save(j);
            }
            j=j with{Phase=DnsPhase.Applied,History=j.History.Append(new DnsHistory(DateTime.UtcNow.ToString("O"),action,profileName,current,j.Expected,"verified")).ToArray()};store.Save(j);return j;
        }catch(Exception original){
            if(evidence.WriteResult=="requested")evidence.WriteResult="failed";
            if(evidence.VerificationResult=="requested")evidence.VerificationResult="failed";
            try{
                var durable=store.Load(current.AdapterId)??throw new DnsException("The recovery record is missing.");
                var observed=reader.Read(current.AdapterId);
                if(!Recoverable(durable,observed)||!observed.CanChange)throw new DnsException("Foreign or uninspectable DNS changes prevent automatic rollback.");
                durable=durable with{Expected=observed,Phase=DnsPhase.RollingBack,Pending=null};store.Save(durable);
                foreach(var family in families.Reverse()){
                    EnsureExpected(durable);if(durable.Expected.Family(family).SameConfiguration(current.Family(family)))continue;
                    durable=durable with{Pending=family};store.Save(durable);writer.Write(current.AdapterId,family,current.Family(family));
                    var actual=reader.Read(current.AdapterId);var expected=durable.Expected.With(family,current.Family(family));
                    if(!actual.SameConfiguration(expected))throw new DnsException("Automatic rollback verification failed.");
                    durable=durable with{Expected=actual,Pending=null};store.Save(durable);
                }
                if(!durable.Expected.SameConfiguration(current))throw new DnsException("Automatic rollback verification failed.");
                durable=durable with{Phase=continuing?DnsPhase.Applied:DnsPhase.Restored,ProfileId=continuing?previous!.ProfileId:"",ProfileName=continuing?previous!.ProfileName:"",Action=continuing?previous!.Action:"restore",History=durable.History.Append(new DnsHistory(DateTime.UtcNow.ToString("O"),action,profileName,current,durable.Expected,"failed; rollback verified")).ToArray()};store.Save(durable);
                evidence.RecoveryResult="verified";
            }catch(Exception recovery){evidence.RecoveryResult="unverified";throw new DnsException("DNS change failed; automatic rollback could not be verified. The recovery record is preserved for offline recovery.",new AggregateException(original,recovery));}
            throw new DnsException("DNS change failed; the previous configuration was restored and verified.",original);
        }
    }

    public DnsJournal Restore(Guid adapter,string approvedFingerprint)
    {
        var j=store.Load(adapter)??throw new DnsException("No DNS backup exists for this adapter.");
        if(!j.Active){if(reader.Read(adapter).SameConfiguration(j.Before))return j;throw new DnsException("DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.");}
        if(j.Schema!=1||j.Owner!=owner||j.Before.AdapterId!=adapter)throw new DnsException("The recovery record does not belong to this user or adapter.");
        var observed=reader.Read(adapter);
        evidence.Begin("restore",observed);
        if(observed.Fingerprint()!=approvedFingerprint)throw new DnsException("DNS settings changed before restoration. Refresh and review them again.");
        if(!Recoverable(j,observed))throw new DnsException("DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.");
        if(!observed.CanChange)throw new DnsException("This adapter cannot be safely changed with the current DNS backend.");
        j=j with{Expected=observed,Pending=null,Phase=DnsPhase.Restoring};store.Save(j);
        foreach(var family in j.Families.Reverse()){
            EnsureExpected(j);
            if(j.Expected.Family(family).SameConfiguration(j.Before.Family(family)))continue;
            j=j with{Pending=family};store.Save(j);
            evidence.RequestedWrites++;evidence.WriteResult="requested";
            try{writer.Write(adapter,family,j.Before.Family(family));evidence.AcceptedWrites++;evidence.WriteResult="accepted";}catch{evidence.WriteResult="failed";throw;}
            evidence.VerificationResult="requested";
            var expected=j.Expected.With(family,j.Before.Family(family));var actual=reader.Read(adapter);
            if(!actual.SameConfiguration(expected)){evidence.VerificationResult="failed";throw new DnsException("Restoration verification failed. The recovery record has been preserved.");}
            evidence.VerifiedWrites++;evidence.VerificationResult="verified";evidence.AfterFingerprint=actual.Fingerprint();evidence.ValuesChanged=!observed.SameConfiguration(actual);
            j=j with{Expected=actual,Pending=null};store.Save(j);
        }
        j=j with{Phase=DnsPhase.Restored,Action="restore",History=j.History.Append(new DnsHistory(DateTime.UtcNow.ToString("O"),"restore","",observed,j.Expected,"verified")).ToArray()};store.Save(j);return j;
    }
    public static bool CanCompletePendingWrite(DnsJournal j,DnsSnapshot current)=>current.CanChange&&j.Phase==DnsPhase.Writing&&(j.Action is "apply" or "reapply")&&j.Pending is DnsFamily family&&j.Target.SameConfiguration(current)&&j.Expected.With(family,j.Target.Family(family)).SameConfiguration(current);
    public static bool Recoverable(DnsJournal j,DnsSnapshot current)
    {
        if(j.Expected.SameConfiguration(current))return true;
        if(j.Pending is not DnsFamily family)return false;
        var destination=j.Phase==DnsPhase.Restoring?j.Before:j.Phase==DnsPhase.RollingBack?j.RollbackTarget??j.Before:j.Target;
        return j.Expected.With(family,destination.Family(family)).SameConfiguration(current);
    }
    void EnsureExpected(DnsJournal j){if(!reader.Read(j.Before.AdapterId).SameConfiguration(j.Expected))throw new DnsException("DNS settings were changed outside Open Net. The operation was stopped.");}
    public static void Validate(DnsSnapshot before,DnsSnapshot target,DnsFamily[] families,bool automatic=false,bool encryptionChangeApproved=false)
    {
        if(!before.CanChange)throw new DnsException(before.Limitation.Length>0?before.Limitation:"The original DNS configuration could not be determined safely.");
        if(before.IPv4.Mode==DnsMode.Unknown||before.IPv6.Mode==DnsMode.Unknown)throw new DnsException("The original DNS configuration could not be determined safely.");
        if(families.Length is <1 or >2||families.Distinct().Count()!=families.Length||families.Any(f=>!Enum.IsDefined(f)))throw new DnsException("Select IPv4 and/or IPv6 explicitly.");
        if(before.AdapterId!=target.AdapterId||before.MachineId!=target.MachineId||before.DependencyFingerprint!=target.DependencyFingerprint)throw new DnsException("The DNS plan does not match the original adapter configuration.");
        foreach(var family in Enum.GetValues<DnsFamily>()){
            var desired=target.Family(family);
            DnsProfiles.ValidateEncryption(before.Family(family));DnsProfiles.ValidateEncryption(desired);
            if(desired.Encryption.Any(p=>(p.Flags&3)!=0)&&!before.SupportsDoh)throw new DnsException("This Windows version cannot apply native DoH. Plain IPv4 DNS remains available.");
            if(!families.Contains(family)){if(!before.Family(family).SameConfiguration(desired))throw new DnsException("The plan changes an unselected address family.");continue;}
            if(automatic){if(family!=DnsFamily.IPv4||desired.Mode!=DnsMode.Automatic||desired.Servers.Length!=0||desired.Encryption.Length!=0)throw new DnsException("Automatic DNS changes only IPv4 DNS assignment.");continue;}
            if(desired.Mode!=DnsMode.Manual||!DnsProfiles.Validate(string.Join(' ',desired.Servers),family).SequenceEqual(desired.Servers))throw new DnsException("Only validated manual DNS profiles can be applied in this alpha.");
            var active=before.Family(family).Encryption.Where(p=>(p.Flags&3)!=0).ToArray();
            if(!encryptionChangeApproved&&active.Length>0&&(desired.Encryption.Count(p=>(p.Flags&3)!=0)!=desired.Servers.Length||active.Any(p=>(p.Flags&4)==0)&&desired.Encryption.Any(p=>(p.Flags&4)!=0)))throw new DnsException("The DNS plan would weaken encryption. Application is blocked.");
            if(!encryptionChangeApproved&&active.Length>0&&(active.Length!=before.Family(family).Servers.Length||active.Select(p=>p.Flags&12).Distinct().Count()!=1||desired.Encryption.Any(p=>(p.Flags&12)!=(active[0].Flags&12))))throw new DnsException("This profile cannot preserve the adapter's DNS encryption policy.");
        }
    }
}

// File persistence is used only under a protected, fixed recovery root in production.
public sealed class DnsFileJournalStore : IDnsArchiveStore
{
    readonly string root;readonly string owner;readonly Action<string>? protect;readonly Action<Guid>? validateRead;
    public void Archive(DnsJournal journal)
    {
        string directory=Path.Combine(root,"Archives",journal.Id.ToString("N")+"-"+Guid.NewGuid().ToString("N"));NetworkHub.Core.Paths.NoReparseAncestors(directory);Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,journal.Before.AdapterId.ToString("D")+".json");byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(journal);
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){protect?.Invoke(path);file.Write(bytes);file.Flush(true);}
        if(!File.ReadAllBytes(path).SequenceEqual(bytes))throw new IOException("Recovery archive verification failed.");
    }
    public DnsFileJournalStore(string root,string owner,Action<string>? protect=null,Action<Guid>? validateRead=null){this.root=root;this.owner=owner;this.protect=protect;this.validateRead=validateRead;}
    public DnsJournal? Load(Guid adapter)
    {
        string path=Path.Combine(root,adapter.ToString("D")+".json");if(!File.Exists(path))return null;
        NetworkHub.Core.Paths.NoReparseAncestors(path);validateRead?.Invoke(adapter);
        if(new FileInfo(path).Length>1048576)throw new InvalidDataException("Oversized DNS recovery record");
        var j=JsonSerializer.Deserialize<DnsJournal>(File.ReadAllText(path))??throw new InvalidDataException("Invalid DNS recovery record");
        if(j.Schema!=1||j.Owner!=owner||j.Id==Guid.Empty||j.Before==null||j.Target==null||j.Expected==null||j.History==null||j.History.Length>129||!Enum.IsDefined(j.Phase)||j.Families==null||j.Families.Length is <1 or >2||j.Families.Distinct().Count()!=j.Families.Length||j.Families.Any(f=>!Enum.IsDefined(f))||j.Pending.HasValue&&!j.Families.Contains(j.Pending.Value))throw new InvalidDataException("Invalid DNS recovery identity");
        foreach(var value in new[]{j.Before,j.Expected,j.Target,j.RollbackTarget}.Where(s=>s!=null)){
            if(value!.AdapterId!=adapter||value.MachineId!=j.Before.MachineId||value.DependencyFingerprint!=j.Before.DependencyFingerprint)throw new InvalidDataException("Inconsistent DNS recovery identity");
            foreach(var f in Enum.GetValues<DnsFamily>()){
                var settings=value.Family(f);
                if(settings==null||!Enum.IsDefined(settings.Mode)||settings.Mode==DnsMode.Unknown||settings.Servers==null||settings.Encryption==null||settings.Mode==DnsMode.Automatic&&(settings.Servers.Length!=0||settings.Encryption.Length!=0)||settings.Mode==DnsMode.Manual&&settings.Servers.Length is <1 or >64)throw new InvalidDataException("Invalid DNS recovery configuration");
                DnsProfiles.ValidateEncryption(settings);
                foreach(string server in settings.Servers)if(!IPAddress.TryParse(server,out var ip)||ip.AddressFamily!=(f==DnsFamily.IPv4?AddressFamily.InterNetwork:AddressFamily.InterNetworkV6))throw new InvalidDataException("Invalid saved DNS address");
            }
        }
        return j;
    }
    public void Save(DnsJournal journal){byte[] payload=JsonSerializer.SerializeToUtf8Bytes(journal);if(payload.Length>1048576)throw new InvalidDataException("DNS session history exceeded the safe record size");Directory.CreateDirectory(root);string path=Path.Combine(root,journal.Before.AdapterId.ToString("D")+".json");NetworkHub.Core.Paths.NoReparseAncestors(path);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){protect?.Invoke(temporary);file.Write(payload);file.Flush(true);}File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
}
