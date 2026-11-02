using System.Linq;
using NetworkHub.App;
using NetworkHub.Core;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;
internal static class DnsPresentation
{
    internal static bool Managed(DnsSnapshot? current,DnsJournal? journal)=>current!=null&&journal?.Active==true&&journal.Phase==DnsPhase.Applied&&journal.Expected.SameConfiguration(current);
    internal static string ProfileName(DnsJournal journal)=>journal.ProfileName.Length>0?journal.ProfileName:DnsProfiles.All.FirstOrDefault(p=>p.Id==journal.ProfileId)?.Name??L.T("DNS profile");
    internal static string StatusTone(DnsSnapshot? current,DnsJournal? journal)=>current==null?"Error":Managed(current,journal)?"Success":journal?.Active==true?"Warning":"Muted";
    internal static string CurrentProfile(DnsSnapshot? current,DnsJournal? journal)=>current==null?"":Managed(current,journal)?ProfileName(journal!):DnsProfiles.All.FirstOrDefault(p=>p.Id!="custom"&&p.IPv4.SequenceEqual(current.IPv4.Servers))?.Name??L.T(current.IPv4.Mode==DnsMode.Automatic?"Automatic":"Custom DNS");
    internal static string LineStatus(DnsSnapshot? current,DnsJournal? journal)=>current==null?L.T("Error"):Managed(current,journal)?L.T("Running"):journal?.Active==true?L.T(journal.Phase==DnsPhase.Restoring?"Restore failed":"Needs review"):current.IPv4.Mode==DnsMode.Automatic||!DnsProfiles.All.Any(p=>p.Id!="custom"&&p.IPv4.SequenceEqual(current.IPv4.Servers))?L.T("System DNS"):L.T("Configured outside Open Net");
    internal static string Status(DnsSnapshot? current,DnsJournal? journal)
    {
        if(current==null)return L.T("Error");
        if(Managed(current,journal))return L.T("Running")+" · "+ProfileName(journal!);
        if(journal?.Active==true)return L.T(journal.Phase==DnsPhase.Restoring?"Restore failed":"Needs review");
        if(current.IPv4.Mode==DnsMode.Automatic)return L.T("Turned off");
        var known=DnsProfiles.All.FirstOrDefault(p=>p.Id!="custom"&&p.IPv4.SequenceEqual(current.IPv4.Servers));
        return L.T("Configured outside Open Net")+(known==null?"":" · "+known.Name);
    }
}
 

