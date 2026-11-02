#if OPENNET_TESTING
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
internal static class TestsStable
{
 internal static void Run(Action<string,Action> test,AppFiles files)
 {
  void Check(bool ok,string text){if(!ok)throw new Exception(text);}
  test("About includes all ten notices without culture satellite omissions",()=>{
   var names=typeof(Notices).Assembly.GetManifestResourceNames().Where(n=>n.StartsWith("OpenNet.Notice.")).ToArray();
   Check(names.Length==10&&names.Contains("OpenNet.Notice.HB-AI-LICENSE.ru.txt"),"A notice was moved to a satellite assembly");
   Check(Notices.Text().Contains("OpenNet.Notice.HB-AI-LICENSE.ru.txt"),"About omits the Russian notice");
  });
  var app=new OpenNetRelease("v9.0.0","https://github.com/MaratKraft/Open-Net/releases/tag/v9.0.0","Open-Net-9.0.0-Windows-x64-Portable.zip","https://github.com/MaratKraft/Open-Net/releases/download/v9.0.0/Open-Net-9.0.0-Windows-x64-Portable.zip",123);
  foreach(string kind in new[]{"wrong asset","missing version","wrong channel"})test("Malformed update cache is ignored: "+kind,()=>{
   var isolated=new AppFiles(Path.Combine(files.Root,"test-results/cache-"+Guid.NewGuid().ToString("N")));
   string path=Paths.Owned(isolated.Data,"Cache/open-net-updates.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   var value=kind=="wrong asset"?app with{AssetName="wrong.zip"}:kind=="missing version"?app with{Version=null!}:app with{Version="v9.0.0-pre.1"};
   File.WriteAllText(path,JsonSerializer.Serialize(new UpdateCache(DateTimeOffset.UtcNow,value,Array.Empty<string>())));
   using var installer=new ModuleInstaller(isolated,runningProbe:_=>false);using var discovery=new UpdateDiscovery(isolated,installer,true,()=>false);
   Check(discovery.Current==null,"Invalid cache was accepted");
  });
  test("Update weekly cache manual retry and API error preserve last successful result",()=>{
   var isolated=new AppFiles(Path.Combine(files.Root,"test-results/update-retry-"+Guid.NewGuid().ToString("N")));
   using var installer=new ModuleInstaller(isolated,runningProbe:_=>false);using var discovery=new UpdateDiscovery(isolated,installer,true,()=>false);int calls=0;
   discovery.Fixture=()=>{calls++;return Task.FromResult<(OpenNetRelease?,string[])>((app,Array.Empty<string>()));};
   Check(discovery.Check(true).GetAwaiter().GetResult()&&calls==1,"Initial discovery failed");var prior=discovery.Current;
   Check(discovery.Check().GetAwaiter().GetResult()&&calls==1,"Weekly cache ignored");
   discovery.Fixture=()=>{calls++;throw new System.Net.Http.HttpRequestException("Mock GitHub API failure");};
   Check(!discovery.Check(true).GetAwaiter().GetResult()&&discovery.Current==prior,"Failure erased cached result");
   discovery.Fixture=()=>{calls++;return Task.FromResult<(OpenNetRelease?,string[])>((null,Array.Empty<string>()));};
   Check(discovery.Check(true).GetAwaiter().GetResult()&&discovery.Current?.Application==null&&calls==3,"Retry did not clear old notice");
  });
  test("DNS helper timeout returns without terminating an in-progress owned helper",()=>{
   string folder=Path.Combine(files.Data,"dns-wait-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   string host=Environment.ProcessPath!;using var supervisor=new StubSupervisor(host,host,Metadata.Hash(host),folder,true);var stub=supervisor.Start();
   using var process=System.Diagnostics.Process.GetProcessById(stub.Pid);
   bool timedOut=false;try{DnsElevated.WaitForHelper(process,TimeSpan.FromMilliseconds(100)).GetAwaiter().GetResult();}catch(NetworkHub.Dns.DnsException){timedOut=true;}
   Check(timedOut&&ProcessIdentity.Read(stub.Pid).Exists,"Timeout killed the helper or did not return");
   supervisor.Stop(stub.Token);DnsElevated.WaitForHelper(process,TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
  });
 }
}
#endif
