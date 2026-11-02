using System;
using System.IO;
using System.Text.Json;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;

// Explicit local read-only diagnostic entry point. No writer, probes, UAC or recovery store.
internal static class DnsDiagnostics
{
    internal static int Run(string[] args)
    {
        if(args.Length!=2||!Guid.TryParse(args[1],out var adapter))return 2;
        var before=new WindowsDnsSettings().Read(adapter);var profile=DnsProfiles.All[0];DnsSnapshot? target=null;string? limitation=null;
        try{target=before.With(DnsFamily.IPv4,DnsProfiles.ProfilePlan(before.IPv4,profile.IPv4,profile,true,false));DnsTransaction.Validate(before,target,new[]{DnsFamily.IPv4},false,true);WindowsDnsSettings.ValidateDestination(profile.IPv4);}catch(DnsException ex){limitation=ex.Message;}
        string root=Path.GetDirectoryName(Environment.ProcessPath!)!,directory=Path.Combine(root,"test-results","dns-diagnostic");NetworkHub.Core.Paths.NoReparseAncestors(directory);Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"read-only.json"),JsonSerializer.Serialize(new{ReadOnly=true,Windows=Environment.OSVersion.Version.ToString(),Before=before,Target=target,AutomaticTarget=before.With(DnsFamily.IPv4,new(DnsMode.Automatic,Array.Empty<string>(),Array.Empty<string>())),CanApply=limitation==null,Limitation=limitation,Restore=before.IPv4,UnchangedIPv6=before.IPv6},new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
}
