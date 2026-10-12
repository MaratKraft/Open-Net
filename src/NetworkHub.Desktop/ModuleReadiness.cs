using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetworkHub.App;
using NetworkHub.Adapters;
namespace NetworkHub.Desktop;
public sealed record ModuleHealth(bool Installed,string? Version,string Detail);
// Installed file readiness is separate from the selected strategy and process state.
// A healthy managed package still exists if the user has selected an invalid external path.
public sealed class ModuleReadiness(AppFiles files)
{
 readonly Dictionary<string,(string Stamp,ModuleHealth Health)> cache=new();
 public ModuleHealth Read(string id,UserSettings settings){try{
  InstalledModule? current=null;try{using var installer=new ModuleInstaller(files);current=installer.Current(id);}catch(Exception ex)when(ex is IOException or System.Text.Json.JsonException){files.Log("Module readiness metadata: "+ex.Message);}
  string selected=settings.ForModule(id).Path;var candidates=new List<string>();if(!string.IsNullOrWhiteSpace(selected))candidates.Add(selected);if(current!=null){string managed=files.ModulePath(current.RelativePath);if(!candidates.Contains(managed,StringComparer.OrdinalIgnoreCase))candidates.Add(managed);}
  var paths=new List<string>{Path.Combine(files.Data,"Config/Modules/"+id+".json")};foreach(string path in candidates){if(id=="tg")paths.Add(path);else if(Directory.Exists(path)){paths.AddRange(Directory.GetFiles(path,"*.bat"));foreach(string folder in new[]{"bin","lists"})if(Directory.Exists(Path.Combine(path,folder)))paths.AddRange(Directory.GetFiles(Path.Combine(path,folder)));}}
  string stamp=selected+"|"+settings.Strategy+"|"+string.Join("|",paths.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Select(x=>{var f=new FileInfo(x);return x+":"+(f.Exists?f.Length+":"+f.LastWriteTimeUtc.Ticks:"missing");}));if(cache.TryGetValue(id,out var previous)&&previous.Stamp==stamp)return previous.Health;
  string detail="";ModuleHealth health=new(false,null,"");foreach(string path in candidates){bool installed=false;var validation=settings.Copy();
   if(id=="tg"){if(File.Exists(path)){detail=new TgExeAdapter(files).Validate(path)??"";installed=detail=="";}}
   else if(Directory.Exists(path)){validation.ZapretDirectory=path;var trusted=files.ForZapret(validation).TrustedFiles;string? compatible=StrategyList.Read(path).FirstOrDefault(name=>ReviewedStrategy.Supported(path,name,trusted));if(compatible!=null){validation.Strategy=compatible;var checks=new ZapretAdapter(files.ForZapret(validation)).Validate();installed=!checks.Any(x=>x.Error);detail=string.Join("; ",checks.Where(x=>x.Error).Select(x=>x.Message));}}
   if(!installed)continue;string? version=current!=null&&string.Equals(Path.GetFullPath(path),files.ModulePath(current.RelativePath),StringComparison.OrdinalIgnoreCase)?current.Version:null;if(version==null&&id=="tg")version=FileVersionInfo.GetVersionInfo(path).FileVersion;health=new(true,string.IsNullOrWhiteSpace(version)?null:version,detail);break;
  }
  if(!health.Installed)health=new(false,null,detail);cache[id]=(stamp,health);return health;
 }catch(Exception ex){return new(false,null,L.Error(ex));}}
}
public static class ModuleActionPresentation
{
 public const string Download="M8,1 L8,11 M3,6 L8,11 L13,6 M2,12 L2,15 L14,15 L14,12";
 public const string Refresh="M13,5 A5.5,5.5 0 1 0 13,11 M13,1 L13,5 L9,5";
 public static string Caption(bool installed)=>L.T(installed?"Check for updates":"Install");
 public static string Glyph(bool installed)=>installed?Refresh:Download;
}
