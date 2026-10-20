using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetworkHub.App;
namespace NetworkHub.Desktop;
// CI checks use WPF layout/resources and data only, with no screenshots or network tools.
public static class TestsHeadless
{
 public static int Run(Shell shell,AppFiles files){var results=new List<object>();int failed=0;
  void Test(string name,Action action){try{action();results.Add(new{Name=name,Passed=true});}catch(Exception ex){failed++;results.Add(new{Name=name,Passed=false,Error=ex.Message});}}
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  ThemeManager.SystemDarkForTests=()=>true;
  Test("new System and legacy dark configuration",()=>{var isolated=new AppFiles(Path.Combine(files.Root,"test-results/legacy"));Check(isolated.Defaults().Theme=="system","New default");Directory.CreateDirectory(Path.GetDirectoryName(isolated.SettingsPath)!);File.WriteAllText(isolated.SettingsPath,"{\"Language\":\"ru\",\"Strategy\":\"general (ALT2).bat\",\"ConfirmStartStop\":false}");var old=isolated.Load();Check(old.Theme=="dark"&&old.Language=="ru"&&!old.ConfirmStartStop&&old.Strategy=="general (ALT2).bat","Legacy settings preserved");old.Theme="light";isolated.Save(old);Check(isolated.Load().Theme=="light","Persisted theme");});
  Test("system resolver and manual precedence",()=>{Check(ThemeManager.Resolve("system",false)=="light"&&ThemeManager.Resolve("system",true)=="dark"&&ThemeManager.Resolve("dark",false)=="dark"&&ThemeManager.Resolve("light",true)=="light","Theme policy");});
  Test("live palette resources in both themes",()=>{foreach(string mode in new[]{"dark","light"}){ThemeManager.SetMode(mode);var text=((SolidColorBrush)ThemeManager.Brush("Text")).Color;var surface=((SolidColorBrush)ThemeManager.Brush("Surface")).Color;Check(mode=="dark"?text.R>surface.R+150:surface.R>text.R+150,"Text contrast");Check(((SolidColorBrush)shell.Window.Foreground).Color==text,"Main resource update");}});
  Test("System updates without modifying Windows settings",()=>{ThemeManager.SetMode("system");ThemeManager.SystemDarkForTests=()=>false;ThemeManager.Refresh();Check(!ThemeManager.IsDark,"System light");ThemeManager.SystemDarkForTests=()=>true;ThemeManager.Refresh();Check(ThemeManager.IsDark,"System dark");});
  Test("empty folder and invalid EXE are not installed modules",()=>{var root=Path.Combine(files.Root,"test-results/readiness");var isolated=new AppFiles(root);string empty=Path.Combine(root,"empty");Directory.CreateDirectory(empty);string invalid=Path.Combine(root,"invalid.exe");File.WriteAllText(invalid,"test data, never executed");var readiness=new ModuleReadiness(isolated);var settings=new UserSettings{ZapretDirectory=empty,TgExe=invalid};Check(!readiness.Read("zapret",settings).Installed&&!readiness.Read("tg",settings).Installed,"No folder-only readiness");});
  Test("download and refresh geometry/localization",()=>{Check(ModuleActionPresentation.Glyph(false)!=ModuleActionPresentation.Glyph(true),"Separate glyphs");Geometry.Parse(ModuleActionPresentation.Download);Geometry.Parse(ModuleActionPresentation.Refresh);foreach(string language in new[]{"en","ru"}){L.Set(language);Check(ModuleActionPresentation.Caption(false)==L.T("Install")&&ModuleActionPresentation.Caption(true)==L.T("Check for updates"),"Action localization");}L.Set("en");});
  Test("compact layout and isolated scrolling",()=>{var surface=(FrameworkElement)shell.Window.Content;surface.Measure(new Size(460,710));surface.Arrange(new Rect(0,0,460,710));surface.UpdateLayout();var list=shell.Find<StackPanel>("ModuleList");Check(list.Children.Count==2,"No future modules in normal UI");foreach(string name in new[]{"ZapretRow","TgRow"})Check(shell.Find<Border>(name).ActualHeight<=80,"Compact row");Check(shell.Find<ScrollViewer>("MainScroll").Content==list,"Only module list scrolls");Check(shell.Find<TextBlock>("ZapretStrategy").TextTrimming==TextTrimming.CharacterEllipsis,"Long strategies trim with tooltip");});
  Test("icon actions are separate and keyboard accessible",()=>{foreach(string module in new[]{"Zapret","Tg"}){var install=shell.Find<Button>(module+"Install");var gear=shell.Find<Button>(module+"Settings");Check(install.Width>=28&&gear.Width>=28&&install.Focusable&&gear.Focusable&&install!=gear,"Independent actions");}FocusCues.SetMode(true);Check(shell.Find<Button>("Settings").FocusVisualStyle!=null,"Keyboard focus");FocusCues.SetMode(false);Check(shell.Find<Button>("Settings").FocusVisualStyle==null,"Mouse cue absent");});
  Test("material fallback and no automatic tool launch",()=>{Check(WindowAppearance.Select(19045,true,false,false,"mica")=="solid"&&WindowAppearance.Select(26200,true,false,false,"solid")=="solid","Fallback policy");Check(shell.Runtime.LaunchCount==0&&!shell.Settings.StartWithWindows,"No network launch/autostart registration");});
  Test("final action spacing and toggle geometry",()=>{Tests032Final.Geometry(shell);Tests032Final.Toggles(shell);});
  Test("fixed repository HTTPS launch plan",()=>{var plan=RepositoryLink.Plan();Check(plan.FileName=="https://github.com/MaratKraft/Open-Net"&&plan.UseShellExecute&&plan.Verb=="open"&&plan.Arguments=="","External browser plan");});
  Test("shared Fluent icon geometry and themed plane",()=>{
   TestsFluent.Geometry(shell);string mode=ThemeManager.Mode;var provider=ThemeManager.SystemDarkForTests;
   try{foreach(bool dark in new[]{true,false}){ThemeManager.SystemDarkForTests=()=>dark;ThemeManager.SetMode("system");TestsFluent.Geometry(shell);Check(ThemeManager.IsDark==dark,"System palette follows provider including new vectors");}}
   finally{ThemeManager.SystemDarkForTests=provider;ThemeManager.SetMode(mode);}
  });
  Tests033.Headless(Test,files);
  TestsStartupReadback.Headless(Test,files);
  File.WriteAllText(Path.Combine(files.Data,"headless-tests.json"),JsonSerializer.Serialize(new{Total=results.Count,Failed=failed,NetworkToolsLaunched=false,Results=results},new JsonSerializerOptions{WriteIndented=true}));return failed==0?0:1;
 }
}
