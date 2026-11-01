using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;
internal sealed class HbDnsController
{
 readonly Shell shell;readonly AppFiles files;internal readonly IDnsSettingsReader Reader;readonly DnsProfileStore custom;
 internal DnsSnapshot? Current {get;private set;}
 internal DnsProfile[] Profiles=>SimpleDns.Presets.Concat(custom.Load().Select(ToProfile)).ToArray();
 internal static DnsProfile ToProfile(SavedDnsProfile p)=>new("hb-custom:"+p.Id,p.Name,p.IPv4,Array.Empty<string>(),null,"","",true,"");
 internal HbDnsController(Shell shell,AppFiles files){this.shell=shell;this.files=files;custom=new(files.Data,"HbDnsProfiles.json");
#if OPENNET_TESTING
 Reader=shell.Runtime.TestMode?ReaderForTests??TestsDns.Fixture(files.Data):new WindowsDnsSettings();
#else
 Reader=new WindowsDnsSettings();
#endif
 }
#if OPENNET_TESTING
 internal static IDnsSettingsReader? ReaderForTests;
 internal static Func<string,Exception?>? FaultForTests;
#endif
 internal DnsAdapter[] Adapters=>Reader.Adapters().Where(a=>a.Kind is "Ethernet" or "Wireless80211" or "Wi-Fi"&&!a.Virtual).ToArray();
 internal void Read(){_ =Profiles;var adapters=Adapters;var selected=adapters.FirstOrDefault(a=>a.Id.ToString("D")==shell.Settings.HbDnsAdapterId);if(shell.Settings.HbDnsAdapterId.Length==0&&adapters.Count(a=>a.Connected)==1)selected=adapters.Single(a=>a.Connected);Current=selected?.Connected==true?Reader.Read(selected.Id):null;}
 internal DnsProfile? Active=>SimpleDns.Match(Current,Profiles);
 internal IDnsJournalStore EmergencyStore=>shell.Runtime.TestMode?new DnsFileJournalStore(Path.Combine(files.Data,"Dns/HbEmergency"),"safe-test"):DnsRecoveryStore.Open(DnsRecoveryStore.Owner(),false);
 internal async Task<DnsResponse> Change(DnsSnapshot before,DnsSnapshot target,DnsProfile? profile,bool allowPlain=false,bool cacheOnly=false)
 {
#if OPENNET_TESTING
 if(shell.Runtime.TestMode){await Task.Yield();if(FaultForTests?.Invoke(cacheOnly?"flush":profile==null?"off":"on") is Exception fault)throw fault;if(cacheOnly)return new(true,DnsCache.Clear(true));SimpleDns.Change(Reader,(IDnsSettingsWriter)Reader,EmergencyStore,"safe-test",before,target,profile==null);return new(true,profile==null?"Automatic IPv4 DNS verified.":"DNS addresses verified. Service availability has not been tested.");}
#endif
 using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);SavedDnsProfile? saved=profile?.Id.StartsWith("hb-custom:")==true?custom.Load().Single(p=>"hb-custom:"+p.Id==profile.Id):null;
 var request=new DnsRequest(Guid.NewGuid().ToString("N"),process.Id,identity.StartFileTime,DnsRecoveryStore.Owner(),shell.Settings.Language,cacheOnly?"flush":profile==null?"hb-off":"hb-on",before,target,cacheOnly?Array.Empty<DnsFamily>():SimpleDns.Families,before.Fingerprint()){ProfileId=profile?.Id??"",ProfileName=profile?.Name??"",CustomProfile=saved,ClearCache=false,EncryptionChangeApproved=allowPlain};
 return await DnsElevated.Execute(files,request,false);
 }
 internal void Save(SavedDnsProfile value)=>custom.Save(value);
 internal void Delete(string id)=>custom.Delete(id);
}
