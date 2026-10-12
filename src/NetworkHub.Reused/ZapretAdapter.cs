using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetworkHub.Core;

namespace NetworkHub.Adapters;
public static class ReviewedStrategy
{
    public const string Digest = "2C0723BE1D3539A70392607B9CF2EF590EFCCEEC8E6139B1A0D2984EA1DA1947";
    public static readonly IReadOnlyDictionary<string,string> Digests = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
        ["general.bat"]=Digest,
        ["general (ALT13).bat"]="0EF7279F222CED580F002C4073CC431307A368D6A70016489FC09E7A1741B0F8"
    };
    public static bool Supported(string bundle,string name,IReadOnlyDictionary<string,string>? trusted=null)
    {
        if(Path.GetFileName(name)!=name||!File.Exists(Path.Combine(bundle,name)))return false;
        string hash=Metadata.Hash(Path.Combine(bundle,name));
        if(Digests.TryGetValue(name,out var digest)&&hash==digest)return true;
        if(trusted==null||!trusted.TryGetValue(name,out var expected)||hash!=expected)return false;
        try{_=Extract(File.ReadAllText(Path.Combine(bundle,name)),bundle,Path.Combine(bundle,"lists"));return true;}catch(InvalidDataException){return false;}
    }
    // This is an extractor for an exact reviewed file, NOT a BAT interpreter.
    public static string[] Extract(string text, string bundle, string ownLists,string tcp="12",string udp="12")
    {
        const string prefix = "start \"zapret: %~n0\" /min \"%BIN%winws.exe\" ";
        int start = text.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0 || text.IndexOf(prefix, start + prefix.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException("Expected one reviewed winws launch");
        var body = text.Substring(start + prefix.Length).Trim();
        body = Regex.Replace(body, @"\^\r?\n", " ");
        if (body.IndexOfAny(new[] {'&','|','<','>','!','^','\r','\n'}) >= 0) throw new InvalidDataException("Shell syntax rejected");
        var args = Tokenize(body);
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) throw new InvalidDataException("Only winws options supported");
            string option=arg.Split('=')[0];
            if(!KnownOptions.Contains(option))throw new InvalidDataException("Unsupported winws option: "+option);
            arg = arg.Replace("%GameFilterTCP%", tcp).Replace("%GameFilterUDP%", udp);
            arg = arg.Replace("%BIN%", Path.Combine(bundle, "bin") + Path.DirectorySeparatorChar)
                .Replace("%LISTS%", Path.Combine(bundle, "lists") + Path.DirectorySeparatorChar);
            if (arg.Contains('%')) throw new InvalidDataException("Unknown BAT expansion");
            int equals = arg.IndexOf('=');
            if (equals >= 0 && IsFileOption(arg.Substring(0, equals)))
            {
                var path = Path.GetFullPath(arg.Substring(equals + 1));
                if (!Paths.Within(path, bundle)) throw new InvalidDataException("Strategy path escapes bundle");
                if (new[] { "list-general-user.txt", "list-exclude-user.txt", "ipset-exclude-user.txt" }.Contains(Path.GetFileName(path)))
                    path = Paths.Owned(ownLists, Path.GetFileName(path));
                arg = arg.Substring(0, equals + 1) + path;
            }
            args[i] = arg;
        }
        return args;
    }
    public static bool IsFileOption(string key) => key == "--hostlist" || key == "--hostlist-exclude" || key == "--ipset" || key == "--ipset-exclude"
        || key == "--dpi-desync-split-seqovl-pattern" || (key.StartsWith("--dpi-desync-fake-", StringComparison.Ordinal)&&!key.EndsWith("-mod",StringComparison.Ordinal));
    static readonly HashSet<string> KnownOptions=new(StringComparer.Ordinal){"--dpi-desync-fake-http","--dpi-desync-fake-discord","--dpi-desync-fake-unknown","--dpi-desync-hostfakesplit-mod","--hostlist-domains","--ip-id","--new","--wf-tcp","--wf-udp","--filter-tcp","--filter-udp","--filter-l7","--hostlist","--hostlist-exclude","--ipset","--ipset-exclude","--dpi-desync","--dpi-desync-repeats","--dpi-desync-fooling","--dpi-desync-fake-quic","--dpi-desync-fake-tls","--dpi-desync-fake-unknown-udp","--dpi-desync-fake-stun","--dpi-desync-fake-dht","--dpi-desync-fake-tls-mod","--dpi-desync-split-pos","--dpi-desync-split-seqovl","--dpi-desync-split-seqovl-pattern","--dpi-desync-any-protocol","--dpi-desync-cutoff","--dpi-desync-autottl"};
    public static string[] Tokenize(string body)
    {
        var result = new List<string>(); var token = new System.Text.StringBuilder(); bool quoted = false;
        foreach (char c in body)
        {
            if (c == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(c) && !quoted) { if (token.Length > 0) { result.Add(token.ToString()); token.Clear(); } }
            else token.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unclosed quote");
        if (token.Length > 0) result.Add(token.ToString());
        return result.ToArray();
    }
}
public sealed class ZapretAdapter : IModuleAdapter
{
    private readonly HubConfig config;
    public string Id => "zapret";
    public ZapretAdapter(HubConfig config) => this.config = config;
    private string Lists => Paths.Owned(config.Workspace, "zapret/lists");
    private string[] Arguments(string strategy){var filter=NetworkHub.App.GameFilter.Read(config.ZapretDirectory);return ReviewedStrategy.Extract(File.ReadAllText(strategy),config.ZapretDirectory,Lists,filter.Tcp,filter.Udp);}
    private static readonly Dictionary<string,string> known = new() {
        ["winws.exe"] = "AFFB4F69D2EA302A7ABCCD5325D81826E140DDAE014F1E070BC4A6C0DD555188",
        ["WinDivert.dll"] = "C1E060EE19444A259B2162F8AF0F3FE8C4428A1C6F694DCE20DE194AC8D7D9A2",
        ["WinDivert64.sys"] = "8DA085332782708D8767BCACE5327A6EC7283C17CFB85E40B03CD2323A90DDC2",
        ["cygwin1.dll"] = "103104A52E5293CE418944725DF19E2BF81AD9269B9A120D71D39028E821499B"
    };
    public Check[] Validate()
    {
        var checks = new List<Check>();
        foreach (var entry in known)
        {
            string p = Path.Combine(config.ZapretDirectory, "bin", entry.Key);
            if (!File.Exists(p)) checks.Add(new("MissingBinary", true, p));
            else if (!string.Equals(Metadata.Hash(p),config.TrustedFiles.TryGetValue("bin/"+entry.Key,out var trusted)?trusted:entry.Value,StringComparison.OrdinalIgnoreCase)) checks.Add(new("UnsupportedBinary",true,entry.Key+": file does not match the verified official release"));
        }
        string strategy = Path.Combine(config.ZapretDirectory, Path.GetFileName(config.Strategy));
        if (!File.Exists(strategy)) checks.Add(new("MissingStrategy", true, strategy));
        else if (!ReviewedStrategy.Supported(config.ZapretDirectory,config.Strategy,config.TrustedFiles)) checks.Add(new("UnsupportedStrategy", true, "Strategy format is unsupported or differs from the verified official release"));
        if (!checks.Any(c => c.Error))
        {
            try
            {
                foreach (string arg in Arguments(strategy))
                {
                    int e = arg.IndexOf('=');
                    if (e < 0 || !ReviewedStrategy.IsFileOption(arg.Substring(0,e))) continue;
                    string p = arg.Substring(e+1);
                    if (!File.Exists(p)) checks.Add(new(Paths.Within(p, Lists) ? "OwnListWillBeCreated" : "MissingData", !Paths.Within(p, Lists), p));
                    else if (Path.GetExtension(p) == ".txt" && new FileInfo(p).Length == 0)
                        checks.Add(new("EmptyList", true, "Empty list requires explicit review: " + p));
                }
            }
            catch (Exception ex) { checks.Add(new("StrategyInvalid", true, ex.Message)); }
        }
        checks.Add(new("Scope", false, "GameFilter read as data; original BAT and service.bat never executed; general.bat and ALT13 reviewed"));
        return checks.Distinct().ToArray();
    }
    public LaunchPlan Prepare()
    {
        var errors = Validate().Where(x => x.Error).ToArray();
        if (errors.Length > 0) throw new InvalidDataException(string.Join("; ", errors.Select(x => x.Code + ": " + x.Message)));
        foreach (string file in new[] { "list-general-user.txt", "list-exclude-user.txt", "ipset-exclude-user.txt" })
        {
            string path = Paths.Owned(Lists, file);
            if (!File.Exists(path))
            {
                string original=Path.Combine(config.ZapretDirectory,"lists",file);
                Paths.WriteNew(path,File.Exists(original)?File.ReadAllText(original):file.StartsWith("ipset")?"203.0.113.113/32\n":"open-net-placeholder.invalid\n");
            }
        }
        string log = Paths.Owned(config.Workspace, "zapret/logs/stdout.log");
        return new(Id, Path.Combine(config.ZapretDirectory, "bin", "winws.exe"), Path.Combine(config.ZapretDirectory, "bin"),
            Arguments(Path.Combine(config.ZapretDirectory,config.Strategy)),
            log, true, "Graceful console stop unverified; job termination would be forced; execution blocked");
    }
    public ModuleStatus Status()
    {
        var checks = Validate(); var processes = ProcessObserver.Find("winws");
        var state = processes.Length > 0 ? ModuleState.ExternalInstance : !File.Exists(Path.Combine(config.ZapretDirectory,"bin","winws.exe")) ? ModuleState.NotInstalled
            : checks.Any(x => x.Code.StartsWith("Unsupported")) ? ModuleState.UpdatePending : checks.Any(x=>x.Error) ? ModuleState.Failed : ModuleState.Stopped;
        return new(Id, state, checks.Any(x=>x.Code=="UnsupportedBinary"||x.Code=="MissingBinary") ? "Unknown" : "v72.9 / Flowseal 1.10.3 (reviewed hashes)",
            processes.Length == 0 ? "No matching process observed" : "External/unknown ownership: " + string.Join(", ", processes.Select(x=>x.Pid)), "Not applicable", "Not tested", checks);
    }
}



