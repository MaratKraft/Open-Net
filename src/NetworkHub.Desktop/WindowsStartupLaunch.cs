using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkHub.App;

namespace NetworkHub.Desktop;

// Only the primary --autostart shell invokes this one-shot controller. No downloads,
// polling for connectivity, process termination, registration changes or retry loops.
internal sealed class WindowsStartupLaunch
{
    bool attempted;
    internal static bool Allowed(UserSettings settings)=>settings.StartWithWindows&&settings.AutoStartTools;
    internal async Task Run(UserSettings settings,Func<string,ToolObservation> observe,Func<string,Task<string>> start,Action<string,string,bool> result)
    {
        if(attempted)return;attempted=true;
        if(!Allowed(settings))return;
        // TG does not require UAC. Its start must not wait for the Zapret prompt.
        foreach(string id in new[]{settings.TgSelected?"tg":"",settings.ZapretSelected?"zapret":""}.Where(s=>s!="")){
            try{
                var state=observe(id);
                if(state.State!=ObservedState.Stopped){result(id,state.Detail,state.State is not (ObservedState.Running or ObservedState.External));continue;}
                result(id,await start(id),false);
            }catch(Exception ex){result(id,L.Error(ex),true);}
        }
    }
}
