using System;
using System.Collections.Generic;
namespace NetworkHub.App;
public enum ModuleSettingsKind { Bundle, NativeExecutable }
public enum ModuleAction { OpenFolder, SelectPath, ConfigureNative, CheckUpdates, Rollback, ImportRelease, Diagnostics }
public sealed record ModuleDefinition(string Id,string Name,string Repository,string ReviewedVersion,string ProcessName,bool Install,bool Start,bool Stop,bool Settings,bool Observe)
{
    public ModuleSettingsKind SettingsKind {get;init;}=ModuleSettingsKind.NativeExecutable;
    public IReadOnlyList<ModuleAction> Actions {get;init;}=new[]{ModuleAction.OpenFolder,ModuleAction.SelectPath,ModuleAction.CheckUpdates,ModuleAction.Rollback,ModuleAction.ImportRelease,ModuleAction.Diagnostics};
}
public sealed record ModuleDetails(ModuleDefinition Definition,ModulePreferences Preferences,string? Version,ToolObservation Observation,bool CanStart,bool CanStop,string Diagnostic);
public static class ModuleCatalog
{
    public static IReadOnlyList<ModuleDefinition> All {get;}=new[] {
        new ModuleDefinition("zapret","Zapret","zapret-discord-youtube","1.10.3","winws",true,true,true,true,true){SettingsKind=ModuleSettingsKind.Bundle,Actions=new[]{ModuleAction.OpenFolder,ModuleAction.SelectPath,ModuleAction.ConfigureNative,ModuleAction.CheckUpdates,ModuleAction.Rollback,ModuleAction.ImportRelease,ModuleAction.Diagnostics}},
        new ModuleDefinition("tg","TG WS Proxy","tg-ws-proxy","v1.11.1","TgWsProxy_windows",true,true,true,true,true)
    };
    public static ModuleDefinition Get(string id) => System.Linq.Enumerable.First(All,x=>x.Id==id);
}
