using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NetworkHub.Core;
namespace NetworkHub.App;
public static class TgRuntimeControl
{
 public static async Task VerifyOfficial(AppFiles files,string path)
 {
  Paths.NoReparseAncestors(path);var record=ModuleTrust.Record(files,"tg");
  if(record==null||!ModuleTrust.Matches(files,"tg",path))throw new InvalidDataException(L.T("The external instance is not from the selected verified installation. No process was stopped."));
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};http.DefaultRequestHeaders.UserAgent.ParseAdd("Open-Net/0.4.0");
  string url="https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/tags/"+Uri.EscapeDataString(record.Version);
  ValidateRelease(await http.GetStringAsync(url),record.Version,Metadata.Hash(path));
 }
 public static void ValidateRelease(string jsonText,string version,string hash)
 {
  using var json=JsonDocument.Parse(jsonText);var release=json.RootElement;
  if(release.GetProperty("draft").GetBoolean()||release.GetProperty("tag_name").GetString()!=version)throw new InvalidDataException("Official release identity mismatch");
  var assets=release.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("name").GetString()=="TgWsProxy_windows.exe").ToArray();
  string expected="https://github.com/Flowseal/tg-ws-proxy/releases/download/"+Uri.EscapeDataString(version)+"/TgWsProxy_windows.exe";
  if(assets.Length!=1||assets[0].GetProperty("browser_download_url").GetString()!=expected||!assets[0].TryGetProperty("digest",out var digest)||!string.Equals(digest.GetString(),"sha256:"+hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Official release digest mismatch; process left unchanged");
 }
 public static ZapretResponse Inspect(string path)
 {
  var all=ProcessObserver.Find("TgWsProxy_windows");var root=ExternalProcessControl.SelectInstance("tg",path,all);
  var members=all.Select(p=>new OwnedReceipt(root.Token,p.Pid,p.StartedUtc!.Value.ToFileTimeUtc(),p.Path!,"tg")).ToArray();
  return new(true,"",root){Members=members};
 }
 public static bool SameMembers(OwnedReceipt[] a,OwnedReceipt[] b)=>a.Length==b.Length&&a.All(x=>b.Any(y=>x.Pid==y.Pid&&x.StartFileTime==y.StartFileTime&&string.Equals(x.Executable,y.Executable,StringComparison.OrdinalIgnoreCase)));
}
