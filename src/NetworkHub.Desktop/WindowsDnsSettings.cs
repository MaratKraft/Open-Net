using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using NetworkHub.Dns;

namespace NetworkHub.Desktop;

// Local inspection only: no DNS lookup, HTTP request, cache flush or configuration command.
// Registry is read to distinguish a DNS override from DHCP/RA's effective resolver list.
// Writes use the per-interface Windows API and a fixed netsh reset for the saved automatic mode.
internal sealed class WindowsDnsSettings : IDnsSettingsReader,IDnsSettingsWriter
{
    [StructLayout(LayoutKind.Sequential)] internal struct Settings {
        public uint Version;public ulong Flags;public IntPtr Domain,NameServer,SearchList;
        public uint RegistrationEnabled,RegisterAdapterName,EnableLlmnr,QueryAdapterName;public IntPtr ProfileNameServer;
        public uint DisableUnconstrainedQueries;public IntPtr SupplementalSearchList;
        public uint ServerCount;public IntPtr ServerProperties;public uint ProfileServerCount;public IntPtr ProfileServerProperties;
    }
    internal sealed class NativeWritePlan : IDisposable
    {
        internal Settings Value;readonly List<IntPtr> allocations=new();
        IntPtr Text(string text){var p=Marshal.StringToHGlobalUni(text);allocations.Add(p);return p;}
        IntPtr Allocate(int bytes){var p=Marshal.AllocHGlobal(bytes);allocations.Add(p);return p;}
        internal NativeWritePlan(DnsFamily family,DnsFamilySettings settings)
        {
            DnsProfiles.ValidateEncryption(settings);
            Value=new Settings{Version=(uint)(settings.Encryption.Length>0?3:1),Flags=2UL|(family==DnsFamily.IPv6?1UL:0),NameServer=Text(string.Join(',',settings.Servers))};
            if(settings.Encryption.Length==0)return;
            Value.Flags|=0x1000;Value.ServerCount=(uint)settings.Encryption.Length;Value.ServerProperties=Allocate(Marshal.SizeOf<ServerProperty>()*settings.Encryption.Length);
            for(int i=0;i<settings.Encryption.Length;i++){
                var item=settings.Encryption[i];var doh=new DohSettings{Flags=item.Flags,Template=item.Template==null?IntPtr.Zero:Text(item.Template)};var pointer=Allocate(Marshal.SizeOf<DohSettings>());Marshal.StructureToPtr(doh,pointer,false);
                var property=new ServerProperty{Version=1,ServerIndex=(uint)Array.FindIndex(settings.Servers,s=>string.Equals(s,item.Server,StringComparison.OrdinalIgnoreCase)),Type=1,Property=pointer};Marshal.StructureToPtr(property,IntPtr.Add(Value.ServerProperties,i*Marshal.SizeOf<ServerProperty>()),false);
            }
        }
        public void Dispose(){foreach(var pointer in allocations)Marshal.FreeHGlobal(pointer);allocations.Clear();}
    }
    [StructLayout(LayoutKind.Sequential)] internal struct ServerProperty {public uint Version,ServerIndex,Type;public IntPtr Property;}
    [StructLayout(LayoutKind.Sequential)] internal struct DohSettings {public IntPtr Template;public ulong Flags;}
    internal static DnsDohSettings[] CopyProperties(uint count,IntPtr pointer,string[] servers)
    {
        if(count>servers.Length||count>64||count>0&&pointer==IntPtr.Zero)throw new DnsException("Windows returned unsupported per-adapter DoH properties.");
        var result=new List<(uint Index,DnsDohSettings Value)>();
        for(int i=0;i<count;i++){
            var property=Marshal.PtrToStructure<ServerProperty>(IntPtr.Add(pointer,i*Marshal.SizeOf<ServerProperty>()));
            if(property.Version!=1||property.Type!=1||property.ServerIndex>=servers.Length||property.Property==IntPtr.Zero||result.Any(p=>p.Index==property.ServerIndex))throw new DnsException("Windows returned unsupported per-adapter DoH properties.");
            var doh=Marshal.PtrToStructure<DohSettings>(property.Property);result.Add((property.ServerIndex,new(servers[property.ServerIndex],doh.Template==IntPtr.Zero?null:Value(doh.Template),doh.Flags)));
        }
        var values=result.OrderBy(p=>p.Index).Select(p=>p.Value).ToArray();DnsProfiles.ValidateEncryption(new(DnsMode.Manual,servers,servers){Encryption=values});return values;
    }
    [DllImport("iphlpapi.dll",ExactSpelling=true)] static extern uint GetInterfaceDnsSettings(Guid id,ref Settings settings);
    [DllImport("iphlpapi.dll",ExactSpelling=true)] static extern uint SetInterfaceDnsSettings(Guid id,ref Settings settings);
    [DllImport("iphlpapi.dll",ExactSpelling=true)] static extern void FreeInterfaceDnsSettings(ref Settings settings);
    static string Value(IntPtr pointer)=>pointer==IntPtr.Zero?"":Marshal.PtrToStringUni(pointer)??"";
    static string InterfaceKey(Guid id,bool v6)=>@"SYSTEM\CurrentControlSet\Services\"+(v6?"Tcpip6":"Tcpip")+@"\Parameters\Interfaces\"+id.ToString("B");
    public IReadOnlyList<DnsAdapter> Adapters()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>Guid.TryParse(n.Id,out _)&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).Select(n=>new DnsAdapter(Guid.Parse(n.Id),n.Name,n.NetworkInterfaceType.ToString(),n.OperationalStatus==OperationalStatus.Up,IsVirtual(n))).OrderBy(n=>n.Virtual).ThenByDescending(n=>n.Connected).ThenBy(n=>n.Name).ToArray();
    static bool IsVirtual(NetworkInterface n)=>n.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp || new[]{"virtual","vpn","wintun","wireguard","hyper-v","tap-","loopback"}.Any(word=>(n.Description+" "+n.Name).Contains(word,StringComparison.OrdinalIgnoreCase));
    static NetworkInterface Find(Guid id)=>NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n=>Guid.TryParse(n.Id,out var found)&&found==id)??throw new DnsException("The selected adapter is no longer available.");
    static string Machine(){using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");string value=key?.GetValue("MachineGuid") as string??throw new DnsException("The machine identity could not be determined.");return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));}
    public DnsSnapshot Read(Guid id)
    {
        var n=Find(id);var ip=n.GetIPProperties();var effective=ip.DnsAddresses.Select(x=>x.ToString()).ToArray();
        var v4=ReadFamily(id,false,effective);var v6=ReadFamily(id,true,effective);bool supported=Environment.OSVersion.Version.Build>=19041;string limitation="";
        string native="";
        if(supported){
            // Version 3 exposes per-interface DoH properties on Windows 11. Unsupported versions
            // are not downgraded silently on a system that should support encryption inspection.
            var settings=new Settings{Version=(uint)(Environment.OSVersion.Version.Build>=22000?3:1)};
            uint error=GetInterfaceDnsSettings(id,ref settings);
            if(error!=0){supported=false;limitation="Windows DNS settings could not be inspected safely.";}
            else try{
                if(settings.Version==3){
                    var servers=Parse(Value(settings.NameServer),DnsFamily.IPv4);
                    v4=v4 with{Encryption=CopyProperties(settings.ServerCount,settings.ServerProperties,servers)};
                    if(settings.ProfileServerCount!=0){supported=false;limitation="Profile-specific DoH overrides cannot be restored by this backend.";}
                }
                if(Value(settings.ProfileNameServer).Length>0){supported=false;limitation="Profile-specific DNS overrides are unsupported in this alpha.";}
                native=string.Join('|',Value(settings.Domain),Value(settings.SearchList),settings.RegistrationEnabled,settings.RegisterAdapterName,settings.EnableLlmnr,settings.QueryAdapterName,Value(settings.ProfileNameServer),settings.DisableUnconstrainedQueries,Value(settings.SupplementalSearchList));
                if(v4.Mode==DnsMode.Manual&&!Parse(Value(settings.NameServer),DnsFamily.IPv4).SequenceEqual(v4.Servers,StringComparer.OrdinalIgnoreCase)){supported=false;limitation="The original DNS configuration could not be determined safely.";}
            }finally{FreeInterfaceDnsSettings(ref settings);}
        }else limitation="DNS changes require Windows 10 build 19041 or newer.";
        string dependencies=DependencyState(id,out bool policy,out bool encrypted);
        if(encrypted){supported=false;limitation="Active DDR, DoT or global DNS encryption settings cannot be restored by this backend.";}
        try{
            var scope=ReadInterfaceEncryption(id,v4,v6);
            if(!v4.SameConfiguration(v4 with{Encryption=scope.IPv4})){supported=false;limitation="Windows API and adapter-specific IPv4 DoH settings disagree. Application is blocked.";}
            else v4=v4 with{Encryption=scope.IPv4}; // Preserve verified disabled properties in backups.
            v6=v6 with{Encryption=scope.IPv6};
            if(v6.Encryption.Any(p=>(p.Flags&3)!=0)){supported=false;limitation="Active IPv6 DoH cannot be independently verified by this Windows API. IPv6 settings are preserved; application is blocked.";}
        }catch(DnsException ex){supported=false;limitation=ex.Message;}
        if(policy){supported=false;limitation="Managed DNS policies are present. DNS changes are blocked in this alpha.";}
        if(v4.Mode==DnsMode.Unknown||v6.Mode==DnsMode.Unknown){supported=false;limitation="The original IPv4 or IPv6 DNS assignment mode could not be determined safely.";}
        if(IsVirtual(n)){supported=false;limitation="VPN and virtual adapters are read-only in this alpha.";}
        if(!n.Supports(NetworkInterfaceComponent.IPv4)){supported=false;limitation="An IPv4-capable physical adapter is required in this alpha.";}
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(native+dependencies)));
        if(n.OperationalStatus!=OperationalStatus.Up){supported=false;limitation="The selected adapter is disconnected. Reconnect it before changing DNS.";}
        return new(id,n.Name,Machine(),v4,v6,hash,supported,limitation){SupportsDoh=Environment.OSVersion.Version.Build>=22000};
    }
    static string[] Parse(string raw,DnsFamily family)=>raw.Split(new[]{',',';',' ','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries).Select(value=>System.Net.IPAddress.Parse(value)).Where(value=>value.AddressFamily==(family==DnsFamily.IPv4?System.Net.Sockets.AddressFamily.InterNetwork:System.Net.Sockets.AddressFamily.InterNetworkV6)).Select(value=>value.ToString()).ToArray();
    static DnsFamilySettings ReadFamily(Guid id,bool v6,string[] effective)
    {
        var family=v6?DnsFamily.IPv6:DnsFamily.IPv4;var observed=effective.Where(value=>System.Net.IPAddress.Parse(value).AddressFamily==(v6?System.Net.Sockets.AddressFamily.InterNetworkV6:System.Net.Sockets.AddressFamily.InterNetwork)).ToArray();
        using var key=Registry.LocalMachine.OpenSubKey(InterfaceKey(id,v6));
        // Missing family key or an unexpected value type is unknown, never guessed as automatic.
        if(key==null)return new(DnsMode.Unknown,Array.Empty<string>(),observed);
        object? raw=key.GetValue("NameServer",null,RegistryValueOptions.DoNotExpandEnvironmentNames);
        if(raw==null||raw is string text&&string.IsNullOrWhiteSpace(text))return new(DnsMode.Automatic,Array.Empty<string>(),observed);
        if(raw is not string servers)return new(DnsMode.Unknown,Array.Empty<string>(),observed);
        try{var parsed=Parse(servers,family);if(parsed.Length==0)return new(DnsMode.Unknown,Array.Empty<string>(),observed);return new(DnsMode.Manual,parsed,observed);}catch(FormatException){return new(DnsMode.Unknown,Array.Empty<string>(),observed);}
    }
    static string ReadTree(string path)
    {
        using var key=Registry.LocalMachine.OpenSubKey(path);if(key==null)return "";var result=new StringBuilder();
        void Walk(RegistryKey k,int depth){if(depth>8)throw new DnsException("DNS policy inspection exceeded the supported limits.");foreach(string name in k.GetValueNames().OrderBy(x=>x,StringComparer.Ordinal)){object? value=k.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);result.Append(k.Name).Append('/').Append(name).Append('=').Append(value is byte[] bytes?Convert.ToHexString(bytes):value is string[] strings?string.Join('\n',strings):Convert.ToString(value,CultureInfo.InvariantCulture)).Append('\n');}foreach(string name in k.GetSubKeyNames().OrderBy(x=>x,StringComparer.Ordinal)){using var child=k.OpenSubKey(name)!;Walk(child,depth+1);}if(result.Length>262144)throw new DnsException("DNS policy inspection exceeded the supported limits.");}
        Walk(key,0);return result.ToString();
    }
    static string DependencyState(Guid id,out bool policy,out bool encrypted)
    {
        string policies=ReadTree(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient")+ReadTree(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig");
        policy=policies.Length>0;
        string scope=@"SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\"+id.ToString("B");
        // Per-server adapter DoH is controlled configuration in the snapshot, not an immutable
        // dependency. Hashing it here would make every legitimate DoH update look foreign.
        string encryption=ReadTree(scope+@"\DohProfileSettings");
        using(var root=Registry.LocalMachine.OpenSubKey(scope)){
            if(root!=null){foreach(string child in root.GetSubKeyNames().Where(s=>s!="DohInterfaceSettings"&&s!="DohProfileSettings").OrderBy(s=>s,StringComparer.Ordinal))encryption+=ReadTree(scope+"\\"+child);foreach(string name in root.GetValueNames())encryption+=name+"="+Convert.ToString(root.GetValue(name),CultureInfo.InvariantCulture)+"\n";}
        }
        // Unknown DDR/DoT payloads are conservatively unsupported, not reported as proof
        // of observed encrypted traffic. No packet or connection probes are performed.
        encrypted=encryption.Contains("Ddr",StringComparison.OrdinalIgnoreCase)||encryption.Contains("Dot",StringComparison.OrdinalIgnoreCase);
        using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters")){
            foreach(string name in new[]{"EnableAutoDoh","EnableDoh","DefaultDohMode","EnableDdr","EnableDot"}){
                object? raw=key?.GetValue(name);if(raw==null)continue;encryption+=name+"="+Convert.ToString(raw,CultureInfo.InvariantCulture)+"\n";
                if(raw is not (int or long)||Convert.ToInt64(raw,CultureInfo.InvariantCulture)!=0)encrypted=true;
            }
        }
        // Include the encryption catalog in comparison, even though we never edit it.
        return policies+encryption+ReadTree(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers");
    }
    static (DnsDohSettings[] IPv4,DnsDohSettings[] IPv6) ReadInterfaceEncryption(Guid id,DnsFamilySettings v4,DnsFamilySettings v6)
    {
        string path=@"SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\"+id.ToString("B")+@"\DohInterfaceSettings";
        using(var root=Registry.LocalMachine.OpenSubKey(path))if(root!=null&&(root.GetValueNames().Length!=0||root.GetSubKeyNames().Any(n=>n!="Doh"&&n!="Doh6")))throw new DnsException("Unknown adapter encryption settings cannot be preserved safely.");
        DnsDohSettings[] Read(string kind,DnsFamilySettings family){
            using var root=Registry.LocalMachine.OpenSubKey(path+"\\"+kind);if(root==null)return Array.Empty<DnsDohSettings>();
            if(root.GetValueNames().Length>0)throw new DnsException("Unknown adapter encryption settings cannot be preserved safely.");
            var result=new List<DnsDohSettings>();foreach(string server in family.Servers){using var entry=root.OpenSubKey(server);if(entry==null)continue;object? raw=entry.GetValue("DohFlags");long flags=raw is long qword?qword:raw is int dword?dword:-1;if(entry.GetSubKeyNames().Length>0||entry.GetValueNames().Any(n=>n!="DohFlags"&&n!="DohTemplate")||flags<0||entry.GetValue("DohTemplate") is not (null or string))throw new DnsException("Unknown adapter encryption settings cannot be preserved safely.");result.Add(new(server,entry.GetValue("DohTemplate") as string,(ulong)flags));}
            var values=result.ToArray();DnsProfiles.ValidateEncryption(family with{Encryption=values});return values;
        }
        return(Read("Doh",v4),Read("Doh6",v6));
    }
    public static void ValidateDestination(string[] servers)
    {
        // Registration alone is not active adapter DoH. Global automatic upgrades remain
        // a separate capability restriction; the catalog is never changed by this module.
        foreach(string server in servers){using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers\"+server);if(key?.GetValue("AutoUpgrade") is int flag&&flag!=0)throw new DnsException("This resolver has a global automatic encryption upgrade. Its effective mode cannot be restored safely.");}
    }
    public void Write(Guid id,DnsFamily family,DnsFamilySettings value)
    {
        if(!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
        if(!Enum.IsDefined(family)||value.Mode==DnsMode.Unknown)throw new InvalidOperationException("Invalid DNS operation");
        var n=Find(id);
        if(!Read(id).CanChange)throw new DnsException("This adapter cannot be safely changed with the current DNS backend.");
        if(value.Mode==DnsMode.Automatic){
            if(value.Encryption.Length>0)throw new DnsException("Automatic DNS with adapter DoH cannot be restored by this backend.");
            int index=family==DnsFamily.IPv4?n.GetIPProperties().GetIPv4Properties().Index:n.GetIPProperties().GetIPv6Properties().Index;
            var plan=AutomaticResetPlan(index,family);using var process=Process.Start(plan)??throw new InvalidOperationException("DNS reset did not start");
            if(!process.WaitForExit(15000)){process.Kill();process.WaitForExit();throw new DnsException("The DNS operation timed out. Review the recovery record.");}
            if(process.ExitCode!=0)throw new DnsException("Windows rejected DNS restoration. The recovery record has been preserved.");
            return;
        }
        // Restore can include a user's existing local resolver, so it is not filtered as a new custom profile.
        foreach(string address in value.Servers)if(!System.Net.IPAddress.TryParse(address,out var parsed)||parsed.AddressFamily!=(family==DnsFamily.IPv4?System.Net.Sockets.AddressFamily.InterNetwork:System.Net.Sockets.AddressFamily.InterNetworkV6))throw new InvalidOperationException("Invalid saved DNS address");
        using var nativePlan=new NativeWritePlan(family,value);uint error=SetInterfaceDnsSettings(id,ref nativePlan.Value);if(error!=0)throw new Win32Exception((int)error);
    }
    internal static ProcessStartInfo AutomaticResetPlan(int index,DnsFamily family)
    {
        if(index<=0||!Enum.IsDefined(family))throw new InvalidOperationException("Invalid DNS reset target");
        var plan=new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"netsh.exe")){UseShellExecute=false,CreateNoWindow=true};
        foreach(string value in new[]{"interface",family==DnsFamily.IPv4?"ipv4":"ipv6","set","dnsservers","name="+index.ToString(CultureInfo.InvariantCulture),"source=dhcp"})plan.ArgumentList.Add(value);return plan;
    }
}
