using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NetworkHub.Core;

public enum ModuleState { NotInstalled, Stopped, Starting, Running, Degraded, Stopping, Failed, ExternalInstance, UpdatePending }
public sealed record Check(string Code, bool Error, string Message);
public sealed record LaunchPlan(string Module, string Executable, string WorkingDirectory, string[] Arguments,
    string LogFile, bool RequiresElevation, string StopSupport)
{
    public LaunchPlan Redacted() => this with { Arguments = Arguments.Select(LogSafety.Redact).ToArray() };
    // Construction is pure; ModuleManager has no execution route.
    public System.Diagnostics.ProcessStartInfo CreateStartInfo()
    {
        string name = Path.GetFileName(Executable);
        if (!Path.IsPathFullyQualified(Executable) || !Path.GetExtension(Executable).Equals(".exe",StringComparison.OrdinalIgnoreCase)
            || new[] {"cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe"}.Contains(name,StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Only a direct executable plan is supported; no shells or BAT");
        var info = new System.Diagnostics.ProcessStartInfo(Executable) { UseShellExecute=false, WorkingDirectory=WorkingDirectory,
            RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
        foreach (string arg in Arguments) info.ArgumentList.Add(arg);
        return info;
    }
}
public sealed record ModuleStatus(string Module, ModuleState State, string Version, string ProcessHealth,
    string ListenerHealth, string FunctionalHealth, Check[] Checks);
public interface IModuleAdapter
{
    string Id { get; }
    Check[] Validate();
    LaunchPlan Prepare();
    ModuleStatus Status();
}
public sealed record OperationResult(string Module, string Action, bool Simulated, string Message, LaunchPlan? Plan);
public sealed class ModuleManager
{
    private readonly IModuleAdapter[] adapters;
    public ModuleManager(params IModuleAdapter[] adapters) => this.adapters = adapters;
    public IModuleAdapter[] Modules => adapters.ToArray();
    public OperationResult Mutate(string action, IModuleAdapter adapter)
    {
        if (action != "start" && action != "stop" && action != "restart") throw new ArgumentException("Unknown operation");
        // There is deliberately no process-launch or broker dependency in the stage-2 manager.
        return new(adapter.Id, action, true, "Stage 2: simulated only; no process or system changes.",
            null);
    }
}
// Typed boundary for a future one-shot authenticated helper. No arbitrary executable API.
public sealed record ZapretElevationRequest(string ReviewedBundleDigest, string ReviewedStrategyDigest);
public sealed class RestrictedElevationBroker
{
    public void Execute(ZapretElevationRequest request) => throw new NotSupportedException(
        "Stage 2: elevation execution disabled. Requires separate authorized experiment and authenticated helper implementation.");
}
public sealed class HubConfig
{
    public Dictionary<string,string> TrustedFiles {get;set;}=new(StringComparer.OrdinalIgnoreCase);
    public string Strategy { get; set; } = "general.bat";
    public string ZapretDirectory { get; set; } = "";
    public string TgExe { get; set; } = "";
    public string Workspace { get; set; } = "";
    public string PythonExe { get; set; } = "";
    public string TgSourceDirectory { get; set; } = "";
    public int TgPort { get; set; } = 1443;
    public string TgHost { get; set; } = "127.0.0.1";
    public static HubConfig Load(string path)
    {
        path = Path.GetFullPath(path);
        var c = JsonSerializer.Deserialize<HubConfig>(File.ReadAllText(path), new JsonSerializerOptions {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("Empty config");
        var root = Path.GetDirectoryName(path)!;
        string Resolve(string s) => string.IsNullOrWhiteSpace(s) ? "" : Path.GetFullPath(s, root);
        c.ZapretDirectory = Resolve(c.ZapretDirectory); c.TgExe = Resolve(c.TgExe);
        c.Workspace = Resolve(c.Workspace); c.PythonExe = Resolve(c.PythonExe); c.TgSourceDirectory = Resolve(c.TgSourceDirectory);
        if (c.ZapretDirectory == "" || c.TgExe == "" || c.Workspace == "") throw new InvalidDataException("Required paths missing");
        if (c.TgHost != "127.0.0.1" || c.TgPort < 1024 || c.TgPort > 65535) throw new InvalidDataException("Only loopback and ports 1024..65535 supported");
        // Own writable root must be inside prototype next to configs, never inside a component/source directory.
        var prototypeRoot = Path.GetFullPath(Path.Combine(root, ".."));
        if (!Paths.Within(c.Workspace, prototypeRoot) || Paths.Overlap(c.Workspace, c.ZapretDirectory)
            || Paths.Within(c.TgExe, c.Workspace) || (c.TgSourceDirectory != "" && Paths.Overlap(c.Workspace, c.TgSourceDirectory)))
            throw new InvalidDataException("Workspace must be isolated inside prototype");
        Paths.NoReparseAncestors(c.Workspace);
        if (c.PythonExe != "" && !Paths.Within(c.PythonExe, Path.Combine(prototypeRoot,"runtime")))
            throw new InvalidDataException("Python must belong to prototype/runtime; global interpreters are not supported");
        return c;
    }
}
public static class Paths
{
    public static bool Within(string path, string root) => Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static bool Overlap(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase) || Within(a,b) || Within(b,a);
    public static void NoReparseAncestors(string path)
    {
        for (var p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse points are not accepted for writable paths");
    }
    public static string Owned(string root, string relative)
    {
        var p = Path.GetFullPath(relative, root);
        if (!Within(p, root)) throw new InvalidDataException("Path escapes workspace");
        NoReparseAncestors(p); return p;
    }
    public static void WriteNew(string path, string content)
    {
        NoReparseAncestors(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true)) { writer.Write(content); writer.Flush(); }
                stream.Flush(true);
            }
            File.Move(temporary,path,false); // Atomic same-directory publish, never overwrite user config.
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
public static class Metadata
{
    public static string Hash(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    public static bool X64Pe(string path)
    {
        using var s = File.OpenRead(path); using var r = new BinaryReader(s);
        if (s.Length < 64 || r.ReadUInt16() != 0x5a4d) return false;
        s.Position = 60; var offset = r.ReadInt32();
        if (offset < 64 || offset > s.Length - 6) return false;
        s.Position = offset; return r.ReadUInt32() == 0x00004550 && r.ReadUInt16() == 0x8664;
    }
}
public static class LogSafety
{
    public static string Redact(string text) => Regex.Replace(text, @"(?i)(secret(?:\s*[:=]\s*|\s+|%3d))[0-9a-f]{32,}", "$1<redacted>");
    public static string Tail(string path, int maximumBytes = 8192)
    {
        using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if(maximumBytes<=0)return "";
        long length=f.Length,offset=Math.Max(0,length-maximumBytes);f.Position=offset;
        byte[] bytes=new byte[(int)Math.Min(length,maximumBytes)];int count=0,read;
        while(count<bytes.Length&&(read=f.Read(bytes,count,bytes.Length-count))>0)count+=read;
        int start=0;
        // The tail may start inside a multibyte UTF-8 character. Skip its continuation bytes.
        if(offset>0)while(start<count&&(bytes[start]&0xC0)==0x80)start++;
        else if(count>=3&&bytes[0]==0xEF&&bytes[1]==0xBB&&bytes[2]==0xBF)start=3;
        return Redact(System.Text.Encoding.UTF8.GetString(bytes,start,count-start));
    }
}



