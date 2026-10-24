using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkHub.Desktop;

internal enum DnsProbeKind {Both,Names,Https}
internal sealed record DnsProbeResult(string Endpoint,bool DnsResolved,int? HttpStatus,string DnsDetail,string HttpsDetail) {public bool NamesRequested {get;init;}=true;public bool HttpsRequested {get;init;}=true;public string StartedUtc {get;init;}=DateTime.UtcNow.ToString("O");public string FinishedUtc {get;init;}="";}
internal interface IDnsProbeRunner {Task<DnsProbeResult> Run(Uri endpoint,CancellationToken cancellation,DnsProbeKind kind=DnsProbeKind.Both);}
internal sealed class WindowsDnsProbeRunner : IDnsProbeRunner
{
    internal static Uri[] Endpoints=>new[]{new Uri("https://example.com/"),new Uri("https://chatgpt.com/"),new Uri("https://gemini.google.com/")};
    public async Task<DnsProbeResult> Run(Uri endpoint,CancellationToken cancellation,DnsProbeKind kind=DnsProbeKind.Both)
    {
        if(!Endpoints.Contains(endpoint)||endpoint.Scheme!="https"||!Enum.IsDefined(kind))throw new InvalidOperationException("Unknown probe endpoint");
        string started=DateTime.UtcNow.ToString("O");
        bool dns=false;string dnsDetail="";int? status=null;string https="";
        if(kind!=DnsProbeKind.Https){
            using var namesLimit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);namesLimit.CancelAfter(TimeSpan.FromSeconds(12));
            try{var addresses=await System.Net.Dns.GetHostAddressesAsync(endpoint.Host,namesLimit.Token);dns=addresses.Length>0;dnsDetail=dns?string.Join(", ",addresses.Select(x=>x.ToString())):"DNS returned no addresses.";}catch(Exception ex)when(ex is System.Net.Sockets.SocketException or OperationCanceledException){dnsDetail=ex is OperationCanceledException?"The check timed out or was cancelled.":"The DNS lookup failed.";}
        }
        // HTTPS is independent of the direct lookup: a configured proxy may resolve remotely.
        if(kind!=DnsProbeKind.Names){
            using var httpsLimit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);httpsLimit.CancelAfter(TimeSpan.FromSeconds(12));
            try{using var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=true};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};using var request=new HttpRequestMessage(HttpMethod.Head,endpoint);using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,httpsLimit.Token);status=(int)response.StatusCode;https="HTTPS endpoint responded. This does not prove that the service or account works.";}catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException){https=ex is OperationCanceledException?"The check timed out or was cancelled.":"HTTPS connection failed. The cause has not been determined.";}
        }
        return new(endpoint.Host,dns,status,dnsDetail,https){NamesRequested=kind!=DnsProbeKind.Https,HttpsRequested=kind!=DnsProbeKind.Names,StartedUtc=started,FinishedUtc=DateTime.UtcNow.ToString("O")};
    }
}
