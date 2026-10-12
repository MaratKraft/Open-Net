using System.Linq;
using NetworkHub.Core;
using NetworkHub.App;
namespace NetworkHub.Desktop;
public static class PrimaryAction
{
    public static bool CanStop(ToolObservation observation)=>observation.State is ObservedState.Running or ObservedState.CleanupRequired;
    public static bool Stop(ToolObservation[] observations,string[] selected)=>observations.Any(x=>selected.Contains(x.Id)&&CanStop(x));
    public static bool ExternalOnly(ToolObservation[] observations,string[] selected)=>selected.Length>0&&selected.All(id=>observations.Any(x=>x.Id==id&&x.State==ObservedState.External));
}
