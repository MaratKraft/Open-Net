using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetworkHub.Core;
namespace NetworkHub.App;
// Locally recorded hashes of files obtained from official HTTPS release metadata.
// A new version is not trusted merely because its file is a valid PE.
public static class ModuleTrust
{
    public static InstalledModule? Record(AppFiles files,string id)
    {
        string manifest=Paths.Owned(files.Data,"Config/Modules/"+id+".json");if(!File.Exists(manifest))return null;
        var item=JsonSerializer.Deserialize<InstalledModule>(File.ReadAllText(manifest));
        if(item==null||item.Module!=id||!Uri.TryCreate(item.Source,UriKind.Absolute,out var source)||source.Scheme!="https"||source.Host!="github.com"||!source.AbsolutePath.StartsWith("/Flowseal/"+ModuleInstaller.Repository(id)+"/releases/",StringComparison.Ordinal))return null;
        return item;
    }
    public static bool Matches(AppFiles files,string id,string path)
    {
        var item=Record(files,id);return item!=null&&string.Equals(files.ModulePath(item.RelativePath),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)&&File.Exists(path)&&Metadata.Hash(path)==item.SHA256;
    }
    public static IReadOnlyDictionary<string,string> ZapretHashes(AppFiles files,string folder)
    {
        var item=Record(files,"zapret");return item?.Files!=null&&string.Equals(files.ModulePath(item.RelativePath),Path.GetFullPath(folder),StringComparison.OrdinalIgnoreCase)?item.Files:new Dictionary<string,string>();
    }
    public static Dictionary<string,string> HashPayload(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(x=>Path.GetRelativePath(root,x).Replace('\\','/'),Metadata.Hash,StringComparer.OrdinalIgnoreCase);
}
