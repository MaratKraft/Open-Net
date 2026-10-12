using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkHub.Desktop;

internal sealed record DnsProbeResult(string Endpoint,bool DnsResolved,int? HttpStatus,string DnsDetail,string HttpsDetail);
internal interface IDnsProbeRunner {Task<DnsProbeResult> Run(Uri endpoint,CancellationToken cancellation);}
internal sealed class WindowsDnsProbeRunner : IDnsProbeRunner
{
    internal static Uri[] Endpoints=>new[]{new Uri("https://example.com/"),new Uri("https://chatgpt.com/"),new Uri("https://gemini.google.com/")};
    public async Task<DnsProbeResult> Run(Uri endpoint,CancellationToken cancellation)
    {
        if(!Endpoints.Contains(endpoint)||endpoint.Scheme!="https")throw new InvalidOperationException("Unknown probe endpoint");
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);limit.CancelAfter(TimeSpan.FromSeconds(12));
        bool dns=false;string dnsDetail="";int? status=null;string https="";
        try{var addresses=await System.Net.Dns.GetHostAddressesAsync(endpoint.Host,limit.Token);dns=addresses.Length>0;dnsDetail=dns?string.Join(", ",addresses.Select(x=>x.ToString())):"DNS returned no addresses.";}catch(Exception ex)when(ex is System.Net.Sockets.SocketException or OperationCanceledException){dnsDetail=ex is OperationCanceledException?"The check timed out or was cancelled.":"The DNS lookup failed.";}
        // HTTPS is independent of the direct lookup: a configured proxy may resolve remotely.
        try{using var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=true};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};using var request=new HttpRequestMessage(HttpMethod.Head,endpoint);using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,limit.Token);status=(int)response.StatusCode;https="HTTPS endpoint responded. This does not prove that the service or account works.";}catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException){https=ex is OperationCanceledException?"The check timed out or was cancelled.":"HTTPS connection failed. The cause has not been determined.";}
        return new(endpoint.Host,dns,status,dnsDetail,https);
    }
}
