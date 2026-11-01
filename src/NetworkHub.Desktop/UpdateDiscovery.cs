using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;

internal sealed record UpdateCache(DateTimeOffset Checked,OpenNetRelease? Application,string[] Modules,bool Prerelease=false);
internal sealed class UpdateDiscovery : IDisposable
{
 readonly AppFiles files;readonly ModuleInstaller installer;readonly bool safe;readonly HttpClient http;readonly SemaphoreSlim gate=new(1,1);readonly Func<bool> prerelease;
 internal UpdateCache? Current {get;private set;}
 internal event Action? Changed;
#if OPENNET_TESTING
 internal Func<Task<(OpenNetRelease? App,string[] Modules)>>? Fixture {get;set;}
#endif
 internal UpdateDiscovery(AppFiles files,ModuleInstaller installer,bool safe,Func<bool> prerelease,HttpMessageHandler? handler=null)
 {
  this.files=files;this.installer=installer;this.prerelease=prerelease;this.safe=safe&&handler==null;http=handler==null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}):new HttpClient(handler);
  http.Timeout=TimeSpan.FromSeconds(15);
  try{string path=Paths.Owned(files.Data,"Cache/open-net-updates.json");if(File.Exists(path)&&new FileInfo(path).Length<65536){var cache=JsonSerializer.Deserialize<UpdateCache>(File.ReadAllText(path));if(cache!=null&&cache.Prerelease==prerelease()&&cache.Checked<=DateTimeOffset.UtcNow.AddMinutes(5)&&cache.Checked>DateTimeOffset.UtcNow.AddDays(-30)&&cache.Modules!=null&&cache.Modules.All(x=>x is "zapret" or "tg")&&Valid(cache.Application))Current=cache;}}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException){files.Log("Update cache: "+ex.Message);}
 }
 bool Valid(OpenNetRelease? a)
 {
  if(a==null)return true;
  string json=JsonSerializer.Serialize(new[]{new{draft=false,prerelease=a.Version.Contains('-'),tag_name=a.Version,html_url=a.ReleaseUrl,assets=new[]{new{name=a.AssetName,browser_download_url=a.AssetUrl,size=a.Size}}}});
  return OpenNetReleaseCheck.Select(json,BuildInfo.Version,true,files.Portable)!=null;
 }
 internal async Task<bool> Check(bool manual=false)
 {
  if(!manual&&Current!=null&&Current.Prerelease==prerelease()&&DateTimeOffset.UtcNow-Current.Checked<TimeSpan.FromDays(7))return true;
  if(safe
#if OPENNET_TESTING
   &&Fixture==null
#endif
  )return false;
  if(!await gate.WaitAsync(0))return false;
  try{
   OpenNetRelease? app;
   var modules=new System.Collections.Generic.List<string>();
#if OPENNET_TESTING
   if(Fixture!=null){var data=await Fixture();app=data.App;modules.AddRange(data.Modules);}else
#endif
   {app=await OpenNetReleaseCheck.Check(http,BuildInfo.Version,prerelease(),files.Portable);
    foreach(string id in new[]{"zapret","tg"}){var installed=installer.RepairableCurrent(id);if(installed==null)continue;try{var latest=await installer.Discover(id);if(!installer.UsingCache&&UpdateVersion.TryParse(installed.Version,out var old)&&UpdateVersion.TryParse(latest.Version,out var next)&&next!.CompareTo(old)>0)modules.Add(id);}catch(Exception ex){files.Log("Module update discovery "+id+": "+ex.Message);}}}
   var result=new UpdateCache(DateTimeOffset.UtcNow,app,modules.ToArray(),prerelease());string path=Paths.Owned(files.Data,"Cache/open-net-updates.json");Paths.NoReparseAncestors(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,JsonSerializer.Serialize(result));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}Current=result;Changed?.Invoke();return true;
  }catch(Exception ex)when(ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or OperationCanceledException or InvalidDataException){files.Log("Open Net update check: "+ex.Message);return false;}finally{gate.Release();}
 }
 internal void OpenRelease()
 {
  var item=Current?.Application;if(item==null||!Valid(item))throw new InvalidOperationException("No valid update.");
  using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.ReleaseUrl){UseShellExecute=true,Verb="open"});
 }
 internal void Invalidate(){Current=null;Changed?.Invoke();}
 public void Dispose(){http.Dispose();}
}
