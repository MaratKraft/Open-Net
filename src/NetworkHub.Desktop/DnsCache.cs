using System;
using System.Diagnostics;
namespace NetworkHub.Desktop;
internal static class DnsCache
{
    internal static ProcessStartInfo Plan()
    {
        var plan=new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"ipconfig.exe")){UseShellExecute=false,CreateNoWindow=true};
        plan.ArgumentList.Add("/flushdns");return plan;
    }
    internal static string Clear(bool simulated)
    {
        if(simulated)return "Simulated DNS cache cleared. Windows was unchanged.";
        using var process=Process.Start(Plan())??throw new InvalidOperationException("DNS cache clearing did not start.");
        if(!process.WaitForExit(15000)){process.Kill();process.WaitForExit();throw new InvalidOperationException("DNS cache clearing timed out.");}
        if(process.ExitCode!=0)throw new InvalidOperationException("Windows rejected DNS cache clearing.");return "System DNS cache cleared. Browser caches and open connections are unchanged.";
    }
}
