using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetworkHub.App;
using NetworkHub.Core;

namespace NetworkHub.Desktop;
internal static class StartupAttempt
{
    internal static void Write(AppFiles files,string phase,string message,UserSettings settings,ToolRuntime? runtime,int newLaunches=0)
    {
        try{
            string path=Paths.Owned(files.Data,"Logs/windows-startup-latest.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var selected=new[]{(settings.StartupZapret??settings.ZapretSelected)?"zapret":"",(settings.StartupTg??settings.TgSelected)?"tg":""}.Where(s=>s!="").ToArray();
            string json=JsonSerializer.Serialize(new{Utc=DateTime.UtcNow.ToString("O"),Phase=phase,Message=LogSafety.Redact(message),Requested=settings.StartWithWindows,ToolsApproved=settings.AutoStartTools,Selected=selected,Simulation=runtime?.TestMode,LaunchCount=newLaunches,Observed=runtime==null?Array.Empty<ToolObservation>():selected.Select(id=>runtime.Observe(id,settings)).ToArray()},new JsonSerializerOptions{WriteIndented=true});
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,json);File.Move(temporary,path,true);
        }catch(Exception ex){files.Log("Windows startup evidence: "+ex);}
    }
    internal static string Summary(AppFiles files)
    {
        string path=Paths.Owned(files.Data,"Logs/windows-startup-latest.json");
        if(!File.Exists(path))return L.T("No recorded automatic tool attempt yet.");
        try{using var doc=JsonDocument.Parse(File.ReadAllText(path));var r=doc.RootElement;string text=L.T("Tool startup attempt:")+" "+L.T(r.GetProperty("Phase").GetString()??"")+" · "+r.GetProperty("Utc").GetString();if(r.GetProperty("LaunchCount").ValueKind==JsonValueKind.Number)text+=" · "+L.T("New process launches:")+" "+r.GetProperty("LaunchCount").GetInt32();return text;}
        catch(Exception){return L.T("The startup attempt record could not be read.");}
    }
}
