using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetworkHub.Core;
namespace NetworkHub.Dns;
public sealed record SavedDnsProfile(string Id,string Name,string[] IPv4,string[] IPv6,string? Doh,bool UseDoh,bool Fallback);
public sealed class DnsProfileStore
{
    readonly string path;
    public DnsProfileStore(string data)=>path=Paths.Owned(data,"Config/DnsProfiles.json");
    public SavedDnsProfile[] Load(){if(!File.Exists(path))return Array.Empty<SavedDnsProfile>();Paths.NoReparseAncestors(path);if(new FileInfo(path).Length>65536)throw new InvalidDataException("DNS profile file is too large");var values=JsonSerializer.Deserialize<SavedDnsProfile[]>(File.ReadAllText(path))??throw new InvalidDataException("Invalid DNS profiles");if(values.Length>32||values.Select(p=>p.Id).Distinct().Count()!=values.Length)throw new InvalidDataException("Invalid DNS profiles");foreach(var value in values)Validate(value);return values;}
    public static void Validate(SavedDnsProfile value){if(!Guid.TryParseExact(value.Id,"N",out _)||string.IsNullOrWhiteSpace(value.Name)||value.Name.Length>80||value.Name.Any(char.IsControl))throw new DnsException("Enter a profile name of up to 80 characters.");var servers=DnsProfiles.Validate(string.Join(' ',value.IPv4),DnsFamily.IPv4);if(servers.Length>2)throw new DnsException("Enter one preferred and optionally one alternative IPv4 DNS address.");if(!servers.SequenceEqual(value.IPv4))throw new DnsException("Invalid profile addresses.");if(value.IPv6.Length>0&&!DnsProfiles.Validate(string.Join(' ',value.IPv6),DnsFamily.IPv6).SequenceEqual(value.IPv6))throw new DnsException("Invalid profile addresses.");DnsProfiles.ProfilePlan(new(DnsMode.Automatic,Array.Empty<string>(),Array.Empty<string>()),servers,new("custom",value.Name,servers,value.IPv6,value.Doh,"","",true,""),value.UseDoh||!string.IsNullOrWhiteSpace(value.Doh),value.Fallback);}
    public void Save(SavedDnsProfile profile){Validate(profile);var values=Load().Where(p=>p.Id!=profile.Id).Append(profile).ToArray();if(values.Length>32)throw new DnsException("At most 32 custom DNS profiles can be saved.");Directory.CreateDirectory(Path.GetDirectoryName(path)!);Paths.NoReparseAncestors(path);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
}
