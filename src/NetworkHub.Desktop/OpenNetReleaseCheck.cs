using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace NetworkHub.Desktop;

// Discovery is read-only. The official release URL is not proof of a signed binary.
internal sealed record OpenNetRelease(string Version,string ReleaseUrl,string AssetName,string AssetUrl,long Size);
internal static class OpenNetReleaseCheck
{
    const string Endpoint="https://api.github.com/repos/MaratKraft/Open-Net/releases?per_page=100";
    const int MaxResponse=2*1024*1024;
    internal static OpenNetRelease? Select(string json,string installed,bool prerelease,bool portable)
    {
        if(!UpdateVersion.TryParse(installed,out var current))throw new InvalidDataException("Invalid installed version.");
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=32});
        if(document.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Invalid release response.");
        OpenNetRelease? best=null;UpdateVersion? bestVersion=current;UpdateVersion? unsupported=null;
        foreach(var release in document.RootElement.EnumerateArray())
        {
            if(release.ValueKind!=JsonValueKind.Object||!release.TryGetProperty("draft",out var draft)||draft.ValueKind!=JsonValueKind.False||!release.TryGetProperty("prerelease",out var pre)||(pre.ValueKind!=JsonValueKind.True&&pre.ValueKind!=JsonValueKind.False)||(!prerelease&&pre.GetBoolean()))continue;
            if(!release.TryGetProperty("tag_name",out var tag)||tag.ValueKind!=JsonValueKind.String)continue;
            string version=tag.GetString()!;if(!UpdateVersion.TryParse(version,out var candidate)||candidate!.CompareTo(bestVersion)<=0)continue;
            if(!prerelease&&version.Split('+')[0].Contains('-'))continue;
            string releaseUrl="https://github.com/MaratKraft/Open-Net/releases/tag/"+Uri.EscapeDataString(version);
            if(!release.TryGetProperty("html_url",out var html)||html.ValueKind!=JsonValueKind.String||html.GetString()!=releaseUrl||!release.TryGetProperty("assets",out var assets)||assets.ValueKind!=JsonValueKind.Array){if(unsupported==null||candidate.CompareTo(unsupported)>0)unsupported=candidate;continue;}
            string plain=version.StartsWith("v",StringComparison.Ordinal)?version.Substring(1):version;
            string expected="Open-Net-"+plain+"-Windows-x64"+(portable?"-Portable.zip":".exe");
            string url="https://github.com/MaratKraft/Open-Net/releases/download/"+Uri.EscapeDataString(version)+"/"+Uri.EscapeDataString(expected);
            bool found=false;foreach(var asset in assets.EnumerateArray())
            {
                if(asset.ValueKind!=JsonValueKind.Object||!asset.TryGetProperty("name",out var name)||name.ValueKind!=JsonValueKind.String||name.GetString()!=expected||!asset.TryGetProperty("browser_download_url",out var download)||download.ValueKind!=JsonValueKind.String||download.GetString()!=url||!asset.TryGetProperty("size",out var size)||size.ValueKind!=JsonValueKind.Number||!size.TryGetInt64(out long length)||length<=0||length>512L*1024*1024)continue;
                best=new(version,releaseUrl,expected,url,length);bestVersion=candidate;found=true;break;
            }
            if(!found&&(unsupported==null||candidate.CompareTo(unsupported)>0))unsupported=candidate;
        }
        if(unsupported!=null&&unsupported.CompareTo(bestVersion)>0)throw new InvalidDataException("Newer release has no matching verified package.");
        return best;
    }
    internal static async Task<OpenNetRelease?> Check(HttpClient client,string installed,bool prerelease,bool portable,CancellationToken cancel=default)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);request.Headers.UserAgent.ParseAdd("Open-Net-Update-Check/1.0");request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>MaxResponse)throw new InvalidDataException("Release response is too large.");
        await using var input=await response.Content.ReadAsStreamAsync(cancel);using var output=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer.AsMemory(),cancel))>0){if(output.Length+count>MaxResponse)throw new InvalidDataException("Release response is too large.");output.Write(buffer,0,count);}
        return Select(System.Text.Encoding.UTF8.GetString(output.ToArray()),installed,prerelease,portable);
    }
}
