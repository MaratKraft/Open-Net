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
        Window=SettingsLayout.Create(shell,L.T("Settings — Open Net"),730);var panel=SettingsLayout.Panel(Window);panel.Margin=new Thickness(24,18,24,18);
        SettingsLayout.Heading(panel,L.T("Settings"),L.T("General preferences. Tool settings are in the main window."));
        SettingsLayout.Label(panel,"Language");var language=new ComboBox{ItemsSource=new[]{"English","Русский"},SelectedIndex=shell.Settings.Language=="ru"?1:0,MinHeight=32};Window.RegisterName("Language",language);panel.Children.Add(language);
        language.SelectionChanged+=(_,_)=>{var next=shell.Settings.Copy();next.Language=language.SelectedIndex==1?"ru":"en";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Language save: "+ex);}};
        var appearance=new Grid{Margin=new Thickness(0,5,0,0)};appearance.ColumnDefinitions.Add(new ColumnDefinition());appearance.ColumnDefinitions.Add(new ColumnDefinition());Window.RegisterName("AppearanceFields",appearance);panel.Children.Add(appearance);var themePanel=new StackPanel{Margin=new Thickness(0,0,6,0)};var materialPanel=new StackPanel{Margin=new Thickness(6,0,0,0)};Grid.SetColumn(materialPanel,1);appearance.Children.Add(themePanel);appearance.Children.Add(materialPanel);
        SettingsLayout.Label(themePanel,"Theme");var theme=new ComboBox{ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")},SelectedIndex=shell.Settings.Theme=="light"?1:shell.Settings.Theme=="dark"?2:0,MinHeight=32};Window.RegisterName("Theme",theme);themePanel.Children.Add(theme);bool changingThemeLabels=false;
        theme.SelectionChanged+=(_,_)=>{if(changingThemeLabels||theme.SelectedIndex<0)return;var next=shell.Settings.Copy();next.Theme=theme.SelectedIndex==1?"light":theme.SelectedIndex==2?"dark":"system";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Theme save: "+ex);}};
        void ThemeLabels(){int index=theme.SelectedIndex;changingThemeLabels=true;try{theme.ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")};theme.SelectedIndex=index;}finally{changingThemeLabels=false;}}L.Changed+=ThemeLabels;Window.Closed+=(_,_)=>L.Changed-=ThemeLabels;
        SettingsLayout.Label(materialPanel,"Window material");var backdrop=new ComboBox{ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")},SelectedIndex=shell.Settings.Backdrop=="solid"?2:shell.Settings.Backdrop=="acrylic"?1:0,MinHeight=32};Window.RegisterName("Backdrop",backdrop);materialPanel.Children.Add(backdrop);
        void BackdropLabels(){int index=backdrop.SelectedIndex;backdrop.ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")};backdrop.SelectedIndex=index;}L.Changed+=BackdropLabels;Window.Closed+=(_,_)=>L.Changed-=BackdropLabels;
        var close=SettingsLayout.Toggle(Window,panel,shell,"CloseToTray","Close to the system tray",shell.Settings.CloseToTray);
        var confirm=SettingsLayout.Toggle(Window,panel,shell,"ConfirmStartStop","Confirm start and stop",shell.Settings.ConfirmStartStop,"Only Open Net's routine prompts are affected. UAC, errors and forced-stop protection remain.");
        var startup=SettingsLayout.Toggle(Window,panel,shell,"StartWithWindows","Start with Windows",shell.Settings.StartWithWindows,"Apply with Save. Automatic tools require separate approval.");
        var tools=SettingsLayout.Toggle(Window,panel,shell,"AutoStartTools","Automatically start selected tools",shell.Settings.AutoStartTools,"Uses Task Scheduler with administrator privileges. Windows asks for permission when registering, not at every logon.");
        var hidden=SettingsLayout.Toggle(Window,panel,shell,"StartupInTray","Start in the tray at Windows logon",shell.Settings.StartupInTray,"Manual launch still opens the main window. Windows controls notification icon placement.");
        void StartupControls(){tools.IsEnabled=hidden.IsEnabled=startup.IsChecked==true;}startup.Checked+=(_,_)=>StartupControls();startup.Unchecked+=(_,_)=>StartupControls();StartupControls();
        var status=new TextBlock{Foreground=Brushes.Gray,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};Window.RegisterName("StartupStatus",status);
        var summary=new TextBlock{FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,2)};ThemeManager.Bind(summary,TextBlock.ForegroundProperty,"Muted");Window.RegisterName("StartupSummary",summary);panel.Children.Add(summary);
        var detailsPanel=new StackPanel();detailsPanel.Children.Add(status);var details=new Expander{Header=L.T("Startup details"),FontSize=12,Content=detailsPanel,Margin=new Thickness(0,4,0,0)};ThemeManager.Bind(details,Control.ForegroundProperty,"Muted");Window.RegisterName("StartupDetails",details);panel.Children.Add(details);
        var repair=SettingsLayout.Button(Window,"RepairStartup","Repair startup entry");repair.HorizontalAlignment=HorizontalAlignment.Left;repair.Margin=new Thickness(0,8,0,0);detailsPanel.Children.Add(repair);
        void StartupInfo(){
            var state=shell.Autostart.Inspect(shell.Settings);string Flag(bool? value)=>L.T(value==true?"yes":value==false?"no":"unknown");
            bool pending=startup.IsChecked!=shell.Settings.StartWithWindows||tools.IsChecked!=shell.Settings.AutoStartTools||hidden.IsChecked!=shell.Settings.StartupInTray;
            summary.Text=pending?L.T("The edited preferences have not been saved yet."):!shell.Settings.StartWithWindows?L.T("Windows startup is off. Network tools never start automatically."):state.Ready?L.T(shell.Settings.AutoStartTools?"Windows startup is configured. Selected tools will start at logon.":"Windows startup is configured for Open Net only."):L.T("Startup setup needs attention. Open details.");
            status.Text=(pending?L.T("The edited preferences have not been saved yet.")+"\n":"")+L.T("Saved startup preference:")+" "+Flag(shell.Settings.StartWithWindows)+" · "+L.T("Settings saved:")+" "+Flag(state.SettingsSaved)+"\n"+L.T("Windows registration:")+" "+Flag(state.Exists)+" · "+L.T("Enabled:")+" "+Flag(state.Enabled)+" · "+L.T("Ownership confirmed:")+" "+Flag(state.Owned)+"\n"+state.Message;
            if(state.Mode=="task")status.Text+="\n"+L.T("Task last run:")+" "+(state.LastTaskResult==0x41303?L.T("has not run"):state.LastRunUtc??L.T("unknown"))+(state.LastTaskResult.HasValue?" · 0x"+state.LastTaskResult.Value.ToString("X8"):"")+(state.HistoryEnabled==false?" · "+L.T("Task history is disabled."):"");
            status.Text+="\n"+StartupAttempt.Summary(files);status.ToolTip=state.TaskName+"\n"+state.Command;repair.Visibility=state.Exists==true&&state.NeedsRepair&&state.RepairAllowed?Visibility.Visible:Visibility.Collapsed;
        }
        StartupInfo();L.Changed+=StartupInfo;Window.Closed+=(_,_)=>L.Changed-=StartupInfo;
        startup.Checked+=(_,_)=>StartupInfo();startup.Unchecked+=(_,_)=>StartupInfo();tools.Checked+=(_,_)=>StartupInfo();tools.Unchecked+=(_,_)=>StartupInfo();hidden.Checked+=(_,_)=>StartupInfo();hidden.Unchecked+=(_,_)=>StartupInfo();
        var error=SettingsLayout.Error(Window,panel,"SettingsError");var errorStyle=new Style(typeof(TextBlock));var emptyError=new Trigger{Property=TextBlock.TextProperty,Value=""};emptyError.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));errorStyle.Triggers.Add(emptyError);error.Style=errorStyle;
        repair.Click+=async(_,_)=>{try{await shell.ApplyGeneralSettingsAsync(shell.Settings.Copy(),true);StartupInfo();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));files.Log("Startup repair: "+ex);}};
        var actions=SettingsLayout.ActionGrid(Window,panel,"SettingsActions");
        void Folder(string name,string caption,string path,int index){var button=SettingsLayout.Button(Window,name,caption);button.Click+=(_,_)=>{try{SettingsLayout.OpenFolder(shell,files,path);}catch(Exception ex){error.Text=L.Error(ex);}};SettingsLayout.AddAction(actions,button,index);}
        Folder("OpenData","Open data",files.Data,0);Folder("OpenModules","Open modules",files.Modules,1);
        var log=SettingsLayout.Button(Window,"OpenLogs","Logs");log.ToolTip=L.T("Open diagnostic log");log.Click+=async(_,_)=>await LogView.Open(Window,files);SettingsLayout.AddAction(actions,log,2);
        var licenses=SettingsLayout.Button(Window,"Licenses","About and licenses");licenses.Click+=(_,_)=>{var view=LogView.Create(Window,"Open Net 0.3.3\n\n"+Notices.Text());view.Title=L.T("Licenses — Open Net");view.Show();};SettingsLayout.AddAction(actions,licenses,3);
        var footer=new Grid{Margin=new Thickness(0,12,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition());Window.RegisterName("SettingsFooter",footer);panel.Children.Add(footer);
        var import=SettingsLayout.Button(Window,"ImportSettings","Import settings");import.Width=136;import.Height=38;import.Margin=new Thickness(0);import.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(import);
        import.Click+=(_,_)=>{try{var picker=new OpenFileDialog{Title=L.T("Open Net settings"),Filter=L.T("JSON settings|*.json")};if(picker.ShowDialog(Window)!=true)return;if(AppDialog.Show(Window,L.T("Import settings? Both configurations will be backed up."),"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;PortableStartup.ImportSettings(files,picker.FileName);var next=files.Load();next.StartWithWindows=shell.Settings.StartWithWindows;next.StartupInstallationId=shell.Settings.StartupInstallationId;next.AutoStartTools=shell.Settings.AutoStartTools;next.StartupInTray=shell.Settings.StartupInTray;shell.ApplySettings(next);Window.Close();}catch(Exception ex){error.Text=L.Error(ex);files.Log("Settings import: "+ex);}};
        var save=SettingsLayout.Save(Window,panel);panel.Children.Remove(save);save.Width=136;save.Height=38;save.Padding=import.Padding;save.Margin=new Thickness(0);Grid.SetColumn(save,1);footer.Children.Add(save);
        save.Click+=async(_,_)=>{try{var next=shell.Settings.Copy();next.Backdrop=backdrop.SelectedIndex==2?"solid":backdrop.SelectedIndex==1?"acrylic":"mica";next.CloseToTray=close.IsChecked==true;next.ConfirmStartStop=confirm.IsChecked==true;next.StartWithWindows=startup.IsChecked==true;next.AutoStartTools=tools.IsChecked==true;next.StartupInTray=hidden.IsChecked==true;if(!await shell.ApplyGeneralSettingsAsync(next))return;shell.ShowMessage(L.T("Settings saved."));Window.Close();}catch(Exception ex){StartupInfo();error.Text=LogSafety.Redact(L.Error(ex));files.Log("General settings: "+ex);}};
        LocalizedTree.Bind(Window);
    }
}
internal static class WindowNames
{
    public static void NameScopeSetup(this Window window)=>NameScope.SetNameScope(window,new NameScope());
}
