using NetworkHub.App;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;

namespace NetworkHub.App;
public static class NetworkErrors
{
    public static string Kind(Exception ex,CancellationToken cancel=default)
    {
        if(ex is OperationCanceledException)return cancel.IsCancellationRequested?"cancelled":"timeout";
        if(ex is HttpRequestException request)
        {
            if(request.StatusCode.HasValue)return "http";
            if(request.HttpRequestError==HttpRequestError.NameResolutionError)return "dns";
            if(request.HttpRequestError==HttpRequestError.SecureConnectionError)return "tls";
        }
        for(Exception? inner=ex;inner!=null;inner=inner.InnerException)
        {
            if(inner is AuthenticationException)return "tls";
            if(inner is SocketException socket)return socket.SocketErrorCode switch {
                SocketError.ConnectionRefused=>"refused",SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain=>"dns",
                SocketError.TimedOut=>"timeout",_=>"connection"};
        }
        return ex is HttpRequestException?"connection":"other";
    }
    public static bool Retryable(Exception ex,CancellationToken cancel) => !cancel.IsCancellationRequested &&
        (Kind(ex,cancel) is "dns" or "refused" or "timeout" or "connection" || ex is HttpRequestException {StatusCode: >= HttpStatusCode.InternalServerError});
    public static string Message(Exception ex,CancellationToken cancel=default) => Kind(ex,cancel) switch {
        "dns"=>L.T("Could not resolve api.github.com. Check your connection and DNS, then retry. Installed modules remain available."),
        "refused"=>L.T("Connection to GitHub was refused (10061). Check your connection, firewall and Windows proxy, then retry. Installed modules remain available."),
        "timeout"=>L.T("GitHub did not respond in time. Retry later. Installed modules remain available."),
        "tls"=>L.T("A secure connection to GitHub could not be verified. Check the system clock and certificates. Certificate checks remain enabled."),
        "cancelled"=>L.T("The request was cancelled. Installed modules remain available."),
        "http"=>L.T("GitHub returned HTTP ")+((HttpRequestException)ex).StatusCode.GetValueOrDefault().ToString("D")+L.T(". Retry later; for 403/429 wait for the API limit to reset. Installed modules remain available."),
        "connection"=>L.T("GitHub is unreachable. Check your connection and Windows proxy, then retry. Installed modules remain available."),
        _=>L.T("The operation could not be completed. Check the selected files and diagnostic log, then retry.")
    };
}



