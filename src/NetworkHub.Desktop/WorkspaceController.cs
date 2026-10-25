using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;

internal sealed class WorkspaceController
{
    readonly Shell shell;readonly AppFiles files;readonly StackPanel list;
    readonly System.Collections.Generic.Dictionary<string,Border> rows=new();
    readonly System.Collections.Generic.Dictionary<string,Button> profiles=new();
    readonly System.Collections.Generic.Dictionary<string,ModuleStatusLine> statusLines=new();
    readonly Button assignment,hidden;bool arranging;double resizeStart;string? pendingStrategy;Window? manager;
    internal bool Editing=>OrderEditor?.IsDragging==true;
    internal ModuleOrderEditor? OrderEditor {get;private set;}
    WorkspaceSettings State=>shell.Settings.Workspace;
    internal static string Name(string id)=>id switch{"zapret"=>"Zapret","tg"=>"TG WS Proxy","dns"=>"DNS Manager","group"=>L.T("Selected group"),_=>L.T("Unavailable module")};
    public WorkspaceController(Shell shell,AppFiles files)
    {
        this.shell=shell;this.files=files;shell.Settings.Workspace??=new();State.Normalize();pendingStrategy=string.IsNullOrEmpty(shell.Settings.PendingStrategy)?null:shell.Settings.PendingStrategy;
        var w=shell.Window;list=shell.Find<StackPanel>("ModuleList");
        foreach(var pair in new[]{("zapret","ZapretRow"),("tg","TgRow"),("dns","DnsRow")})rows[pair.Item1]=shell.Find<Border>(pair.Item2);
        list.Children.Clear();
        foreach(var pair in rows){var row=pair.Value;row.Padding=new Thickness(0,13,0,13);row.MinHeight=70;var grid=(Grid)row.Child;
            if(pair.Key=="dns"){
                var gear=shell.Find<Button>("DnsSettings");grid.Children.Remove(gear);var group=new ModuleActionGroup();var refresh=new Button{Style=(Style)w.FindResource("ModuleAction"),ToolTip=L.T("Check actual DNS")};refresh.Content=new System.Windows.Shapes.Path{Data=(Geometry)w.FindResource("RefreshGeometry"),Style=(Style)w.FindResource("IconArrow")};refresh.Click+=(_,_)=>shell.UpdateDnsStatus();group.Children.Add(refresh);group.Children.Add(gear);var check=new CheckBox{Style=(Style)w.FindResource("ModuleSelection"),IsChecked=State.DnsInGroup,ToolTip=L.T("Include in group")};check.Checked+=(_,_)=>{State.DnsInGroup=true;Save();Render();};check.Unchecked+=(_,_)=>{State.DnsInGroup=false;Save();Render();};group.Children.Add(check);Grid.SetColumn(group,2);grid.Children.Add(group);ModuleIconAlignment.Attach(refresh,gear,check);
            }
            row.BorderThickness=new Thickness(0);row.Padding=new Thickness(0,11,0,11);
            var status=grid.Children.OfType<TextBlock>().First(t=>Grid.GetRow(t)==1);status.TextWrapping=TextWrapping.NoWrap;status.TextTrimming=TextTrimming.CharacterEllipsis;
            if(pair.Key!="tg"){
                grid.Children.Remove(status);var line=new ModuleStatusLine(w,status);Grid.SetColumn(line,1);Grid.SetRow(line,1);Grid.SetColumnSpan(line,2);grid.Children.Add(line);statusLines[pair.Key]=line;
                string id=pair.Key;var selector=line.Selector;selector.Name=id=="zapret"?"ZapretQuick":"DnsQuick";selector.Click+=(_,_)=>ProfileMenu(id,selector);profiles[id]=selector;w.RegisterName(selector.Name,selector);
            }else{Grid.SetColumnSpan(status,2);status.Height=18;}
        }
        shell.Find<TextBlock>("ZapretStrategy").Visibility=Visibility.Collapsed;
        var summary=shell.Find<TextBlock>("Summary");summary.TextWrapping=TextWrapping.NoWrap;summary.TextTrimming=TextTrimming.CharacterEllipsis;summary.Height=32;
        var selection=shell.Find<TextBlock>("Selection");var hero=(StackPanel)selection.Parent;assignment=Button("",0);assignment.Height=28;assignment.Padding=new Thickness(8,3,8,3);assignment.Background=Brushes.Transparent;assignment.Margin=selection.Margin;selection.Visibility=Visibility.Collapsed;hero.Children.Insert(hero.Children.IndexOf(selection)+1,assignment);w.RegisterName("MainAssignment",assignment);assignment.Click+=(_,_)=>AssignmentMenu();
        var foot=(Grid)shell.Find<Button>("Settings").Parent;var version=foot.Children.OfType<TextBlock>().First();version.Visibility=Visibility.Collapsed;var modules=Button("Modules…");modules.Style=(Style)w.FindResource("QuietLink");modules.Height=28;modules.ClearValue(Control.BackgroundProperty);ThemeManager.Bind(modules,Control.ForegroundProperty,"Muted");modules.Padding=new Thickness(4,0,4,0);modules.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(modules,1);foot.Children.Add(modules);w.RegisterName("ModulesEntry",modules);modules.Click+=(_,_)=>OpenManager();
        var settingsEntry=shell.Find<Button>("Settings");settingsEntry.Style=(Style)w.FindResource("QuietLink");ThemeManager.Bind(settingsEntry,Control.ForegroundProperty,"Muted");settingsEntry.Height=28;settingsEntry.Padding=new Thickness(4,0,4,0);ThemeManager.Bind(shell.Find<TextBlock>("SettingsCaption"),TextBlock.ForegroundProperty,"Muted");ThemeManager.Bind(shell.Find<System.Windows.Shapes.Path>("SettingsGear"),System.Windows.Shapes.Shape.FillProperty,"Muted");var message=shell.Find<TextBlock>("Message");message.Text="";message.Height=16;message.MaxHeight=16;message.Margin=new Thickness(0,8,0,8);((Grid)shell.Find<Button>("Settings").Parent).RowDefinitions.Clear();((Grid)((Border)w.Content).Child).RowDefinitions[2].Height=new GridLength(32);hidden=Button("");hidden.HorizontalAlignment=HorizontalAlignment.Right;hidden.Background=Brushes.Transparent;hidden.FontSize=11;hidden.Visibility=Visibility.Collapsed;Grid.SetColumn(hidden,0);hidden.Margin=new Thickness(90,0,8,0);foot.Children.Add(hidden);hidden.Click+=(_,_)=>OpenManager();
        var factory=new FrameworkElementFactory(typeof(Border));factory.SetValue(Border.BackgroundProperty,Brushes.Transparent);var grip=new System.Windows.Controls.Primitives.Thumb{Height=6,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,-14),Cursor=Cursors.SizeNS,Template=new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb)){VisualTree=factory}};
        var mainGrid=(Grid)((Border)w.Content).Child;Grid.SetRowSpan(grip,3);mainGrid.Children.Add(grip);grip.DragDelta+=(_,e)=>{w.Height=Math.Max(w.MinHeight,w.Height+e.VerticalChange);WindowSizing.Fit(w);};grip.DragCompleted+=(_,_)=>{State.ManualHeight=true;State.Height=w.Height;Save();};
        w.MaxWidth=460;w.MinHeight=450;w.ResizeMode=ResizeMode.CanResize;w.PreviewKeyDown+=(_,e)=>{if(Editing&&e.Key==Key.Escape){manager?.Close();e.Handled=true;}};
        w.SourceInitialized+=(_,_)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(w).Handle);IntPtr Hook(IntPtr h,int m,IntPtr wp,IntPtr lp,ref bool handled){if(m==0x231)resizeStart=w.Height;if(m==0x232){if(Math.Abs(resizeStart-w.Height)>1){State.ManualHeight=true;State.Height=w.Height;}State.Left=w.Left;State.Top=w.Top;Save();}return IntPtr.Zero;}source.AddHook(Hook);w.Closed+=(_,_)=>source.RemoveHook(Hook);Reload();};
        w.Loaded+=(_,_)=>{w.UpdateLayout();Resize();WindowSizing.Fit(w);w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{if(!State.ManualHeight){w.UpdateLayout();Resize();}}));};w.SizeChanged+=(_,_)=>CompactHero();w.DpiChanged+=(_,_)=>Resize();w.Closing+=(_,_)=>{if(!Editing){State.Left=w.Left;State.Top=w.Top;Save();}};Arrange();
    }
    Button Button(string text,int width=0){var b=new Button{Content=L.T(text),FontSize=12,Height=32,Padding=new Thickness(8,2,8,2),Background=ThemeManager.Brush("ButtonSurface")};if(width>0)b.Width=width;ThemeManager.Bind(b,Control.BackgroundProperty,"ButtonSurface");ThemeManager.Bind(b,Control.ForegroundProperty,"Text");return b;}
    internal static string NaturalKey(string value)=>System.Text.RegularExpressions.Regex.Replace(value,@"\d+",m=>m.Value.PadLeft(12,'0'));
    void Save(){try{files.Save(shell.Settings);}catch(Exception ex){shell.ShowMessage(L.Error(ex));}}
    internal void Reload(){State.Normalize();Arrange();if(State.Left is double left&&State.Top is double top){shell.Window.WindowStartupLocation=WindowStartupLocation.Manual;shell.Window.Left=left;shell.Window.Top=top;}Resize();}
    void CompactHero(){var icon=(Grid)shell.Find<System.Windows.Shapes.Path>("PowerArc").Parent;bool small=shell.Window.Height<500;icon.Visibility=shell.Window.Height<420?Visibility.Collapsed:Visibility.Visible;icon.Height=60;icon.LayoutTransform=small?new ScaleTransform(.7333333,.7333333):Transform.Identity;icon.Margin=small?new Thickness(0,12,0,6):new Thickness(0,30,0,13);assignment.Margin=small?new Thickness(0,8,0,12):new Thickness(0,8,0,26);}
    internal double MeasuredHeight(){var w=shell.Window;var surface=(Border)w.Content;double width=Math.Max(240,w.Width-surface.Padding.Left-surface.Padding.Right-2);var hero=(StackPanel)assignment.Parent;var icon=(Grid)shell.Find<System.Windows.Shapes.Path>("PowerArc").Parent;icon.Height=60;icon.LayoutTransform=Transform.Identity;icon.Margin=new Thickness(0,30,0,13);assignment.Margin=new Thickness(0,8,0,26);hero.Measure(new Size(width,double.PositiveInfinity));list.Measure(new Size(width-4,double.PositiveInfinity));var message=shell.Find<TextBlock>("Message");message.Measure(new Size(width,double.PositiveInfinity));return Math.Clamp(Math.Ceiling(surface.Padding.Top+surface.Padding.Bottom+2+42+32+hero.DesiredSize.Height+list.DesiredSize.Height+message.DesiredSize.Height+12),450,780);}
    void Resize(){if(arranging)return;arranging=true;try{shell.Window.Height=State.ManualHeight?State.Height:MeasuredHeight();WindowSizing.Fit(shell.Window);CompactHero();}finally{arranging=false;}}
    void Arrange(){list.Children.Clear();foreach(string id in State.Order.Where(State.Visible.Contains))list.Children.Add(rows[id]);Resize();Render();}
    bool Active(string id)=>id=="dns"?shell.DnsApplied:shell.Observations.Any(o=>o.Id==id&&o.State is ObservedState.Running or ObservedState.External or ObservedState.CleanupRequired);
    internal static string StrategyCaption(string value)=>Path.GetFileNameWithoutExtension(value).Replace("general (","").TrimEnd(')');
    void RenderProfiles()
    {
        string selected=pendingStrategy??shell.Settings.Strategy,active=shell.Runtime.ActiveZapretProfile;
        bool working=shell.Observations.Any(o=>o.Id=="zapret"&&o.State is ObservedState.Running or ObservedState.External);
        statusLines["zapret"].Update(StrategyCaption(active),StrategyCaption(selected),working);
        profiles["zapret"].ToolTip=L.T("Active profile")+": "+(working?(active.Length==0?L.T("Unknown profile"):active):L.T("Stopped"))+"\n"+L.T("Selected profile")+": "+selected;
        shell.Find<TextBlock>("DnsState").Text=shell.DnsLineStatus;shell.Find<TextBlock>("DnsState").Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:ThemeManager.Brush(shell.DnsTone);
        statusLines["dns"].Update(shell.DnsCurrentProfile,shell.Settings.DnsProfileName,shell.DnsCurrentProfile.Length>0);
        profiles["dns"].ToolTip=L.T("Current DNS")+": "+shell.DnsCurrentProfile+"\n"+L.T("Selected profile")+": "+shell.Settings.DnsProfileName;
    }
    internal void Render(){if(assignment==null)return;if(shell.Window.FindName("ModulesEntry") is Button modulesEntry)modulesEntry.Content=CompactUi.Modules(L.T("Modules"));pendingStrategy=string.IsNullOrEmpty(shell.Settings.PendingStrategy)?null:shell.Settings.PendingStrategy;assignment.Content=CompactUi.Disclosure(Editing?L.T("Drag or use Alt+Up/Down"):L.T("Main button:")+" "+Name(State.Primary),assignment.ContextMenu?.IsOpen==true);RenderProfiles();
        int hiddenActive=WorkspaceSettings.Modules.Count(id=>!State.Visible.Contains(id)&&Active(id));hidden.Content=L.T("Hidden active:")+" "+hiddenActive;hidden.Visibility=hiddenActive>0?Visibility.Visible:Visibility.Collapsed;

        if(Editing){shell.Primary.IsEnabled=false;return;}
        if(State.Primary!="group"){bool available=State.Visible.Contains(State.Primary)&&(State.Primary=="dns"||shell.HealthStates.TryGetValue(State.Primary,out var ready)&&ready.Installed);shell.Primary.Content=L.T(!available?"Choose main module":Active(State.Primary)?"Turn off":"Turn on")+" "+Name(State.Primary);shell.Primary.IsEnabled=!shell.Busy&&!shell.DnsBusy; shell.Find<TextBlock>("Summary").Text=!available?L.T(State.Visible.Contains(State.Primary)?"Main module is unavailable":"Main module is hidden"):State.Primary=="dns"?shell.Find<TextBlock>("DnsState").Text:Active(State.Primary)?L.T("Processes are running"):L.T("Ready to start");}
        else {if(shell.DnsBusy)shell.Primary.IsEnabled=false;var ids=new[]{shell.Settings.ZapretSelected?"zapret":"",shell.Settings.TgSelected?"tg":"",State.DnsInGroup?"dns":""}.Where(x=>x!="").ToArray();int active=ids.Count(Active);assignment.Content=CompactUi.Disclosure(L.T("Main button:")+" "+Name("group")+" ("+ids.Length+")",assignment.ContextMenu?.IsOpen==true);if(State.DnsInGroup&&!shell.Busy){shell.Primary.Content=L.T(active==ids.Length?"Turn off selected":active>0?"Start remaining":"Start selected");shell.Primary.IsEnabled=!shell.DnsBusy;shell.Find<TextBlock>("Summary").Text=L.T(active==ids.Length?"Processes are running":active>0?"Some tools are running":"Ready to start");}if(State.Visible.Length==0){shell.Find<TextBlock>("Summary").Text=L.T("Add modules");shell.Primary.Content=L.T("Modules");shell.Primary.IsEnabled=true;}}
    }
    internal async Task Execute(){if(Editing)return;if(shell.Busy||shell.DnsBusy)return;if(State.Visible.Length==0){OpenManager();return;}string target=State.Primary;if(target!="group"&&!State.Visible.Contains(target)){AssignmentMenu();return;}if(target is "zapret" or "tg"&&(!shell.HealthStates.TryGetValue(target,out var health)||!health.Installed)){shell.OpenModuleSettings(target);return;}if(target=="dns"){await shell.DnsAction();return;}string[] ids=target=="group"?new[]{shell.Settings.ZapretSelected?"zapret":"",shell.Settings.TgSelected?"tg":""}.Where(x=>x!="").ToArray():new[]{target};bool dns=target=="group"&&State.DnsInGroup;bool stop=(ids.Length==0||ids.All(Active))&&(!dns||shell.DnsApplied);
        var hiddenTargets=ids.Where(id=>!State.Visible.Contains(id)).Concat(dns&&!State.Visible.Contains("dns")?new[]{"dns"}:Array.Empty<string>()).ToArray();if(hiddenTargets.Length>0&&AppDialog.Show(shell.Window,L.T("This group includes hidden tools:")+" "+string.Join(", ",hiddenTargets.Select(Name)),"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        if(dns&&!await shell.DnsAction(stop))return;if(ids.Length>0){if(stop)await shell.StopSelected(false,ids);else await shell.StartSelected(ids);}Render();}
    ContextMenu Menu(Button target){var m=new ContextMenu{PlacementTarget=target,Style=(Style)shell.Window.FindResource("ZapretMenu")};target.ContextMenu=m;if(target==assignment){m.Opened+=(_,_)=>Render();m.Closed+=(_,_)=>Render();}return m;}
    void Entry(ContextMenu menu,string label,Action action){var item=new MenuItem{Header=L.T(label),Style=(Style)shell.Window.FindResource("ZapretMenuItem"),Padding=new Thickness(12,7,12,7)};item.Click+=(_,_)=>action();menu.Items.Add(item);}
    void AssignmentMenu(){var m=Menu(assignment);foreach(string id in new[]{"group","zapret","tg","dns"}){string target=id;Entry(m,Name(id),()=>{State.Primary=target;Save();Render();});((MenuItem)m.Items[m.Items.Count-1]).IsChecked=State.Primary==target;}Entry(m,"Height by visible modules",()=>{State.ManualHeight=false;Save();Resize();});m.IsOpen=true;}
    void ProfileMenu(string id,Button button){var m=Menu(button);if(id=="dns"){foreach(var profile in NetworkHub.Dns.DnsProfiles.All.Where(p=>p.Id!="custom")){var choice=profile;Entry(m,choice.Name,()=>{shell.Settings.DnsProfileId=choice.Id;shell.Settings.DnsProfileName=choice.Name;Save();shell.SelectDnsProfile(choice.Id);Render();});((MenuItem)m.Items[m.Items.Count-1]).IsChecked=shell.Settings.DnsProfileId==choice.Id;}Entry(m,"DNS Manager",()=>shell.OpenDnsSettings());}else{
        try{string root=files.Resolve(shell.Settings.ZapretDirectory);if(Directory.Exists(root))foreach(string path in Directory.EnumerateFiles(root,"general*.bat").OrderBy(path=>NaturalKey(Path.GetFileName(path)),StringComparer.OrdinalIgnoreCase)){string value=Path.GetFileName(path);Entry(m,Path.GetFileNameWithoutExtension(path),()=>{if(Active("zapret")){pendingStrategy=value;shell.Settings.PendingStrategy=value;Save();}else{shell.Settings.Strategy=value;Save();}Render();});((MenuItem)m.Items[m.Items.Count-1]).IsChecked=(pendingStrategy??shell.Settings.Strategy)==value;}}catch(Exception ex){shell.ShowMessage(L.Error(ex));}
        if(pendingStrategy!=null)Entry(m,"Restart with selected profile",async()=>{string next=pendingStrategy!;await shell.StopSelected(false,new[]{"zapret"});if(Active("zapret"))return;shell.Settings.Strategy=next;pendingStrategy=null;shell.Settings.PendingStrategy="";Save();await shell.StartSelected(new[]{"zapret"});});Entry(m,"Settings",()=>shell.OpenModuleSettings("zapret"));}m.IsOpen=true;}
#if OPENNET_TESTING
    internal Func<string,bool>? ConfirmationForTests {get;set;}
#endif
    bool Confirm(Window owner,string message)
    {
#if OPENNET_TESTING
        if(shell.Runtime.TestMode&&ConfirmationForTests!=null)return ConfirmationForTests(message);
#endif
        return AppDialog.Show(owner,message,"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
    }
    internal void OpenManager()
    {
        if(manager!=null){manager.Show();manager.WindowState=WindowState.Normal;manager.Activate();return;}
        manager=SettingsLayout.Create(shell,L.T("Modules"),440);var window=manager;var body=SettingsLayout.Panel(window);body.Margin=new Thickness(24,20,24,12);SettingsLayout.Heading(body,L.T("Modules"),L.T("Visibility does not change running tools or startup."));
        var header=new Grid{Margin=new Thickness(4,12,4,4)};header.ColumnDefinitions.Add(new(){Width=new GridLength(56)});header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=new GridLength(88)});header.ColumnDefinitions.Add(new(){Width=new GridLength(88)});int column=1;foreach(string caption in new[]{"Module","On main","At logon"}){var text=new TextBlock{Text=L.T(caption),FontSize=11};ThemeManager.Bind(text,TextBlock.ForegroundProperty,"Muted");Grid.SetColumn(text,column++);header.Children.Add(text);}body.Children.Add(header);
        var error=SettingsLayout.Error(window,body,"ModulesError");bool updating=false;
        bool Persist(Action<UserSettings> change){try{var next=shell.Settings.Copy();change(next);shell.ApplySettings(next);error.Text="";return true;}catch(Exception ex){error.Text=L.Error(ex);files.Log("Modules settings: "+ex);return false;}}
        void Configure(string id,Grid grid){grid.Height=54;var border=(Border)grid.Parent;border.BorderThickness=new Thickness(0);grid.ColumnDefinitions.Add(new(){Width=new GridLength(88)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(88)});var name=grid.Children.OfType<TextBlock>().Single();grid.Children.Remove(name);var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(name);string installation=id=="dns"?L.T("Built in"):shell.HealthStates.TryGetValue(id,out var ready)&&ready.Installed?L.T("Installed")+" · "+ready.Version:L.T("Not installed");var status=new TextBlock{Text=installation,FontSize=11};ThemeManager.Bind(status,TextBlock.ForegroundProperty,"Muted");labels.Children.Add(status);Grid.SetColumn(labels,2);grid.Children.Add(labels);
            var visible=new CheckBox{Style=(Style)shell.Window.FindResource("OptionToggle"),IsChecked=State.Visible.Contains(id),VerticalAlignment=VerticalAlignment.Center,ToolTip=L.T("Show on main")};Grid.SetColumn(visible,3);grid.Children.Add(visible);window.RegisterName("Visible"+id,visible);
            void VisibilityChanged(){if(updating)return;bool value=visible.IsChecked==true;if(!value&&Active(id)&&!Confirm(window,L.T("The hidden tool will continue working. Group membership and startup will remain unchanged."))){updating=true;visible.IsChecked=true;updating=false;return;}if(!Persist(next=>next.Workspace.Visible=value?next.Workspace.Visible.Append(id).Distinct().ToArray():next.Workspace.Visible.Where(x=>x!=id).ToArray())){updating=true;visible.IsChecked=State.Visible.Contains(id);updating=false;}}visible.Checked+=(_,_)=>VisibilityChanged();visible.Unchecked+=(_,_)=>VisibilityChanged();
            if(id!="dns"){var startup=new CheckBox{Style=(Style)shell.Window.FindResource("OptionToggle"),IsChecked=id=="zapret"?shell.Settings.StartupZapret:shell.Settings.StartupTg,VerticalAlignment=VerticalAlignment.Center,ToolTip=L.T("Launch at Windows logon")};Grid.SetColumn(startup,4);grid.Children.Add(startup);window.RegisterName("Startup"+id,startup);void StartupChanged(){if(updating)return;if(!Persist(next=>{if(id=="zapret")next.StartupZapret=startup.IsChecked==true;else next.StartupTg=startup.IsChecked==true;})){updating=true;startup.IsChecked=id=="zapret"?shell.Settings.StartupZapret:shell.Settings.StartupTg;updating=false;}}startup.Checked+=(_,_)=>StartupChanged();startup.Unchecked+=(_,_)=>StartupChanged();}
        }
        OrderEditor=new ModuleOrderEditor(shell.Window,State.Order,configure:Configure);var editor=OrderEditor;body.Children.Insert(body.Children.IndexOf(error),editor);editor.Committed+=()=>{if(!Persist(next=>next.Workspace.Order=editor.Order))editor.SetOrder(State.Order);};
        var hint=new TextBlock{Text=L.T("Drag the six-dot handle. Alt+Up/Down also moves a module."),FontSize=11,Margin=new Thickness(4,12,0,0),TextWrapping=TextWrapping.Wrap};ThemeManager.Bind(hint,TextBlock.ForegroundProperty,"Muted");body.Children.Add(hint);
        var done=SettingsLayout.Finish(window,"ModulesDone","Done");done.Click+=(_,_)=>window.Close();var reset=SettingsLayout.Button(window,"ModulesDefaults","Restore defaults…");reset.Style=(Style)window.FindResource("QuietLink");reset.ClearValue(Control.BackgroundProperty);reset.Padding=new Thickness(0,0,8,0);reset.HorizontalAlignment=HorizontalAlignment.Left;ThemeManager.Bind(reset,Control.ForegroundProperty,"Muted");
        reset.Click+=(_,_)=>{if(shell.Busy||shell.DnsBusy)return;string message=L.T("Restore default module order and visibility, and select Zapret and TG WS Proxy for Windows startup? Global Windows startup registration, the main button, running tools, DNS profiles, files and recovery backups will remain unchanged.");if(!Confirm(window,message))return;try{var next=WorkspaceDefaults.Plan(shell.Settings);shell.ApplySettings(next);updating=true;editor.SetOrder(State.Order);foreach(string id in WorkspaceSettings.Modules){((CheckBox)window.FindName("Visible"+id)).IsChecked=true;if(id!="dns")((CheckBox)window.FindName("Startup"+id)).IsChecked=true;}error.Text="";}catch(Exception ex){error.Text=L.Error(ex);files.Log("Workspace defaults: "+ex);}finally{updating=false;}};
        SettingsLayout.PinActions(window,SettingsLayout.Footer(window,done,reset));void Enabled(){editor.IsEnabled=reset.IsEnabled=!shell.Busy&&!shell.DnsBusy;}shell.StateChanged+=Enabled;window.Closed+=(_,_)=>{shell.StateChanged-=Enabled;OrderEditor=null;manager=null;};Enabled();ThemeManager.BindTree(window);window.Show();
    }
}
