using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
public sealed class SettingsView
{
    public Window Window {get;}
    public SettingsView(Shell shell,AppFiles files)
    {
        Window=SettingsLayout.Create(shell,L.T("Settings — Open Net"),730);var panel=SettingsLayout.Panel(Window);
        SettingsLayout.Heading(panel,L.T("Settings"),L.T("General preferences. Use the gear beside a tool for its settings."));
        SettingsLayout.Label(panel,"Language");var language=new ComboBox{ItemsSource=new[]{"English","Русский"},SelectedIndex=shell.Settings.Language=="ru"?1:0,MinHeight=32};Window.RegisterName("Language",language);panel.Children.Add(language);
        language.SelectionChanged+=(_,_)=>{var next=shell.Settings.Copy();next.Language=language.SelectedIndex==1?"ru":"en";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Language save: "+ex);}};
        SettingsLayout.Label(panel,"Theme");var theme=new ComboBox{ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")},SelectedIndex=shell.Settings.Theme=="light"?1:shell.Settings.Theme=="dark"?2:0,MinHeight=32};Window.RegisterName("Theme",theme);panel.Children.Add(theme);bool changingThemeLabels=false;
        theme.SelectionChanged+=(_,_)=>{if(changingThemeLabels||theme.SelectedIndex<0)return;var next=shell.Settings.Copy();next.Theme=theme.SelectedIndex==1?"light":theme.SelectedIndex==2?"dark":"system";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Theme save: "+ex);}};
        void ThemeLabels(){int index=theme.SelectedIndex;changingThemeLabels=true;try{theme.ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")};theme.SelectedIndex=index;}finally{changingThemeLabels=false;}}L.Changed+=ThemeLabels;Window.Closed+=(_,_)=>L.Changed-=ThemeLabels;
        SettingsLayout.Label(panel,"Window material");var backdrop=new ComboBox{ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")},SelectedIndex=shell.Settings.Backdrop=="solid"?2:shell.Settings.Backdrop=="acrylic"?1:0,MinHeight=32};Window.RegisterName("Backdrop",backdrop);panel.Children.Add(backdrop);
        void BackdropLabels(){int index=backdrop.SelectedIndex;backdrop.ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")};backdrop.SelectedIndex=index;}L.Changed+=BackdropLabels;Window.Closed+=(_,_)=>L.Changed-=BackdropLabels;
        var close=SettingsLayout.Toggle(Window,panel,shell,"CloseToTray","Close to the system tray",shell.Settings.CloseToTray);
        var confirm=SettingsLayout.Toggle(Window,panel,shell,"ConfirmStartStop","Confirm start and stop",shell.Settings.ConfirmStartStop,"Only Open Net's routine prompts are affected. UAC, errors and forced-stop protection remain.");
        var startup=SettingsLayout.Toggle(Window,panel,shell,"StartWithWindows","Start with Windows",shell.Settings.StartWithWindows,"Starts only Open Net in the tray, never the network tools. Apply with Save.");
        var status=new TextBlock{Foreground=Brushes.Gray,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};Window.RegisterName("StartupStatus",status);panel.Children.Add(status);
        var repair=SettingsLayout.Button(Window,"RepairStartup","Repair startup entry");repair.HorizontalAlignment=HorizontalAlignment.Left;repair.Margin=new Thickness(0,8,0,0);panel.Children.Add(repair);
        void StartupInfo(){var state=shell.Autostart.Inspect(shell.Settings);status.Text=state.Message;repair.Visibility=state.NeedsRepair?Visibility.Visible:Visibility.Collapsed;}
        StartupInfo();L.Changed+=StartupInfo;Window.Closed+=(_,_)=>L.Changed-=StartupInfo;
        var error=SettingsLayout.Error(Window,panel,"SettingsError");
        repair.Click+=(_,_)=>{try{shell.RepairStartup();StartupInfo();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));files.Log("Startup repair: "+ex);}};
        var actions=SettingsLayout.ActionGrid(Window,panel,"SettingsActions");
        void Folder(string name,string caption,string path,int index){var button=SettingsLayout.Button(Window,name,caption);button.Click+=(_,_)=>{try{SettingsLayout.OpenFolder(shell,files,path);}catch(Exception ex){error.Text=L.Error(ex);}};SettingsLayout.AddAction(actions,button,index);}
        Folder("OpenData","Open data",files.Data,0);Folder("OpenModules","Open modules",files.Modules,1);
        var log=SettingsLayout.Button(Window,"OpenLogs","Logs");log.ToolTip=L.T("Open diagnostic log");log.Click+=async(_,_)=>await LogView.Open(Window,files);SettingsLayout.AddAction(actions,log,2);
        var licenses=SettingsLayout.Button(Window,"Licenses","About and licenses");licenses.Click+=(_,_)=>{var view=LogView.Create(Window,"Open Net 0.3.2\n\n"+Notices.Text());view.Title=L.T("Licenses — Open Net");view.Show();};SettingsLayout.AddAction(actions,licenses,3);
        var import=SettingsLayout.Button(Window,"ImportSettings","Import settings");import.Margin=new Thickness(0,8,0,0);import.HorizontalAlignment=HorizontalAlignment.Left;panel.Children.Add(import);
        import.Click+=(_,_)=>{try{var picker=new OpenFileDialog{Title=L.T("Open Net settings"),Filter=L.T("JSON settings|*.json")};if(picker.ShowDialog(Window)!=true)return;if(AppDialog.Show(Window,L.T("Import settings? Both configurations will be backed up."),"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;PortableStartup.ImportSettings(files,picker.FileName);var next=files.Load();next.StartWithWindows=shell.Settings.StartWithWindows;next.StartupInstallationId=shell.Settings.StartupInstallationId;shell.ApplySettings(next);Window.Close();}catch(Exception ex){error.Text=L.Error(ex);files.Log("Settings import: "+ex);}};
        SettingsLayout.Save(Window,panel).Click+=(_,_)=>{try{var next=shell.Settings.Copy();next.Backdrop=backdrop.SelectedIndex==2?"solid":backdrop.SelectedIndex==1?"acrylic":"mica";next.CloseToTray=close.IsChecked==true;next.ConfirmStartStop=confirm.IsChecked==true;next.StartWithWindows=startup.IsChecked==true;shell.ApplyGeneralSettings(next);shell.ShowMessage(L.T("Settings saved."));Window.Close();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));files.Log("General settings: "+ex);}};
        LocalizedTree.Bind(Window);
    }
}
internal static class WindowNames
{
    public static void NameScopeSetup(this Window window)=>NameScope.SetNameScope(window,new NameScope());
}
