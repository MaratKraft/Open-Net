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
        Window=SettingsLayout.Create(shell,L.T("Settings — Open Net"),740);var panel=SettingsLayout.Panel(Window);panel.Margin=new Thickness(24,18,24,0);var scroll=(ScrollViewer)Window.Content;Window.RegisterName("SettingsScroll",scroll);var root=new Grid();root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Window.Content=null;root.Children.Add(scroll);Window.Content=root;
        SettingsLayout.Heading(panel,L.T("Settings"),L.T("General preferences. Tool settings are in the main window."));
        SettingsLayout.Label(panel,"Language");var language=new ComboBox{ItemsSource=new[]{"English","Русский"},SelectedIndex=shell.Settings.Language=="ru"?1:0,MinHeight=32};Window.RegisterName("Language",language);panel.Children.Add(language);
        language.SelectionChanged+=(_,_)=>{var next=shell.Settings.Copy();next.Language=language.SelectedIndex==1?"ru":"en";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Language save: "+ex);}};
        var appearance=new Grid{Margin=new Thickness(0,5,0,0)};appearance.ColumnDefinitions.Add(new ColumnDefinition());appearance.ColumnDefinitions.Add(new ColumnDefinition());Window.RegisterName("AppearanceFields",appearance);panel.Children.Add(appearance);var themePanel=new StackPanel{Margin=new Thickness(0,0,6,0)};var materialPanel=new StackPanel{Margin=new Thickness(6,0,0,0)};Grid.SetColumn(materialPanel,1);appearance.Children.Add(themePanel);appearance.Children.Add(materialPanel);
        SettingsLayout.Label(themePanel,"Theme");var theme=new ComboBox{ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")},SelectedIndex=shell.Settings.Theme=="light"?1:shell.Settings.Theme=="dark"?2:0,MinHeight=32};Window.RegisterName("Theme",theme);themePanel.Children.Add(theme);bool changingThemeLabels=false;
        theme.SelectionChanged+=(_,_)=>{if(changingThemeLabels||theme.SelectedIndex<0)return;var next=shell.Settings.Copy();next.Theme=theme.SelectedIndex==1?"light":theme.SelectedIndex==2?"dark":"system";try{shell.ApplySettings(next);}catch(Exception ex){shell.ShowMessage(L.Error(ex));files.Log("Theme save: "+ex);}};
        void ThemeLabels(){int index=theme.SelectedIndex;changingThemeLabels=true;try{theme.ItemsSource=new[]{L.T("System"),L.T("Light"),L.T("Dark")};theme.SelectedIndex=index;}finally{changingThemeLabels=false;}}L.Changed+=ThemeLabels;Window.Closed+=(_,_)=>L.Changed-=ThemeLabels;
        SettingsLayout.Label(materialPanel,"Window material");var backdrop=new ComboBox{ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")},SelectedIndex=shell.Settings.Backdrop=="solid"?2:shell.Settings.Backdrop=="acrylic"?1:0,MinHeight=32};Window.RegisterName("Backdrop",backdrop);materialPanel.Children.Add(backdrop);
        void BackdropLabels(){int index=backdrop.SelectedIndex;backdrop.ItemsSource=new[]{"Mica","Acrylic",L.T("Solid")};backdrop.SelectedIndex=index;}L.Changed+=BackdropLabels;Window.Closed+=(_,_)=>L.Changed-=BackdropLabels;
        var behavior=new StackPanel{Margin=new Thickness(0,20,0,20)};Window.RegisterName("BehaviorGroup",behavior);panel.Children.Add(behavior);var behaviorLabel=new TextBlock{Text=L.T("Application behavior"),Margin=new Thickness(0,0,0,12)};ThemeManager.Bind(behaviorLabel,TextBlock.ForegroundProperty,"SecondaryText");behavior.Children.Add(behaviorLabel);
        var close=SettingsLayout.Toggle(Window,behavior,shell,"CloseToTray","Close to the system tray",shell.Settings.CloseToTray);
        close.Margin=new Thickness(0);
        var confirm=SettingsLayout.Toggle(Window,behavior,shell,"ConfirmStartStop","Confirm start and stop",shell.Settings.ConfirmStartStop,"Only Open Net's routine prompts are affected. UAC, errors and forced-stop protection remain.");
        var startup=SettingsLayout.Toggle(Window,behavior,shell,"StartWithWindows","Start with Windows",shell.Settings.StartWithWindows,"Apply with Save. Automatic tools require separate approval.");
        var tools=SettingsLayout.Toggle(Window,behavior,shell,"AutoStartTools","Automatically start selected tools",shell.Settings.AutoStartTools,"Uses Task Scheduler with administrator privileges. Windows asks for permission when registering, not at every logon.");
        var hidden=SettingsLayout.Toggle(Window,behavior,shell,"StartupInTray","Start in the tray at Windows logon",shell.Settings.StartupInTray,"Manual launch still opens the main window. Windows controls notification icon placement.");
        void StartupControls(){tools.IsEnabled=hidden.IsEnabled=startup.IsChecked==true;}startup.Checked+=(_,_)=>StartupControls();startup.Unchecked+=(_,_)=>StartupControls();StartupControls();
        var summary=new TextBlock{FontSize=12,TextWrapping=TextWrapping.Wrap};ThemeManager.Bind(summary,TextBlock.ForegroundProperty,"Muted");Window.RegisterName("StartupSummary",summary);var startupStatus=new StackPanel{Margin=new Thickness(0,0,0,16)};Window.RegisterName("StartupStatusRegion",startupStatus);panel.Children.Add(startupStatus);startupStatus.Children.Add(summary);
        var repair=SettingsLayout.Button(Window,"RepairStartup","Repair");repair.HorizontalAlignment=HorizontalAlignment.Left;repair.Padding=new Thickness(16,4,16,4);repair.Margin=new Thickness(0,4,0,0);startupStatus.Children.Add(repair);
        var diagnosticText=new TextBlock{FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)};ThemeManager.Bind(diagnosticText,TextBlock.ForegroundProperty,"Muted");Window.RegisterName("StartupDiagnosticText",diagnosticText);
        string? lastDiagnostic=null;
        void StartupInfo(){
            var state=shell.Autostart.Inspect(shell.Settings);string Flag(bool? value)=>L.T(value==true?"yes":value==false?"no":"unknown");
            bool pending=startup.IsChecked!=shell.Settings.StartWithWindows||tools.IsChecked!=shell.Settings.AutoStartTools||hidden.IsChecked!=shell.Settings.StartupInTray;
            summary.Text=pending?L.T("The edited preferences have not been saved yet."):!shell.Settings.StartWithWindows?L.T("Windows startup is off."):state.Ready?L.T("Windows startup is configured."):L.T("Startup needs repair.");
            ThemeManager.Bind(summary,TextBlock.ForegroundProperty,!pending&&!state.Ready&&(shell.Settings.StartWithWindows||state.Exists==true)?"Warning":"Muted");
            string diagnostic=(pending?L.T("The edited preferences have not been saved yet.")+"\n":"")+L.T("Saved startup preference:")+" "+Flag(shell.Settings.StartWithWindows)+" · "+L.T("Settings saved:")+" "+Flag(state.SettingsSaved)+"\n"+L.T("Windows registration:")+" "+Flag(state.Exists)+" · "+L.T("Enabled:")+" "+Flag(state.Enabled)+" · "+L.T("Ownership confirmed:")+" "+Flag(state.Owned)+"\n"+state.Message;
            if(state.Mode=="task")diagnostic+="\n"+L.T("Task last run:")+" "+(state.LastTaskResult==0x41303?L.T("has not run"):state.LastRunUtc??L.T("unknown"))+(state.LastTaskResult.HasValue?" · 0x"+state.LastTaskResult.Value.ToString("X8"):"")+(state.HistoryEnabled==false?" · "+L.T("Task history is disabled."):"");
            diagnostic+="\n"+StartupAttempt.Summary(files);diagnosticText.Text=diagnostic;diagnostic+="\n"+state.TaskName+"\n"+state.Command;if(diagnostic!=lastDiagnostic){files.Log("Startup diagnostics: "+diagnostic);lastDiagnostic=diagnostic;}repair.Visibility=state.Exists==true&&state.NeedsRepair&&state.RepairAllowed?Visibility.Visible:Visibility.Collapsed;
        }
        StartupInfo();L.Changed+=StartupInfo;Window.Closed+=(_,_)=>L.Changed-=StartupInfo;
        startup.Checked+=(_,_)=>StartupInfo();startup.Unchecked+=(_,_)=>StartupInfo();tools.Checked+=(_,_)=>StartupInfo();tools.Unchecked+=(_,_)=>StartupInfo();hidden.Checked+=(_,_)=>StartupInfo();hidden.Unchecked+=(_,_)=>StartupInfo();
        var error=SettingsLayout.Error(Window,panel,"SettingsError");var errorStyle=new Style(typeof(TextBlock));var emptyError=new Trigger{Property=TextBlock.TextProperty,Value=""};emptyError.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));errorStyle.Triggers.Add(emptyError);error.Style=errorStyle;
        repair.Click+=async(_,_)=>{try{await shell.ApplyGeneralSettingsAsync(shell.Settings.Copy(),true);StartupInfo();}catch(Exception ex){error.Text=LogSafety.Redact(L.Error(ex));files.Log("Startup repair: "+ex);}};
        var extra=new StackPanel{Margin=new Thickness(0,12,0,8)};Window.RegisterName("SettingsAdditionalContent",extra);var lower=panel;var actions=SettingsLayout.ActionGrid(Window,lower,"SettingsActions");actions.Margin=new Thickness(0);
        void Folder(string name,string caption,string path,int index){var button=SettingsLayout.Button(Window,name,caption);button.Click+=(_,_)=>{try{SettingsLayout.OpenFolder(shell,files,path);}catch(Exception ex){error.Text=L.Error(ex);}};SettingsLayout.AddAction(actions,button,index);}
        Folder("OpenData","Open data",files.Data,0);Folder("OpenModules","Open modules",files.Modules,1);
        var log=SettingsLayout.Button(Window,"OpenLogs","Logs");log.ToolTip=L.T("Open diagnostic log");log.Click+=async(_,_)=>await LogView.Open(Window,files);SettingsLayout.AddAction(actions,log,2);
        var licenses=SettingsLayout.Button(Window,"Licenses","About and licenses");licenses.Click+=(_,_)=>{var view=LogView.Create(Window,BuildInfo.Caption+"\n\n"+Notices.Text());view.Title=L.T("Licenses — Open Net");view.Show();};SettingsLayout.AddAction(actions,licenses,3);
        panel.Children.Add(extra);
        var import=SettingsLayout.Button(Window,"ImportSettings","Import settings");import.HorizontalAlignment=HorizontalAlignment.Left;extra.Children.Add(import);var technical=new ScrollViewer{Content=diagnosticText,MaxHeight=140,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,8,0,4)};extra.Children.Add(technical);
        import.Click+=(_,_)=>{try{var picker=new OpenFileDialog{Title=L.T("Open Net settings"),Filter=L.T("JSON settings|*.json")};if(picker.ShowDialog(Window)!=true)return;if(AppDialog.Show(Window,L.T("Import settings? Both configurations will be backed up."),"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;PortableStartup.ImportSettings(files,picker.FileName);var next=files.Load();next.StartWithWindows=shell.Settings.StartWithWindows;next.StartupInstallationId=shell.Settings.StartupInstallationId;next.AutoStartTools=shell.Settings.AutoStartTools;next.StartupInTray=shell.Settings.StartupInTray;shell.ApplySettings(next);Window.Close();}catch(Exception ex){error.Text=L.Error(ex);files.Log("Settings import: "+ex);}};
        var save=SettingsLayout.Finish(Window,"SaveSettings","Save");var disclosure=SettingsLayout.Additional(Window,"SettingsAdditional",extra);var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});footer.ColumnDefinitions.Add(new ColumnDefinition());Window.RegisterName("SettingsFooter",footer);footer.Children.Add(disclosure);var version=new TextBlock{Text=BuildInfo.Caption,FontSize=11,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};ThemeManager.Bind(version,TextBlock.ForegroundProperty,"Muted");Window.RegisterName("ApplicationVersion",version);Grid.SetColumn(version,1);footer.Children.Add(version);Grid.SetColumn(save,2);footer.Children.Add(save);SettingsLayout.PinActions(Window,footer);
        save.Click+=async(_,_)=>{try{var next=shell.Settings.Copy();next.Backdrop=backdrop.SelectedIndex==2?"solid":backdrop.SelectedIndex==1?"acrylic":"mica";next.CloseToTray=close.IsChecked==true;next.ConfirmStartStop=confirm.IsChecked==true;next.StartWithWindows=startup.IsChecked==true;next.AutoStartTools=tools.IsChecked==true;next.StartupInTray=hidden.IsChecked==true;if(!await shell.ApplyGeneralSettingsAsync(next))return;shell.ShowNotice(L.T("Settings saved."));Window.Close();}catch(Exception ex){StartupInfo();error.Text=LogSafety.Redact(L.Error(ex));files.Log("General settings: "+ex);}};
        LocalizedTree.Bind(Window);
    }
}
internal static class WindowNames
{
    public static void NameScopeSetup(this Window window)=>NameScope.SetNameScope(window,new NameScope());
}
