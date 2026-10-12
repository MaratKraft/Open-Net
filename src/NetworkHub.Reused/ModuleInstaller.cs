using NetworkHub.App;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetworkHub.Core;
using NetworkHub.Adapters;

namespace NetworkHub.App;
public sealed record ReleaseAsset(string Module,string Version,string Name,string Url,long Size,string? Digest,string Source,string Latest);
public sealed record InstalledModule(string Module,string Version,string RelativePath,string SHA256,string Source,string AssetDigest,string? Snapshot=null,Dictionary<string,string>? Files=null);
public sealed record ReleaseCache(DateTimeOffset RetrievedUtc,ReleaseAsset Asset);
public sealed class ModuleInstaller : IDisposable
{
    public bool UsingCache {get;private set;}
    public DateTimeOffset? CatalogTimestamp {get;private set;}
    public Exception? LastCatalogFailure {get;private set;}
    readonly AppFiles files;readonly HttpClient http;readonly SemaphoreSlim gate=new(1,1);
    readonly Func<string,bool> running;
    public ModuleInstaller(AppFiles files,HttpMessageHandler? handler=null,Func<string,bool>? runningProbe=null)
    {
        running=runningProbe??(id=>ProcessObserver.Find(ModuleCatalog.Get(id).ProcessName).Length>0);
        this.files=files;http=handler==null?new HttpClient():new HttpClient(handler);
        http.Timeout=TimeSpan.FromMinutes(5);http.DefaultRequestHeaders.UserAgent.ParseAdd("Open-Net/0.3.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public static string Repository(string id)=>ModuleCatalog.Get(id).Repository;
    public string Manifest(string id)=>Paths.Owned(files.Data,"Config/Modules/"+id+".json");
    public InstalledModule? Current(string id)
    {
        if(!File.Exists(Manifest(id)))return null;
        var item=JsonSerializer.Deserialize<InstalledModule>(File.ReadAllText(Manifest(id)))??throw new InvalidDataException(L.T("Module metadata is damaged. Reinstall the compatible release."));
        if(item.Module!=id)throw new InvalidDataException(L.T("Module metadata does not match. Reinstall the compatible release."));
        string path=files.ModulePath(item.RelativePath);
        if(!(id=="tg"?File.Exists(path):Directory.Exists(path)))throw new InvalidDataException(L.T("Module files are missing. Reinstall the compatible release."));
        return item;
    }
    public InstalledModule? RepairableCurrent(string id)
    {
        try{return Current(id);}
        catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException){files.Log("Invalid module metadata; reinstall may repair: "+ex.Message);return null;}
    }
    public UserSettings ApplyManaged(UserSettings settings)
    {
        foreach(string id in ModuleCatalog.All.Select(x=>x.Id).ToArray())
        {
            var item=Current(id);if(item==null)continue;
            string path=files.ModulePath(item.RelativePath);
            if(id=="zapret"&&(settings.ZapretDirectory==""||Paths.Within(settings.ZapretDirectory,files.Modules))){settings.ZapretDirectory=path;if(settings.Strategy==StrategyList.Preferred&&!File.Exists(Path.Combine(path,settings.Strategy)))settings.Strategy="";}
            else if(id=="tg"&&(settings.TgExe==""||Paths.Within(settings.TgExe,files.Modules)))settings.TgExe=path;
        }
        return settings;
    }
    public async Task<ReleaseAsset> Discover(string id,CancellationToken cancel=default)
    {
        UsingCache=false;LastCatalogFailure=null;CatalogTimestamp=null;
        try
        {
            var asset=await DiscoverOnline(id,cancel);CatalogTimestamp=DateTimeOffset.UtcNow;
            string path=Paths.Owned(files.Data,"Cache/releases-"+id+".json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            try{string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new ReleaseCache(CatalogTimestamp.Value,asset)));File.Move(temp,path,true);}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){files.Log("Catalog cache write: "+ex);}
            return asset;
        }
        catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException)
        {
            files.Log("Catalog network: "+ex);LastCatalogFailure=ex;
            if(cancel.IsCancellationRequested)throw;
            if(NetworkErrors.Kind(ex,cancel) is "dns" or "refused" or "timeout" or "connection")
            {
                try {
                    string path=Paths.Owned(files.Data,"Cache/releases-"+id+".json");
                    if(File.Exists(path)) {
                        var saved=JsonSerializer.Deserialize<ReleaseCache>(File.ReadAllText(path));
                        if(saved!=null && saved.Asset!=null && saved.RetrievedUtc<=DateTimeOffset.UtcNow.AddMinutes(5) && ValidAsset(saved.Asset,id)) {
                            UsingCache=true;CatalogTimestamp=saved.RetrievedUtc;return saved.Asset;
                        }
                    }
                }catch(Exception cacheEx)when(cacheEx is IOException or JsonException or ArgumentException){files.Log("Catalog cache read: "+cacheEx);}
            }
            throw;
        }
    }
    public static bool ValidAsset(ReleaseAsset asset,string id) => asset.Module==id && System.Text.RegularExpressions.Regex.IsMatch(asset.Version,@"^[A-Za-z0-9][A-Za-z0-9._+-]{0,63}$") && !asset.Version.EndsWith('.') && asset.Size>0 && asset.Size<=256*1024*1024
        && Uri.TryCreate(asset.Url,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host=="github.com"
        && uri.AbsolutePath.StartsWith("/Flowseal/"+Repository(id)+"/releases/download/",StringComparison.Ordinal)
        && uri.AbsolutePath=="/Flowseal/"+Repository(id)+"/releases/download/"+Uri.EscapeDataString(asset.Version)+"/"+Uri.EscapeDataString(asset.Name)
        && (id=="zapret"?asset.Name.StartsWith("zapret-discord-youtube-",StringComparison.Ordinal)&&asset.Name.EndsWith(".zip",StringComparison.Ordinal)&&Path.GetFileName(asset.Name)==asset.Name:asset.Name=="TgWsProxy_windows.exe");
    async Task<HttpResponseMessage> CatalogRequest(string url,CancellationToken cancel)
    {
        for(int attempt=0;;attempt++)
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try{var response=await http.GetAsync(url,timeout.Token);try{await CheckHttp(response);return response;}catch{response.Dispose();throw;}}
            catch(Exception ex)when(attempt<2&&NetworkErrors.Retryable(ex,cancel))
            {files.Log("Catalog retry "+(attempt+1)+": "+ex);await Task.Delay(attempt==0?350:900,cancel);}
        }
    }
    async Task<ReleaseAsset> DiscoverOnline(string id,CancellationToken cancel)
    {
        string repo=Repository(id),latest="";
        for(int page=1;page<=5;page++)
        {
            using var response=await CatalogRequest($"https://api.github.com/repos/Flowseal/{repo}/releases?per_page=100&page={page}",cancel);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
            var stable=json.RootElement.EnumerateArray().Where(x=>!x.GetProperty("draft").GetBoolean()&&!x.GetProperty("prerelease").GetBoolean()).ToArray();
            if(latest==""&&stable.Length>0)latest=stable.OrderByDescending(x=>x.GetProperty("published_at").GetDateTimeOffset()).First().GetProperty("tag_name").GetString()!;
            foreach(var release in stable.OrderByDescending(x=>x.GetProperty("published_at").GetDateTimeOffset()))
            {
                string version=release.GetProperty("tag_name").GetString()!;
                var assets=release.GetProperty("assets").EnumerateArray().Where(x=>{string name=x.GetProperty("name").GetString()!;return id=="tg"?name=="TgWsProxy_windows.exe":name.StartsWith("zapret-discord-youtube-",StringComparison.Ordinal)&&name.EndsWith(".zip",StringComparison.Ordinal);}).ToArray();
                if(assets.Length==0)continue;if(assets.Length!=1)throw new InvalidDataException(L.T("No unique compatible Windows x64 artifact was found. Check the diagnostic log."));
                var asset=assets[0];string url=asset.GetProperty("browser_download_url").GetString()!;
                string expected=asset.GetProperty("name").GetString()!;
                var uri=new Uri(url);
                if(uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith($"/Flowseal/{repo}/releases/download/",StringComparison.Ordinal))throw new InvalidDataException(L.T("The artifact is not from the official GitHub Release. Installation was blocked."));
                long size=asset.GetProperty("size").GetInt64();if(size<=0||size>256*1024*1024)throw new InvalidDataException(L.T("The download size is unexpected. Installation was blocked."));
                string? digest=asset.TryGetProperty("digest",out var d)&&d.ValueKind==JsonValueKind.String?d.GetString():null;
                var result=new ReleaseAsset(id,version,expected,url,size,digest,release.GetProperty("html_url").GetString()!,latest);if(!ValidAsset(result,id))throw new InvalidDataException(L.T("The official release metadata is invalid. Installation was blocked."));return result;
            }
            if(json.RootElement.GetArrayLength()<100)break;
        }
        throw new NotSupportedException(L.T("No Windows artifact was found. Latest published: ")+latest);
    }
    static Task CheckHttp(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return Task.CompletedTask;
        string message=response.StatusCode switch{HttpStatusCode.Forbidden=>L.T("GitHub denied the request or the API limit was reached. Retry later."),HttpStatusCode.TooManyRequests=>L.T("GitHub rate-limited the request. Retry later."),HttpStatusCode.NotFound=>L.T("The release or file was removed from GitHub. Check for updates."),_=>L.T("GitHub HTTP error ")+(int)response.StatusCode};
        throw new HttpRequestException(message,null,response.StatusCode);
    }
    public Task<InstalledModule> Install(ReleaseAsset asset,IProgress<int>? progress=null,CancellationToken cancel=default)=>InstallCore(asset,null,progress,cancel);
    public Task<InstalledModule> Import(ReleaseAsset asset,string path,CancellationToken cancel=default)
    {
        path=Path.GetFullPath(path);Paths.NoReparseAncestors(path);
        if(asset.Digest==null)throw new InvalidDataException(L.T("A manual import requires an official release SHA-256. Check releases online or restore a verified catalog first."));
        return InstallCore(asset,path,null,cancel);
    }
    async Task<InstalledModule> InstallCore(ReleaseAsset asset,string? importedPath,IProgress<int>? progress,CancellationToken cancel)
    {
        string repo=Repository(asset.Module);var sourceUri=new Uri(asset.Url);
        if(!ValidAsset(asset,asset.Module))throw new InvalidDataException(L.T("The source or release metadata is invalid. Installation was blocked."));
        await gate.WaitAsync(cancel);string transaction=Guid.NewGuid().ToString("N"),folder=Paths.Owned(files.Data,"Downloads/"+transaction);
        string staging=Path.Combine(folder,"stage"),download=Path.Combine(folder,asset.Name);
        string? replaced=null,target=null;bool committed=false;
        try
        {
            if(File.Exists(Manifest(asset.Module))&&running(asset.Module))throw new InvalidOperationException(asset.Module=="tg"?L.T("TG is running. Close it using its own tray before updating."):L.T("Zapret is running. Stop it first; active files will not be replaced."));
            Directory.CreateDirectory(folder);Directory.CreateDirectory(staging);
            Paths.WriteNew(Path.Combine(folder,"open-net-staging.json"),JsonSerializer.Serialize(new{Application="Open Net",Token=transaction}));
            long free=new DriveInfo(Path.GetPathRoot(files.Data)!).AvailableFreeSpace;
            if(free<asset.Size*3+32*1024*1024)throw new IOException(L.T("There is not enough disk space. Free some space and retry."));
            using var response=importedPath==null?await http.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,cancel):new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(File.OpenRead(importedPath))};await CheckHttp(response);
            if(response.Content.Headers.ContentLength is long length&&length!=asset.Size)throw new InvalidDataException(L.T("The artifact size does not match the GitHub API. Retry the download."));
            await using(var input=await response.Content.ReadAsStreamAsync(cancel))
            await using(var output=new FileStream(download,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
            {
                var buffer=new byte[81920];long total=0;int read;
                while((read=await input.ReadAsync(buffer,cancel))>0){total+=read;if(total>asset.Size)throw new InvalidDataException(L.T("The download exceeded its expected size. Installation was blocked."));await output.WriteAsync(buffer.AsMemory(0,read),cancel);progress?.Report((int)(total*100/asset.Size));}
                if(total!=asset.Size)throw new InvalidDataException(L.T("The download was interrupted: the size does not match. Retry the download."));await output.FlushAsync(cancel);
            }
            string digest=Metadata.Hash(download);
            if(asset.Digest is { } trusted)
            {
                if(!trusted.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||!string.Equals(digest,trusted.Substring(7),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("The GitHub Release checksum does not match. Retry the official download."));
            }
            string payload;
            if(asset.Module=="zapret")
            {
                ExtractZip(download,staging);
                var roots=Directory.GetFiles(staging,"general.bat",SearchOption.AllDirectories).Select(Path.GetDirectoryName).Where(x=>File.Exists(Path.Combine(x!,"bin","winws.exe"))).ToArray();
                if(roots.Length!=1)throw new InvalidDataException(L.T("The Zapret archive structure is ambiguous. Installation was blocked."));payload=roots[0]!;
                if(!Metadata.X64Pe(Path.Combine(payload,"bin","winws.exe"))||!File.Exists(Path.Combine(payload,"service.bat"))||!Directory.Exists(Path.Combine(payload,"lists")))throw new InvalidDataException(L.T("The archive does not contain the expected Zapret Windows structure."));
            }
            else
            {
                payload=staging;File.Copy(download,Path.Combine(payload,"TgWsProxy_windows.exe"));
                if(!Metadata.X64Pe(Path.Combine(payload,"TgWsProxy_windows.exe")))throw new InvalidDataException(L.T("The official artifact is not a Windows x64 executable. Installation was blocked."));
                var previous=RepairableCurrent("tg");
                if(previous!=null)
                {
                    string old=Path.GetDirectoryName(files.ModulePath(previous.RelativePath))!;
                    foreach(string source in Directory.EnumerateFiles(old,"*",SearchOption.AllDirectories))
                    {
                        Paths.NoReparseAncestors(source);string ext=Path.GetExtension(source);
                        if(new[]{".exe",".dll",".sys",".bat",".cmd"}.Contains(ext,StringComparer.OrdinalIgnoreCase))continue;
                        string destination=Paths.Owned(payload,Path.GetRelativePath(old,source));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(source,destination,false);
                    }
                }
            }
            cancel.ThrowIfCancellationRequested();
            if(File.Exists(Manifest(asset.Module))&&running(asset.Module))throw new InvalidOperationException(L.T("The module started during installation. Switching versions was cancelled."));
            string readableName=repo+"-"+asset.Version;
            string versionFolder=Paths.Owned(files.Modules,readableName);target=versionFolder;
            var previousModule=RepairableCurrent(asset.Module);
            if(Directory.Exists(versionFolder))
            {
                string? previousFolder=previousModule==null?null:asset.Module=="tg"?Path.GetDirectoryName(files.ModulePath(previousModule.RelativePath)):files.ModulePath(previousModule.RelativePath);
                if(previousModule==null||previousModule.Version!=asset.Version||!string.Equals(previousFolder,versionFolder,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("The module folder already exists and is not registered as the active version. Choose another location; its files were kept."));
                bool intact=asset.Module=="tg"?Metadata.Hash(Path.Combine(versionFolder,"TgWsProxy_windows.exe"))==digest:previousModule.Files!=null&&previousModule.Files.All(x=>File.Exists(Paths.Owned(versionFolder,x.Key))&&Metadata.Hash(Paths.Owned(versionFolder,x.Key))==x.Value);
                if(intact&&previousModule.SHA256==digest)return previousModule;
                string snapshot="Backups/"+asset.Module+"/"+transaction+"/"+readableName;
                replaced=Paths.Owned(files.Data,snapshot);Directory.CreateDirectory(Path.GetDirectoryName(replaced)!);
                Paths.NoReparseAncestors(versionFolder);Directory.Move(versionFolder,replaced);previousModule=previousModule with{Snapshot=snapshot};
            }
            Directory.CreateDirectory(Path.GetDirectoryName(versionFolder)!);Directory.Move(payload,versionFolder);
            string relative=Path.GetRelativePath(files.Modules,asset.Module=="tg"?Path.Combine(versionFolder,"TgWsProxy_windows.exe"):versionFolder).Replace('\\','/');
            var installed=new InstalledModule(asset.Module,asset.Version,relative,digest,"https://github.com/Flowseal/"+repo+"/releases/tag/"+Uri.EscapeDataString(asset.Version),asset.Digest??"No release digest; official HTTPS download, local hash recorded",Files:ModuleTrust.HashPayload(versionFolder));
            string manifest=Manifest(asset.Module);Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            if(previousModule!=null){string backup=Paths.Owned(files.Data,"Backups/"+asset.Module+"/"+transaction+".json");Paths.WriteNew(backup,JsonSerializer.Serialize(previousModule));}
            string temp=manifest+"."+transaction+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(installed,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,manifest,true);
            committed=true;files.Log("Installed "+asset.Module+" "+asset.Version+" SHA256 "+digest);return installed;
        }
        catch(Exception ex){files.Log("Install "+asset.Module+": "+ex);throw;}
        finally
        {
            if(!committed&&replaced!=null&&Directory.Exists(replaced)&&target!=null)
            {
                try{if(Directory.Exists(target))Directory.Move(target,Path.Combine(folder,"uncommitted"));Directory.Move(replaced,target);}
                catch(Exception ex){files.Log("Installation restore requires attention; preserved backup: "+replaced+"; "+ex);}
            }
            // Only this validated staging directory; previous active versions always remain available.
            try{if(Directory.Exists(folder)){Paths.NoReparseAncestors(folder);if(!Paths.Within(folder,Path.Combine(files.Data,"Downloads")))throw new InvalidDataException("Unsafe staging cleanup");Directory.Delete(folder,true);}}
            catch(Exception ex){files.Log("Staging cleanup deferred: "+ex.Message);}
            finally{gate.Release();}
        }
    }
    public void RecoverInterruptedDownloads()
    {
        string downloads=Paths.Owned(files.Data,"Downloads");if(!Directory.Exists(downloads))return;
        foreach(var folder in Directory.GetDirectories(downloads))
        {
            if(!Guid.TryParseExact(Path.GetFileName(folder),"N",out _))continue;
            try
            {
                Paths.NoReparseAncestors(folder);string marker=Path.Combine(folder,"open-net-staging.json");if(!File.Exists(marker))continue;
                using var json=JsonDocument.Parse(File.ReadAllText(marker));
                if(!json.RootElement.TryGetProperty("Application",out var app)||app.GetString()!="Open Net"||!json.RootElement.TryGetProperty("Token",out var token)||token.GetString()!=Path.GetFileName(folder))continue;
                Directory.Delete(folder,true);files.Log("Recovered interrupted staging "+Path.GetFileName(folder));
            }
            catch(Exception ex){files.Log("Cannot clean interrupted staging: "+ex.Message);}
        }
    }
    public void Rollback(string id)
    {
        if(running(id))throw new InvalidOperationException(L.T("Close the module before rolling back."));
        string folder=Paths.Owned(files.Data,"Backups/"+id);
        var backup=Directory.Exists(folder)?new DirectoryInfo(folder).GetFiles("*.json").OrderByDescending(x=>x.CreationTimeUtc).FirstOrDefault():null;
        if(backup==null)throw new InvalidOperationException(L.T("No previous version is available. Install a compatible release."));
        var item=JsonSerializer.Deserialize<InstalledModule>(File.ReadAllText(backup.FullName))??throw new InvalidDataException(L.T("Backup metadata is damaged. Choose a valid backup."));
        if(item.Module!=id)throw new InvalidDataException(L.T("Backup metadata does not match. Choose a valid backup."));
        string path=item.Snapshot==null?files.ModulePath(item.RelativePath):Paths.Owned(files.Data,item.Snapshot);
        if(item.Snapshot!=null&&id=="tg")path=Path.Combine(path,"TgWsProxy_windows.exe");
        if(id=="tg") {if(!File.Exists(path)||!Metadata.X64Pe(path)||Metadata.Hash(path)!=item.SHA256)throw new InvalidDataException(L.T("The previous TG build is damaged. Reinstall the official release."));}
        else if(item.Files==null||item.Files.Any(x=>!File.Exists(Paths.Owned(path,x.Key))||Metadata.Hash(Paths.Owned(path,x.Key))!=x.Value))throw new InvalidDataException(L.T("The previous Zapret build is damaged. Reinstall the official release."));
        if(item.Snapshot!=null)
        {
            string source=Paths.Owned(files.Data,item.Snapshot);
            if(!Paths.Within(source,Paths.Owned(files.Data,"Backups/"+id)))throw new InvalidDataException(L.T("Backup metadata is damaged. Choose a valid backup."));
            string target=id=="tg"?Path.GetDirectoryName(files.ModulePath(item.RelativePath))!:files.ModulePath(item.RelativePath);
            string stage=Paths.Owned(files.Data,"Downloads/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            string restored=Path.Combine(stage,"restored");CopyPayload(source,restored);
            string preserved=Paths.Owned(files.Data,"Backups/"+id+"/rollback-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"/"+Path.GetFileName(target));Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
            bool moved=false;
            try{if(Directory.Exists(target)){Paths.NoReparseAncestors(target);Directory.Move(target,preserved);moved=true;}Directory.Move(restored,target);}
            catch{if(moved&&!Directory.Exists(target))Directory.Move(preserved,target);throw;}
            finally{if(Directory.Exists(stage)){Paths.NoReparseAncestors(stage);Directory.Delete(stage,true);}}
        }
        string temp=Manifest(id)+".rollback.tmp";File.WriteAllText(temp,JsonSerializer.Serialize(item with{Snapshot=null}));File.Move(temp,Manifest(id),true);
    }
    static void CopyPayload(string source,string target)
    {
        Paths.NoReparseAncestors(source);Directory.CreateDirectory(target);
        foreach(string path in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){Paths.NoReparseAncestors(path);string destination=Paths.Owned(target,Path.GetRelativePath(source,path));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(path,destination,false);}
    }
    public static void ExtractZip(string archive,string destination)
    {
        using var zip=ZipFile.OpenRead(archive);if(zip.Entries.Count>10000)throw new InvalidDataException(L.T("The archive contains too many files. Installation was blocked."));long expanded=0;var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            string name=entry.FullName.Replace('\\','/');
            if(name.StartsWith('/')||name.Contains(':')||name.Split('/').Any(x=>x==".."||x.EndsWith(' ')||x.EndsWith('.')))throw new InvalidDataException(L.T("The ZIP contains an unsafe path. Installation was blocked."));
            string path=Paths.Owned(destination,name);
            if(!used.Add(path))throw new InvalidDataException(L.T("The ZIP contains duplicate paths. Installation was blocked."));
            if(((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException(L.T("ZIP links are not allowed. Installation was blocked."));
            expanded+=entry.Length;if(entry.Length>128*1024*1024||expanded>512*1024*1024)throw new InvalidDataException(L.T("The extracted archive exceeds the size limit. Installation was blocked."));
            if(name.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,false);
        }
    }
    public void Dispose(){http.Dispose();gate.Dispose();}
}




