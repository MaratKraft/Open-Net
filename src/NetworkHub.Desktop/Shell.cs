using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using NetworkHub.App;
using NetworkHub.Core;

namespace NetworkHub.Desktop;
public sealed class Shell : IDisposable
{
    private readonly AppFiles files;
    private readonly DispatcherTimer timer;
    private readonly ModuleReadiness readiness;
    internal IReadOnlyDictionary<string,ModuleHealth> HealthStates {get;private set;}=new Dictionary<string,ModuleHealth>();
    private readonly Dictionary<string,string> errors=new();
    private readonly Dictionary<string,string> catalogErrors=new();
    private System.Drawing.Icon icon;
    private bool exiting,updatingChecks;
    private Task? refreshTask;
    private bool stopping;
    private bool configuring;
    private Window? settingsWindow;
    private readonly Dictionary<string,Window> moduleWindows=new();
    private readonly Dictionary<string,string> moduleMessages=new();
    public event Action? StateChanged;
    public AutostartService Autostart {get;}
    public bool Busy {get;private set;}
    public int ExitCode {get;set;}
    public Window Window {get;}
    public UserSettings Settings {get;private set;}
    public ToolRuntime Runtime {get;}
    public ModuleInstaller Installer {get;}
    private string? installing;
    public Forms.NotifyIcon Tray {get;}
    public Forms.ToolStripMenuItem TrayOpen {get;}
    public Forms.ToolStripMenuItem TrayStart {get;}
    public Forms.ToolStripMenuItem TrayStop {get;}
    public Forms.ToolStripMenuItem TrayExit {get;}
    public ToolObservation[] Observations {get;private set;}=Array.Empty<ToolObservation>();
    public Button Primary => Find<Button>("Primary");
    public Shell(AppFiles files,bool testMode,SingleInstance instance):this(files,testMode,instance,null) {}
    internal Shell(AppFiles files,bool testMode,SingleInstance instance,System.Net.Http.HttpMessageHandler? installerHandler,Func<string,bool>? runningProbe=null)
    {
        this.files=files;readiness=new ModuleReadiness(files);ThemeManager.SetMode("system");
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("NetworkHub.MainView.xaml")!;
        Window=(Window)XamlReader.Load(stream);
        Window.SourceInitialized+=(_,_)=>{WindowFrame.Attach(Window);WindowSizing.Fit(Window);WindowAppearance.Apply(Window,files,()=>Settings?.Backdrop??"mica");};
        Window.DpiChanged+=(_,_)=>WindowSizing.Fit(Window);
        Application.Current.Resources.MergedDictionaries.Add(Window.Resources);
        try {Settings=files.Load();} catch(Exception ex)
        {
            Settings=files.Defaults();errors["settings"]=ex.Message;files.Log("Settings load: "+ex.Message);
            if(File.Exists(files.SettingsPath)){string backup=Paths.Owned(files.Data,"Backups/corrupt-settings-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".json");Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(files.SettingsPath,backup);}
        }
        ThemeManager.SetMode(Settings.Theme);L.Set(Settings.Language);LocalizedTree.Bind(Window);Find<Image>("Logo").Source=IconFactory.Logo();
        if(!testMode&&(installerHandler!=null||runningProbe!=null))throw new InvalidOperationException("Test installer overrides require safe mode.");
        Installer=new ModuleInstaller(files,installerHandler,runningProbe);Installer.RecoverInterruptedDownloads();
        try{Settings=Installer.ApplyManaged(Settings);}catch(Exception ex){errors["settings"]=ex.Message;files.Log("Module manifest: "+ex.Message);}
        if(!File.Exists(files.SettingsPath))files.Save(Settings);
        Runtime=new ToolRuntime(files,testMode);
        Autostart=new AutostartService(files,testMode?new IsolatedStartupStore():new RegistryStartupStore(),Environment.ProcessPath!);
        icon=IconFactory.TrayIcon(96);Window.Icon=IconFactory.WindowIcon();FocusCues.Attach(Window);
        updatingChecks=true; Find<CheckBox>("ZapretCheck").IsChecked=Settings.ZapretSelected; Find<CheckBox>("TgCheck").IsChecked=Settings.TgSelected;updatingChecks=false;
        Find<CheckBox>("ZapretCheck").Checked+=ChoiceChanged; Find<CheckBox>("ZapretCheck").Unchecked+=ChoiceChanged;
        Find<CheckBox>("TgCheck").Checked+=ChoiceChanged; Find<CheckBox>("TgCheck").Unchecked+=ChoiceChanged;
        Find<Button>("Minimize").Click+=(_,_)=>Window.WindowState=WindowState.Minimized;
        Find<Button>("Close").Click+=(_,_)=>HideToTray();
        Find<Button>("LogoLink").Click+=(_,_)=>OpenRepository();
        Find<Button>("Settings").Click+=(_,_)=>OpenSettings();
        Find<Button>("ZapretSettings").Click+=(_,_)=>OpenModuleSettings("zapret");
        Find<Button>("TgSettings").Click+=(_,_)=>OpenModuleSettings("tg");
        Primary.Click+=async(_,_)=> {await Refresh();if(PrimaryAction.Stop(Observations,Selected())) await StopSelected(false);else await StartSelected();};
        Find<Button>("ZapretInstall").Click+=async(_,_)=>await InstallOrCheck("zapret");
        Find<Button>("TgInstall").Click+=async(_,_)=>await InstallOrCheck("tg");
        foreach(string prefix in new[]{"Zapret","Tg"})ModuleIconAlignment.Attach(Find<Button>(prefix+"Install"),Find<Button>(prefix+"Settings"),Find<CheckBox>(prefix+"Check"));
        Window.Closing+=OnClosing;
        TrayOpen=new Forms.ToolStripMenuItem(L.T("Open Open Net")); TrayStart=new Forms.ToolStripMenuItem(L.T("Start selected"));
        TrayStop=new Forms.ToolStripMenuItem(L.T("Stop managed processes")); TrayExit=new Forms.ToolStripMenuItem(L.T("Exit"));
        var menu=new Forms.ContextMenuStrip(); menu.Items.AddRange(new Forms.ToolStripItem[]{TrayOpen,new Forms.ToolStripSeparator(),TrayStart,TrayStop,new Forms.ToolStripSeparator(),TrayExit});
        Tray=new Forms.NotifyIcon {Icon=icon,Text="Open Net",ContextMenuStrip=menu,Visible=true};
        void UpdateIcon(){var next=IconFactory.TrayIcon((uint)Math.Round(VisualTreeHelper.GetDpi(Window).DpiScaleX*96));Tray.Icon=next;var previous=icon;icon=next;previous.Dispose();}
        Window.SourceInitialized+=(_,_)=>UpdateIcon();Window.DpiChanged+=(_,_)=>UpdateIcon();
        ThemeManager.StyleTray(Tray.ContextMenuStrip!);ThemeManager.Changed+=ApplyTheme;
        Tray.DoubleClick+=(_,_)=>Window.Dispatcher.Invoke(Restore);
        void LanguageChanged(){TrayOpen.Text=L.T("Open Open Net");TrayStart.Text=L.T("Start selected");TrayStop.Text=L.T("Stop managed processes");TrayExit.Text=L.T("Exit");ShowMessage(L.T("Close the window — Open Net stays in the tray."));_ =Refresh();Render();}
        L.Changed+=LanguageChanged;Window.Closed+=(_,_)=>L.Changed-=LanguageChanged;
        TrayOpen.Click+=(_,_)=>Restore(); TrayStart.Click+=async(_,_)=>await StartSelected();
        TrayStop.Click+=async(_,_)=>await StopSelected(true); TrayExit.Click+=(_,_)=>Exit();
        timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};timer.Tick+=async(_,_)=>await Refresh();timer.Start();
        Window.Loaded+=async(_,_)=>{await Refresh();if(Runtime.TestMode)ShowMessage(L.T("Safe testing: buttons run only our test processes."));else if(errors.ContainsKey("settings"))ShowMessage(L.T("Settings are damaged. Save them again; the original and a backup have been kept."));else if(Settings.StartWithWindows){var state=Autostart.Inspect(Settings);if(state.NeedsRepair||!state.Registered)ShowMessage(state.Message);}else if(!string.Equals(files.Data,Path.Combine(files.Root,"Data"),StringComparison.OrdinalIgnoreCase))ShowMessage(L.T("Data: ")+files.Data);};
    }
    public T Find<T>(string name) where T:class => (T)Window.FindName(name);
#if OPENNET_TESTING
    internal Action<System.Diagnostics.ProcessStartInfo>? RepositoryLaunchForTests {get;set;}
#endif
    private void OpenRepository()
    {
        try
        {
#if OPENNET_TESTING
            if(Runtime.TestMode){RepositoryLaunchForTests?.Invoke(RepositoryLink.Plan());return;}
#endif
            RepositoryLink.Open();
        }
        catch(Exception ex){files.Log("Open repository: "+ex);ShowMessage(L.T("Could not open the browser. Open github.com/MaratKraft/Open-Net manually."));}
    }
    private void ChoiceChanged(object sender,RoutedEventArgs e)
    {
        if(updatingChecks) return;
        Settings.ZapretSelected=Find<CheckBox>("ZapretCheck").IsChecked==true;Settings.TgSelected=Find<CheckBox>("TgCheck").IsChecked==true;
        try {files.Save(Settings);} catch(Exception ex) {ShowMessage(L.T("Could not save the selection: ")+ex.Message);} Render();
    }
    private string[] Selected()=>new[]{Settings.ZapretSelected?"zapret":"",Settings.TgSelected?"tg":""}.Where(x=>x!="").ToArray();
    private bool IsActive(string id)=>Observations.Any(x=>x.Id==id&&(x.State==ObservedState.Running||x.State==ObservedState.External||x.State==ObservedState.CleanupRequired));
    public Task Refresh()
    {
        if(Busy)return Task.CompletedTask;
        return refreshTask??=RefreshCore();
    }
    private async Task RefreshCore()
    {
        try {var snapshot=Settings.Copy();var result=await Task.Run(()=>{var observations=ModuleCatalog.All.Select(module=>Runtime.Observe(module.Id,snapshot)).ToArray();var health=ModuleCatalog.All.ToDictionary(module=>module.Id,module=>readiness.Read(module.Id,snapshot));return (observations,health);});Observations=result.observations;HealthStates=result.health;Render();}
        finally {refreshTask=null;}
    }
    private static string Label(ObservedState s)=>s switch {ObservedState.Stopped=>L.T("Stopped"),ObservedState.Starting=>L.T("Starting"),ObservedState.Running=>L.T("Running"),ObservedState.Error=>L.T("Error"),ObservedState.External=>L.T("External instance"),ObservedState.CleanupRequired=>L.T("WinDivert is still loaded"),ObservedState.NotInstalled=>L.T("Not installed"),_=>L.T("Unknown state")};
    private void Render()
    {
        foreach(var o in Observations)
        {
            var text=Find<TextBlock>(o.Id=="zapret"?"ZapretState":"TgState");
            bool failed=errors.ContainsKey(o.Id)&&(o.State==ObservedState.Stopped||o.State==ObservedState.NotInstalled);
            text.Text=failed?L.T("Installation / launch failed"):Label(o.State)+(o.Pid.HasValue?" · PID "+o.Pid:"");
            if(o.State==ObservedState.Error)text.Text=o.Detail;
            text.TextWrapping=TextWrapping.Wrap;
            text.ToolTip=failed?errors[o.Id]:o.Detail;
            text.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:ThemeManager.Brush(o.State==ObservedState.Running?"Success":o.State==ObservedState.Error||failed?"Error":o.State==ObservedState.External||o.State==ObservedState.Unknown?"Warning":"Muted");
        }
        Find<TextBlock>("ZapretStrategy").Text=Settings.Strategy==""?"":"· "+Path.GetFileNameWithoutExtension(Settings.Strategy);Find<TextBlock>("ZapretStrategy").ToolTip=Settings.Strategy;
        foreach(string id in ModuleCatalog.All.Select(x=>x.Id).ToArray())
        {
            var button=Find<Button>(id=="zapret"?"ZapretInstall":"TgInstall");
            var health=HealthStates.TryGetValue(id,out var known)?known:new ModuleHealth(false,null,"");
            bool working=installing==id;string caption=working?L.T("Downloading…"):ModuleActionPresentation.Caption(health.Installed);
            var version=Find<TextBlock>(id=="zapret"?"ZapretVersion":"TgVersion");version.Text=health.Version??"";version.ToolTip=health.Version;
            var glyph=Find<System.Windows.Shapes.Path>(id=="zapret"?"ZapretInstallGlyph":"TgInstallGlyph");glyph.Data=Geometry.Parse(ModuleActionPresentation.Glyph(health.Installed));glyph.Visibility=working?Visibility.Collapsed:Visibility.Visible;
            Find<ProgressBar>(id=="zapret"?"ZapretInstallProgress":"TgInstallProgress").Visibility=working?Visibility.Visible:Visibility.Collapsed;
            button.ToolTip=caption+(catalogErrors.TryGetValue(id,out var failure)?"\n"+L.Current(failure):"");System.Windows.Automation.AutomationProperties.SetName(button,caption);button.IsEnabled=!Busy;
        }
        string[] selected=Selected(); int active=selected.Count(IsActive);
        string title=Busy?(configuring?L.T("Original Zapret menu is open"):installing!=null?L.T("Installing a module"):stopping?L.T("Stopping tools"):L.T("Starting tools")):selected.Length==0?L.T("Select tools"):active==selected.Length?L.T("Processes are running"):active>0?L.T("Some tools are running"):selected.Any(id=>Observations.Any(o=>o.Id==id&&(o.State==ObservedState.Error||o.State==ObservedState.Unknown))||errors.ContainsKey(id))?L.T("Attention required"):L.T("Ready to start");
        if(!Busy&&Observations.Any(x=>selected.Contains(x.Id)&&x.State==ObservedState.CleanupRequired))title=L.T("Driver cleanup required");
        else if(!Busy&&Observations.Any(x=>selected.Contains(x.Id)&&x.State==ObservedState.External))title=L.T("External processes detected");
        Find<TextBlock>("Summary").Text=title; Find<TextBlock>("Selection").Text=L.T("Selected tools: ")+selected.Length;
        bool canStop=PrimaryAction.Stop(Observations,selected),externalOnly=PrimaryAction.ExternalOnly(Observations,selected);
        Primary.Content=Busy?(configuring?L.T("Console is open…"):stopping?L.T("Stopping…"):installing!=null?L.T("Installing…"):L.T("Starting…")):canStop?L.T("Stop selected"):externalOnly?L.T("External instance"):L.T("Start selected");Primary.IsEnabled=!Busy&&selected.Length>0&&!externalOnly;
        Primary.ToolTip=selected.Any(id=>Observations.Any(x=>x.Id==id&&x.State==ObservedState.External))?L.T("Close external tools using their own window or tray. Open Net manages only processes it launched."):null;
        var brush=ThemeManager.Brush(active>0?"Success":"Muted");Find<System.Windows.Shapes.Path>("PowerArc").Stroke=brush;Find<System.Windows.Shapes.Path>("PowerLine").Stroke=brush;
        Tray.Text="Open Net · "+(active>0?active+L.T(" processes"):L.T("ready")); TrayStart.Enabled=!Busy&&selected.Length>0&&!externalOnly;TrayStop.Enabled=!Busy&&Observations.Any(PrimaryAction.CanStop);
        if(settingsWindow!=null)settingsWindow.IsEnabled=!Busy||installing!=null;
        Find<CheckBox>("ZapretCheck").IsEnabled=!Busy||installing!=null;Find<CheckBox>("TgCheck").IsEnabled=!Busy||installing!=null;Find<Button>("Settings").IsEnabled=!Busy||installing!=null;
        if(files.LogFailure!=null)ShowMessage(L.T("Could not write the log: ")+files.LogFailure);
        StateChanged?.Invoke();
    }
    public async Task StartSelected()
    {
        if(Busy) return;await Refresh();if(Busy)return;string[] ids=Selected();if(ids.Length==0){ShowMessage(L.T("Select at least one tool."));return;}
        bool authorized=Runtime.TestMode;
        if(Runtime.TestMode&&RoutineConfirmationForTests!=null&&!ConfirmLaunch(ids))return;
        if(!Runtime.TestMode)
        {
            var fresh=ids.Select(id=>Runtime.Observe(id,Settings)).ToArray();
            var available=fresh.Where(o=>o.State==ObservedState.Stopped).Select(o=>o.Id).ToArray();
            if(available.Length==0){ShowMessage(string.Join("\n",fresh.Select(o=>o.Detail)));await Refresh();return;}
            if(!ConfirmLaunch(available)) return;
            authorized=true;
        }
        Busy=true;Render();var messages=new List<string>();
        try
        {
            foreach(string id in ids)
            {
                errors.Remove(id);
                try {messages.Add((id=="zapret"?"Zapret: ":"TG: ")+await Runtime.Start(id,Settings,authorized));}
                catch(Exception ex) {errors[id]=L.Error(ex);messages.Add((id=="zapret"?"Zapret: ":"TG: ")+errors[id]);files.Log(id+" start: "+ex);}
            }
        }
        finally {Busy=false;await Refresh();ShowMessage(string.Join("\n",messages));}
    }
    private bool ConfirmLaunch(string[] ids)
    {
        if(!Settings.ConfirmStartStop)return true;
        string message=L.T("Tools to start: ")+string.Join(", ",ids.Select(x=>x=="zapret"?"Zapret":"TG WS Proxy"))+".\n\n";
        if(ids.Contains("zapret")) message+=L.T("Zapret will request UAC and may load WinDivert and change network traffic processing.\n");
        if(ids.Contains("tg")) message+=L.T("TG opens its own tray and may create a listener and external connections. It manages its own settings.\n");
        message+=L.T("\nStop ends owned processes, including TG child processes. Zapret requires UAC. Allow this launch?");
        return AskRoutine("start",message,L.T("Open Net — launch permission"));
    }
    internal Func<string,bool>? RoutineConfirmationForTests {get;set;}
    private bool AskRoutine(string kind,string message,string title)=>Runtime.TestMode?RoutineConfirmationForTests?.Invoke(kind)??true:AppDialog.Show(Window,message,title,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
    public async Task StopSelected(bool allManaged)
    {
        if(Busy)return;await Refresh();if(Busy)return;
        string[] ids=allManaged?ModuleCatalog.All.Select(x=>x.Id).ToArray():Selected();
        if(ids.Length==0){ShowMessage(L.T("Select at least one tool."));return;}
        if(!Observations.Any(x=>ids.Contains(x.Id)&&PrimaryAction.CanStop(x))){ShowMessage(Observations.Any(x=>ids.Contains(x.Id)&&x.State==ObservedState.External)?L.T("Close external tools using their own window or tray. Open Net manages only processes it launched."):L.T("The process is not running."));return;}
        bool ownRunning=Observations.Any(x=>ids.Contains(x.Id)&&x.State==ObservedState.Running);
        if(Settings.ConfirmStartStop&&ownRunning&&!AskRoutine("stop",L.T("Only processes launched by this copy of Open Net will be ended. TG and its child processes may be terminated forcibly; unsaved settings may be lost. Zapret and WinDivert require UAC. Continue?"),L.T("Open Net — stop")))return;
        if(!Settings.ConfirmStartStop&&!Runtime.TestMode&&Observations.Any(x=>ids.Contains(x.Id)&&x.Id=="tg"&&x.State==ObservedState.Running)&&!ConfirmForcedTgStop())return;
        stopping=true;Busy=true;Render();ShowMessage(L.T("Stopping owned processes. Confirm the Windows prompt for Zapret."));var messages=new List<string>();
        try
        {
            foreach(string id in ids)
            {
                try{messages.Add((id=="zapret"?"Zapret: ":"TG: ")+await Runtime.Stop(id,Settings));errors.Remove(id);}
                catch(Exception ex){messages.Add((id=="zapret"?"Zapret: ":"TG: ")+(ex is Win32Exception w&&w.NativeErrorCode==1223?L.T("Stop was cancelled in the UAC prompt."):L.Error(ex)));files.Log(id+" stop: "+ex);}
            }
        }
        catch(Exception ex){messages.Add(ex.Message);files.Log("stop: "+ex.Message);}
        finally {Busy=false;stopping=false;await Refresh();ShowMessage(string.Join("\n",messages));}
    }
    public void ShowMessage(string message) {string safe=NetworkHub.Core.LogSafety.Redact(message);Find<TextBlock>("Message").Text=safe;Find<TextBlock>("Message").ToolTip=safe;if(installing!=null)moduleMessages[installing]=safe;StateChanged?.Invoke();}
    internal bool ConfirmForcedTgStop()=>AppDialog.Show(Window,L.T("TG may be stopped forcibly with its child processes. Unsaved settings may be lost. Stop this owned instance?"),L.T("Open Net — stop"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
    public async Task ConfigureZapret(string folder,Window owner)
    {
        if(Busy)return;bool confirmed=false;string result="";
        try
        {
            await ZapretActions.Configure(folder,()=>
            {
                if(AppDialog.Show(owner,L.T("Open the original Zapret console utility? It can install or remove services, change network settings and stop other processes. Windows will request administrator permission. No menu items will be selected automatically."),L.T("Configure Zapret"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return false;
                confirmed=true;Busy=true;configuring=true;Render();return true;
            },Runtime.TestMode?plan=>{files.Log("Safe test: service launch plan only; original BAT not executed. "+plan.FileName);return Task.CompletedTask;}:null,files);
            if(confirmed)result=L.T("The original console closed. Component state has been refreshed.");
        }
        catch(Exception ex){result=L.Error(ex);files.Log("Zapret console: "+ex);}
        finally{if(confirmed){Busy=false;configuring=false;}await Refresh();if(result!="")ShowMessage(result);}
    }
    public async Task InstallOrCheck(string id)
    {
        if(Busy)return;Busy=true;installing=id;Render();ShowMessage(L.T("Retrieving official GitHub releases…"));
        try
        {
            var asset=await Installer.Discover(id);var current=Installer.RepairableCurrent(id);
            string cached=Installer.UsingCache?L.T("Using the saved catalog from ")+Installer.CatalogTimestamp?.ToLocalTime().ToString("g")+L.T(". It may be outdated. Installed modules remain available.")+"\n":"";
            catalogErrors.Remove(id);if(Installer.UsingCache)catalogErrors[id]=cached;
            bool intact=current!=null&&(id=="tg"?new TgExeAdapter(files).Validate(files.ModulePath(current.RelativePath))==null:!new NetworkHub.Adapters.ZapretAdapter(files.ForZapret(new UserSettings{ZapretDirectory=files.ModulePath(current.RelativePath)})).Validate().Any(x=>x.Error));
            if(current?.Version==asset.Version&&intact){ShowMessage(cached+L.T("Installed version with supported file format: ")+asset.Version+L.T(". Latest published: ")+asset.Latest);return;}
            string info=(id=="zapret"?"Zapret":"TG WS Proxy")+" "+asset.Version+L.T("\nFile: ")+asset.Name+L.T("\nSize: ")+(asset.Size/1048576.0).ToString("F1")+L.T(" MB\nSource: ")+asset.Source+L.T("\nLatest published: ")+asset.Latest+L.T("\n\nInstall the files? Tools will not start automatically.");
            bool approved=Runtime.TestMode&&InstallConfirmationForTests!=null?InstallConfirmationForTests(cached+info):AppDialog.Show(Window,cached+info,L.T("Open Net — installation"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
            if(!approved)return;
            await Installer.Install(asset,new Progress<int>(value=>ShowMessage(L.T("Downloading ")+asset.Version+": "+value+"%")));
            Settings=Installer.ApplyManaged(Settings);files.Save(Settings);ShowMessage(L.T("Installed version ")+asset.Version+L.T(". Use the main button to start it."));
            errors.Remove(id);
        }
        catch(Exception ex){catalogErrors[id]=L.Error(ex);ShowMessage(catalogErrors[id]);files.Log("Catalog: "+ex);}
        finally{Busy=false;installing=null;await Refresh();}
    }
    // Internal safe-mode seam: the release UI always uses the explicit confirmation dialog.
    internal Func<string,bool>? InstallConfirmationForTests {get;set;}
    public async Task ImportRelease(string id,string path,Window owner)
    {
        if(Busy)return;Busy=true;installing=id;Render();
        try{
            ShowMessage(L.T("Checking official release metadata…"));var asset=await Installer.Discover(id);
            string text=(Installer.UsingCache?L.T("Using the saved catalog from ")+Installer.CatalogTimestamp?.ToLocalTime().ToString("g")+L.T(". It may be outdated. Installed modules remain available.")+"\n":"")+L.T("Import the selected file as official release ")+asset.Version+"?\n"+path+"\nSHA-256: "+(asset.Digest??L.T("Unavailable"));
            bool approved=Runtime.TestMode&&InstallConfirmationForTests!=null?InstallConfirmationForTests(text):AppDialog.Show(owner,text,L.T("Import official release"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
            if(!approved)return;await Installer.Import(asset,path);Settings=Installer.ApplyManaged(Settings);files.Save(Settings);ShowMessage(L.T("Official release imported. Tools were not started."));
        }catch(Exception ex){ShowMessage(L.Error(ex));files.Log("Import release: "+ex);}
        finally{Busy=false;installing=null;await Refresh();}
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(exiting)return;
        if(Settings.CloseToTray){e.Cancel=true;HideToTray();}
        else {e.Cancel=true;Window.Dispatcher.BeginInvoke(new Action(Exit));}
    }
    public void HideToTray(){Window.ShowInTaskbar=false;Window.Hide();}
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    public void Restore()
    {
        FocusCues.SetMode(false);
        Window.ShowInTaskbar=true;Window.Show();Window.WindowState=WindowState.Normal;Window.Activate();
        var handle=new WindowInteropHelper(Window).Handle;BringWindowToTop(handle);SetForegroundWindow(handle);Window.Focus();
    }
    public void Exit() {if(exiting)return;if(Busy){ShowMessage(L.T("Wait for the current operation to finish before exiting."));return;}exiting=true;timer.Stop();Tray.Visible=false;Window.Close();Application.Current.Shutdown();}
    public void ApplySettings(UserSettings settings) {files.Save(settings);if(Settings.Language!=settings.Language){moduleMessages.Clear();}Settings=settings;ThemeManager.SetMode(settings.Theme);L.Set(settings.Language);WindowAppearance.Refresh(Window);errors.Clear();_ =Refresh();Render();}
    public void ApplyGeneralSettings(UserSettings settings)
    {
        if(settings.StartWithWindows!=Settings.StartWithWindows)Autostart.Configure(settings,()=>ApplySettings(settings));
        else ApplySettings(settings);
    }
    public void RepairStartup(){var next=Settings.Copy();next.StartWithWindows=true;Autostart.Configure(next,()=>ApplySettings(next));}
    public void StartHidden(){Window.ShowInTaskbar=false;new WindowInteropHelper(Window).EnsureHandle();_ =Refresh();}
    public static string StateLabel(ObservedState state)=>Label(state);
    public ModuleDetails DescribeModule(string id)
    {
        var definition=ModuleCatalog.Get(id);var observation=Observations.FirstOrDefault(x=>x.Id==id)??new ToolObservation(id,ObservedState.Unknown,L.T("Checking"));
        string? version=null;string diagnostic=errors.TryGetValue(id,out var error)?error:catalogErrors.TryGetValue(id,out var catalog)?catalog:moduleMessages.TryGetValue(id,out var message)?message:observation.Detail;
        try{version=Installer.Current(id)?.Version;}catch(Exception ex){diagnostic=L.Error(ex);}
        return new(definition,Settings.ForModule(id),version,observation,definition.Start&&observation.State==ObservedState.Stopped,definition.Stop&&PrimaryAction.CanStop(observation),L.Current(diagnostic));
    }
    public void RollbackModule(string id){Installer.Rollback(id);ApplySettings(Installer.ApplyManaged(Settings));ShowMessage(L.T("The previous build has been restored."));}
    public Window OpenSettings()
    {
        if(settingsWindow!=null){settingsWindow.Activate();return settingsWindow;}
        var view=new SettingsView(this,files);settingsWindow=view.Window;settingsWindow.Owner=Window;
        settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();return settingsWindow;
    }
    public Window OpenModuleSettings(string id)
    {
        if(moduleWindows.TryGetValue(id,out var existing)){existing.Activate();return existing;}
        var view=new ModuleSettingsView(this,files,id);moduleWindows[id]=view.Window;view.Window.Closed+=(_,_)=>moduleWindows.Remove(id);view.Window.Show();return view.Window;
    }
    private void ApplyTheme(){WindowAppearance.Refresh(Window);ThemeManager.StyleTray(Tray.ContextMenuStrip!);Render();}
    public void Dispose() {ThemeManager.Changed-=ApplyTheme;timer.Stop();Tray.Visible=false;Tray.ContextMenuStrip?.Dispose();Tray.Dispose();icon.Dispose();Installer.Dispose();}
}












