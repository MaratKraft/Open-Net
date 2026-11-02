using System;
using System.Linq;
using NetworkHub.App;
namespace NetworkHub.Desktop;
internal static class StopConfirmation
{
 internal static string Message(string[] targets)
 {
  string result=L.T("Stop only these tools:")+" "+string.Join(", ",targets.Distinct().Select(id=>id=="zapret"?"Zapret":id=="tg"?"TG WS Proxy":throw new ArgumentException("Unsupported stop target")))+"?";
  if(targets.Contains("tg"))result+="\n"+L.T("TG and its confirmed child processes may be terminated forcibly; unsaved settings may be lost.");
  if(targets.Contains("zapret"))result+="\n"+L.T("Windows may request UAC. The shared WinDivert driver will be left loaded.");
  return result;
 }
}
