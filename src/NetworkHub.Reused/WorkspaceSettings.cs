using System;
using System.Linq;
namespace NetworkHub.App;

// Presentation choices are independent of runtime, installation and startup state.
public sealed class WorkspaceSettings
{
    public static readonly string[] Modules={"zapret","tg","dns"};
    public string[] Order {get;set;}=(string[])Modules.Clone();
    public string[] Visible {get;set;}=(string[])Modules.Clone();
    public string Primary {get;set;}="group";
    public bool DnsInGroup {get;set;}
    public bool ManualHeight {get;set;}
    public double Height {get;set;}=656;
    public double? Left {get;set;}
    public double? Top {get;set;}
    public void Normalize(){Order=(Order??Array.Empty<string>()).Where(Modules.Contains).Distinct().Concat(Modules).Distinct().ToArray();Visible=(Visible??Modules).Where(Modules.Contains).Distinct().ToArray();if(Primary==null)Primary="group";if(!double.IsFinite(Height))Height=656;Height=Math.Clamp(Height,300,2000);if(Left.HasValue&&!double.IsFinite(Left.Value))Left=null;if(Top.HasValue&&!double.IsFinite(Top.Value))Top=null;}
    public static double AutoHeight(int count)=>Math.Clamp(437+73*Math.Max(1,count),510,780);
}

public static class WorkspaceDefaults
{
    // Only the controls exposed by Modules; global registration, runtime and DNS are untouched.
    public static UserSettings Plan(UserSettings current){var next=current.Copy();next.Workspace.Order=(string[])WorkspaceSettings.Modules.Clone();next.Workspace.Visible=(string[])WorkspaceSettings.Modules.Clone();next.StartupZapret=true;next.StartupTg=true;return next;}
}
