using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public static class ZapretActions
{
    public const string ServiceDigest="3A4A22F8E8D6F337E22F17D0DD5BEC38A99BA268843643F6400851FC41F823B3";
    public static string Folder(string folder)
    {
        string result=Path.GetFullPath(folder);Paths.NoReparseAncestors(result);
        if(!Directory.Exists(result))throw new DirectoryNotFoundException(L.T("Choose an existing Zapret folder first."));
        return result;
    }
    public static ProcessStartInfo FolderPlan(string folder)=>new(Folder(folder)){UseShellExecute=true,Verb="open"};
    public static ProcessStartInfo ServicePlan(string folder,AppFiles? files=null)
    {
        folder=Folder(folder);string path=Path.Combine(folder,"service.bat");
        if(!File.Exists(path)||(Metadata.Hash(path)!=ServiceDigest&&(files==null||!ModuleTrust.ZapretHashes(files,folder).TryGetValue("service.bat",out var verified)||Metadata.Hash(path)!=verified)))throw new InvalidDataException(L.T("The original service.bat was not found or does not match the verified official release."));
        if(!File.ReadAllText(path).Contains("if \"%1\"==\"admin\"",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("This console entry format is unsupported. Open the folder to review the original utility."));
        if(path.IndexOfAny(new[]{'%','\r','\n','"'})>=0)throw new InvalidDataException(L.T("This folder name cannot safely be passed to the original console utility. Choose a path without percent signs."));
        // 'admin' is the author's documented-in-source interactive entry, not an automated menu action.
        // A single native UAC launch keeps the console process observable until its menu closes.
        return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe"))
        {UseShellExecute=true,Verb="runas",WorkingDirectory=folder,WindowStyle=ProcessWindowStyle.Normal,Arguments="/d /v:off /s /c \"\""+path+"\" admin\""};
    }
    public static async Task<bool> Configure(string folder,Func<bool> confirm,Func<ProcessStartInfo,Task>? launch=null,AppFiles? files=null)
    {
        if(!confirm())return false;
        var plan=ServicePlan(folder,files);
        if(launch!=null)await launch(plan);
        else{using var process=Process.Start(plan)??throw new InvalidOperationException(L.T("The original console utility did not open. Check the UAC prompt and log."));await process.WaitForExitAsync();}
        return true;
    }
}
