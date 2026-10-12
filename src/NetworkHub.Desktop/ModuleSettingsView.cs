using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public sealed class ModuleSettingsView
{
    public Window Window {get;}
    public ModuleSettingsView(Shell shell,AppFiles files,string id)
    {
        var definition=ModuleCatalog.Get(id);bool bundle=definition.SettingsKind==ModuleSettingsKind.Bundle;var preference=shell.Settings.ForModule(id);
        Window=SettingsLayout.Create(shell,L.T(bundle?"Zapret settings":"TG WS Proxy settings"),bundle?670:535);var panel=SettingsLayout.Panel(Window);
        SettingsLayout.Heading(panel,definition.Name,L.T("Applies to the next launch. Running processes are unchanged."));
        var state=new TextBlock{Foreground=Brushes.LightGray,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};Window.RegisterName("ModuleStatus",state);panel.Children.Add(state);
        SettingsLayout.Label(panel,bundle?"Zapret folder":"EXE TG WS Proxy");var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});panel.Children.Add(row);
        var path=new TextBox{Text=preference.Path,FontSize=12};Window.RegisterName(bundle?"ZapretPath":"TgPath",path);row.Children.Add(path);
        var browse=SettingsLayout.Button(Window,bundle?"ZapretPathBrowse":"TgPathBrowse","Browse");browse.Margin=new Thickness(8,0,0,0);Grid.SetColumn(browse,1);row.Children.Add(browse);
        void ChooseExisting(){if(bundle){using var picker=new Forms.FolderBrowserDialog{Description=L.T("Choose the portable Zapret folder"),UseDescriptionForTitle=true,SelectedPath=path.Text};if(picker.ShowDialog()==Forms.DialogResult.OK)path.Text=picker.SelectedPath;}else{var picker=new OpenFileDialog{Title=L.T("Choose TgWsProxy_windows.exe"),Filter=L.T("TG WS Proxy|TgWsProxy_windows.exe|Executable file|*.exe")};if(picker.ShowDialog(Window)==true)path.Text=picker.FileName;}}
        browse.Click+=(_,_)=>{if(bundle)SettingsLayout.BrowseBundle(shell,files,Window,path,browse);else ChooseExisting();};
        ComboBox? strategies=null;TextBlock? strategyInfo=null;
        void Populate(bool fromSettings=false){if(strategies==null)return;string keep=fromSettings?shell.Settings.Strategy:strategies.SelectedItem?.ToString()??shell.Settings.Strategy;var list=StrategyList.Read(files.Resolve(path.Text));strategies.ItemsSource=list;strategies.SelectedItem=list.Contains(keep)?keep:null;}
        void StrategyInfo(){if(strategies==null||strategyInfo==null)return;if(strategies.SelectedItem==null){strategyInfo.Text=L.T("Choose an existing strategy. ALT13 is preferred when present; no missing BAT will be substituted.");strategyInfo.Foreground=Brushes.Gray;return;}try{bool supported=NetworkHub.Adapters.ReviewedStrategy.Supported(files.Resolve(path.Text),strategies.SelectedItem?.ToString()??"",ModuleTrust.ZapretHashes(files,files.Resolve(path.Text)));strategyInfo.Text=strategies.SelectedItem==null?L.T("Choose an existing strategy. ALT13 is preferred when present; no missing BAT will be substituted."):supported?L.T("Reviewed strategy format; no CMD window. GameFilter settings are read from the bundle; disabled by default. Real network behavior has not been tested."):L.T("Unsupported strategy format or modified BAT. Choose a compatible strategy from the verified official release. This selection is saved without substitution, but cannot start.");strategyInfo.Foreground=supported?Brushes.Gray:Brushes.Salmon;}catch(Exception ex){strategyInfo.Text=L.Error(ex);}}
        if(bundle){SettingsLayout.Label(panel,"Zapret strategy");strategies=new ComboBox{MinHeight=32};Window.RegisterName("Strategy",strategies);panel.Children.Add(strategies);strategyInfo=new TextBlock{FontSize=11,Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,4)};Window.RegisterName("StrategySupport",strategyInfo);panel.Children.Add(strategyInfo);Populate();strategies.SelectionChanged+=(_,_)=>StrategyInfo();path.TextChanged+=(_,_)=>{try{Populate();StrategyInfo();}catch(Exception ex){shell.ShowMessage(L.Error(ex));}};StrategyInfo();}
        var actions=SettingsLayout.ActionGrid(Window,panel,"ModuleActions");var buttons=new List<Button>();int index=0;
        Button Action(ModuleAction action,string name,string caption,Action click){var button=SettingsLayout.Button(Window,name,caption);button.Click+=(_,_)=>click();if(definition.Actions.Contains(action)){SettingsLayout.AddAction(actions,button,index++);buttons.Add(button);}return button;}
        var error=SettingsLayout.Error(Window,panel,"ModuleError");
        TextBlock diagnostic=null!; Button save=null!;
        async Task Execute(Func<Task> command){try{if(shell.Busy)return;var before=shell.Settings.ForModule(id);await command();var after=shell.Settings.ForModule(id);if(before!=after){path.Text=after.Path;Populate(true);}StrategyInfo();UpdateInfo();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));files.Log("Module settings "+id+": "+ex);}}
        Action(ModuleAction.OpenFolder,"OpenModuleFolder","Open module folder",()=>{try{string selected=files.Resolve(path.Text.Trim());if(selected=="")throw new InvalidOperationException(L.T("Choose the module path first."));SettingsLayout.OpenFolder(shell,files,bundle?selected:Path.GetDirectoryName(selected)!);}catch(Exception ex){error.Text=L.Error(ex);}});
        Action(ModuleAction.CheckUpdates,"CheckUpdates","Check for updates",async()=>await Execute(()=>shell.InstallOrCheck(id)));
        Action(ModuleAction.SelectPath,"SelectExisting",bundle?"Use existing folder":"Use existing EXE",ChooseExisting);
        Action(ModuleAction.ImportRelease,"ImportRelease","Import release",async()=>await Execute(()=>SettingsLayout.ImportRelease(shell,id,Window)));
        Action(ModuleAction.Rollback,"RollbackModule","Rollback version",()=>{try{if(shell.Busy)return;if(MessageBox.Show(Window,L.T("Restore the previous reviewed build? Current files will be kept."),"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;shell.RollbackModule(id);path.Text=shell.Settings.ForModule(id).Path;Populate(true);StrategyInfo();UpdateInfo();}catch(Exception ex){error.Text=L.Error(ex);}});
        if(bundle)Action(ModuleAction.ConfigureNative,"ConfigureZapret","Configure Zapret",async()=>await Execute(()=>shell.ConfigureZapret(files.Resolve(path.Text.Trim()),Window)));
        SettingsLayout.Label(panel,"Diagnostics");diagnostic=new TextBlock{Foreground=Brushes.Gray,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)};Window.RegisterName("ModuleDiagnostic",diagnostic);panel.Children.Add(diagnostic);
        save=SettingsLayout.Save(Window,panel,"SaveModuleSettings");
        void UpdateInfo(){var detail=shell.DescribeModule(id);state.Text=(detail.Version??L.T("Not installed"))+" · "+Shell.StateLabel(detail.Observation.State);diagnostic.Text=detail.Diagnostic;foreach(var button in buttons)button.IsEnabled=!shell.Busy;browse.IsEnabled=!shell.Busy;save.IsEnabled=!shell.Busy;}
        save.Click+=(_,_)=>{try{if(shell.Busy)return;var next=shell.Settings.Copy();string selected=files.Resolve(path.Text.Trim()),strategy=bundle?(strategies!.SelectedItem?.ToString()??(selected==""?next.Strategy:"")):"";if(bundle&&(Path.GetFileName(strategy)!=strategy||!strategy.EndsWith(".bat",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException(L.T("Choose a strategy filename from the list."));next.SetModule(id,new(selected,strategy,next.ForModule(id).Selected));if(selected!=""){if(bundle||next.ZapretDirectory!="")files.ValidateIsolation(next);else{Paths.NoReparseAncestors(selected);if(Paths.Within(selected,files.Data)&&!Paths.Within(selected,files.Modules))throw new InvalidDataException(L.T("Choose a separate external build or an installed module from Modules."));}}shell.ApplySettings(next);shell.ShowMessage(L.T("Settings saved."));Window.Close();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));}};
        shell.StateChanged+=UpdateInfo;L.Changed+=UpdateInfo;L.Changed+=StrategyInfo;Window.Closed+=(_,_)=>{shell.StateChanged-=UpdateInfo;L.Changed-=UpdateInfo;L.Changed-=StrategyInfo;};LocalizedTree.Bind(Window);UpdateInfo();
    }
}



