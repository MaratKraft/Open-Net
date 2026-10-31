using System;
using System.Linq;
namespace NetworkHub.Dns;
// HB-style assignment policy; the transactional engine below is used only for crash-safe writes.
public static class SimpleDns
{
 public static readonly DnsFamily[] Families={DnsFamily.IPv4};
 public static readonly DnsProfile[] Presets={
  new("hb:comss","Comss.one",new[]{"94.250.253.55","212.109.195.93"},Array.Empty<string>(),"https://dns.comss.one/dns-query","https://www.comss.ru/page.php?id=7315","2026-10-27",true,""),
  new("hb:geo","GeoHide EU",new[]{"143.20.64.192","45.13.237.213"},Array.Empty<string>(),null,"https://github.com/neiroseti-ai/hb-ai-xbox-dns/blob/c3a85a5e05aae3d866271770fe7cf240d742a8d5/XboxDns.cs","2026-10-27",true,"GeoHide addresses are from HB. The operator catalogue could not be independently verified."),
  new("hb:magic","MagicDNS",new[]{"109.120.132.43","94.249.177.102"},Array.Empty<string>(),"https://dns.magicdns.ru/dns-query","https://magicdns.ru/","2026-10-27",true,""),
  new("hb:astra","Astracat",new[]{"85.209.2.112"},Array.Empty<string>(),"https://frd4wvnobp.cloudflare-gateway.com/dns-query","https://astracat2022.github.io/dns/","2026-10-27",true,""),
  new("hb:xbox","Xbox DNS",new[]{"111.88.96.54","111.88.96.55"},Array.Empty<string>(),"https://xbox-dns.ru/dns-query","https://xbox-dns.ru/","2026-10-27",false,"The Xbox DNS operator currently reports an outage.")};
 public static bool Eligible(DnsAdapter a)=>a.Connected&&!a.Virtual&&(a.Kind=="Ethernet"||a.Kind=="Wireless80211"||a.Kind=="Wi-Fi");
 public static SavedDnsProfile Custom(string id,string name,string first,string second){if(first.Trim().Length==0)throw new DnsException("Enter a preferred IPv4 DNS address.");string[] values=second.Trim().Length==0?new[]{first.Trim()}:new[]{first.Trim(),second.Trim()};foreach(string value in values){if(value.Split('.').Length!=4||!System.Net.IPAddress.TryParse(value,out var ip)||ip.ToString()!=value)throw new DnsException("Enter valid dotted IPv4 DNS addresses.");}var addresses=DnsProfiles.Validate(string.Join(' ',values),DnsFamily.IPv4);var saved=new SavedDnsProfile(id,name.Trim(),addresses,Array.Empty<string>(),null,false,false);DnsProfileStore.Validate(saved);return saved;}
 public static DnsProfile? Match(DnsSnapshot? state,System.Collections.Generic.IEnumerable<DnsProfile> profiles)=>state?.IPv4.Mode==DnsMode.Manual?profiles.FirstOrDefault(p=>p.IPv4.SequenceEqual(state.IPv4.Servers,StringComparer.OrdinalIgnoreCase)):null;
 public static DnsSnapshot Plan(DnsSnapshot before,DnsProfile? profile,bool allowPlain=false)
 {
  if(profile==null)return before.With(DnsFamily.IPv4,new(DnsMode.Automatic,Array.Empty<string>(),Array.Empty<string>()));
  if(!profile.Available)throw new DnsException(profile.Warning);
  var addresses=DnsProfiles.Validate(string.Join(' ',profile.IPv4),DnsFamily.IPv4);if(addresses.Length>2)throw new DnsException("Enter one preferred and optionally one alternative IPv4 DNS address.");
  DnsFamilySettings target;
  try{target=DnsProfiles.ManualPlan(before.IPv4,addresses,profile);}catch(DnsException)when(allowPlain){target=DnsProfiles.ProfilePlan(before.IPv4,addresses,profile,false,false);}
  return before.With(DnsFamily.IPv4,target);
 }
 public static DnsJournal Change(IDnsSettingsReader reader,IDnsSettingsWriter writer,IDnsJournalStore store,string owner,DnsSnapshot before,DnsSnapshot target,bool off,DnsOperationEvidence? evidence=null)
 {
  if(!reader.Adapters().Any(a=>a.Id==before.AdapterId&&Eligible(a)))throw new DnsException("Choose a connected physical adapter. VPN and virtual interfaces are excluded.");
  DnsTransaction.Validate(before,target,Families,off,true);
  var old=store.Load(before.AdapterId);
  if(old?.Active==true&&old.Phase!=DnsPhase.Applied)throw new DnsException("An interrupted DNS operation needs offline recovery. Its backup has been kept.");
  var tx=new DnsTransaction(reader,writer,store,owner,evidence);tx.AcceptCurrent(before);
  if(off)return tx.Automatic(before);
  return before.SameConfiguration(target)?tx.Reapply(before,target,Families,"",""):tx.Apply(before,target,Families,"",true,"");
 }
}
