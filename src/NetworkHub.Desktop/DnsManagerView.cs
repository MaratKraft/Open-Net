using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
using NetworkHub.Core;
using NetworkHub.Dns;

namespace NetworkHub.Desktop;

internal sealed class DnsManagerView
{
    readonly Shell shell;readonly AppFiles files;readonly IDnsSettingsReader reader;readonly IDnsJournalStore store;readonly IDnsProbeRunner probes;
    readonly Window window;readonly StackPanel panel;readonly ComboBox adapters,profiles,endpoint,probeMode;readonly CheckBox ipv4,ipv6;readonly TextBox addresses4,addresses6;
    readonly TextBlock current,preview,warning,result,reason,profileAddresses;readonly StackPanel customFields;readonly ComboBox ipv6Mode;readonly TextBox profileName,alternate4,dohEndpoint;readonly CheckBox useDoh,fallback,clearCache;readonly Button automatic,flush,saveProfile,reapply,more;readonly MenuItem reapplyItem,automaticItem,flushItem;readonly TextBlock operationEvidence,probeEvidence;DnsOperationEvidence? lastEvidence;readonly DnsProfileStore profileStore;readonly Button apply,restore,refresh,probe;readonly CancellationTokenSource cancellation=new();
    Button? resolve;TextBlock? simpleStatus;bool refreshing,configuringProfile,useAdvancedPolicy;Window? advancedWindow;StackPanel advanced=new();TextBlock? basicNote;string operationError="";bool restoringOperation;string customDraftBaseline="";
    DnsSnapshot? snapshot,operationBefore,operationTarget;DnsJournal? journal;bool busy;
    internal Window Window=>window;
    internal bool IsBusy=>busy;
    internal bool Applied=>DnsPresentation.Managed(snapshot,journal);
    internal string LineStatus=>(shell.Runtime.TestMode?L.T("Preview mode")+" · ":"")+(busy?L.T(restoringOperation?"Restoring…":"Applying…"):operationError.Length>0?L.T("Error"):DnsPresentation.LineStatus(snapshot,journal));
    internal string StatusTone=>operationError.Length>0?"Error":busy?"Muted":DnsPresentation.StatusTone(snapshot,journal);
    internal string CurrentProfile=>DnsPresentation.CurrentProfile(snapshot,journal);
    internal void RefreshStatus(){if(busy||refreshing)return;refreshing=true;try{Read();}finally{refreshing=false;}}
    internal void SelectProfile(string id){foreach(ComboBoxItem item in profiles.Items)if(item.Tag is DnsProfile profile&&profile.Id==id||item.Tag is SavedDnsProfile saved&&"saved:"+saved.Id==id){profiles.SelectedItem=item;break;}}
    internal async Task<bool> Power(bool? restore=null){RefreshStatus();bool off=restore??OffAction;if(off&&!Applied)return journal?.Active!=true;await Operation(off,repeat:!off&&apply.IsEnabled==false&&reapply.IsEnabled);RefreshStatus();if(off)return journal?.Phase==DnsPhase.Restored&&snapshot!=null&&journal.Before.SameConfiguration(snapshot);try{return Applied&&Plan().Target.SameConfiguration(snapshot!);}catch(DnsException){return false;}}
#if OPENNET_TESTING
    internal Func<string,bool>? ConfirmationForTests {get;set;}
    internal static IDnsSettingsReader? ReaderForTests {get;set;}
    internal Task OperationForTests(bool restoring,bool automaticMode=false,bool cacheOnly=false,bool repeat=false){if(!shell.Runtime.TestMode)throw new InvalidOperationException();return Operation(restoring,automaticMode,cacheOnly,repeat);}
    internal Task ProbeForTests(){if(!shell.Runtime.TestMode)throw new InvalidOperationException();return Probe();}
#endif
    internal string Status=>(shell.Runtime.TestMode?L.T("Preview mode")+" · ":"")+(busy?L.T(restoringOperation?"Restoring…":"Applying…"):operationError.Length>0?L.T("Error"):DnsPresentation.Status(snapshot,journal));
    bool OffAction {get{if(!Applied)return false;try{return Plan().Target.SameConfiguration(snapshot!);}catch(DnsException){return true;}}}
    internal static string ProfileStatus(DnsJournal record)=>L.T("Running")+" · "+DnsPresentation.ProfileName(record);
    // Delay protected-store access until inspection, so errors are visible inside the window.
    sealed class ReadOnlyRecovery : IDnsJournalStore {
        public DnsJournal? Load(Guid id)=>DnsRecoveryStore.Open(DnsRecoveryStore.Owner(),false).Load(id);
        public void Save(DnsJournal record)=>throw new InvalidOperationException("Only the elevated helper writes protected recovery records.");
    }
    internal DnsManagerView(Shell shell,AppFiles files)
    {
        this.shell=shell;this.files=files;profileStore=new DnsProfileStore(files.Data);
#if OPENNET_TESTING
        if(shell.Runtime.TestMode){reader=ReaderForTests??TestsDns.Fixture(files.Data);store=new DnsFileJournalStore(Paths.Owned(files.Data,"Dns/IsolatedRecovery"),"safe-test");probes=new TestsDns.FakeProbes();}
        else
#endif
        {reader=new WindowsDnsSettings();store=new ReadOnlyRecovery();probes=new WindowsDnsProbeRunner();}
        window=SettingsLayout.Create(shell,L.T("DNS Manager — Open Net"),680);window.MinHeight=460;panel=SettingsLayout.Panel(window);
        SettingsLayout.Heading(panel,"DNS Manager",L.T(shell.Runtime.TestMode?"Preview mode · Windows settings are unchanged.":"Configure DNS for one network adapter. Network tools are controlled separately."));
        Label("Network adapter");adapters=Combo("DnsAdapter");adapters.SelectionChanged+=(_,_)=>Read();
        var currentHeader=new Grid{Name="DnsCurrentHeader",Margin=new Thickness(0,12,0,0)};currentHeader.Children.Add(new TextBlock{Text=L.T("Current DNS"),VerticalAlignment=VerticalAlignment.Center});
        refresh=SettingsLayout.IconButton(window,"DnsRefresh","Check settings","RefreshGeometry");refresh.HorizontalAlignment=HorizontalAlignment.Right;
        var arrow=new System.Windows.Shapes.Path{Data=(System.Windows.Media.Geometry)shell.Window.Resources["RefreshGeometry"],Style=(Style)shell.Window.Resources["IconArrow"]};refresh.Content=new Grid{Width=18,Height=18,Children={arrow}};currentHeader.Children.Add(refresh);panel.Children.Add(currentHeader);refresh.Click+=(_,_)=>Read();
        current=AddText("Current settings","SecondaryText");current.Name="DnsCurrent";window.RegisterName("DnsCurrent",current);current.Margin=new Thickness(0,4,0,8);
        Label("DNS profile");profiles=Combo("DnsProfile");profiles.Items.Add(new ComboBoxItem{Content=L.T("Choose a profile"),Tag=null});foreach(var item in DnsProfiles.All)profiles.Items.Add(new ComboBoxItem{Content=L.T(item.Name),Tag=item});profiles.SelectedIndex=0;
        profiles.SelectionChanged+=(_,_)=>ProfileChanged();
        profileAddresses=AddText("","SecondaryText");profileAddresses.Name="DnsProfileAddresses";
        customFields=new StackPanel{Visibility=Visibility.Collapsed};
        addresses4=Input("DnsAddresses4","Preferred IPv4 DNS");var label4=panel.Children[panel.Children.Count-2];panel.Children.Remove(label4);panel.Children.Remove(addresses4);customFields.Children.Add(label4);customFields.Children.Add(addresses4);
        addresses6=Input("DnsAddresses6","IPv6 DNS addresses, in order");var label6=panel.Children[panel.Children.Count-2];panel.Children.Remove(label6);panel.Children.Remove(addresses6);customFields.Children.Add(label6);customFields.Children.Add(addresses6);panel.Children.Add(customFields);
        ipv4=SettingsLayout.Toggle(window,panel,shell,"DnsIPv4","Change IPv4 DNS",true);
        // The retained checkbox is the internal family flag; the visible choice states preservation explicitly.
        ipv6=new CheckBox{IsChecked=false};window.RegisterName("DnsIPv6",ipv6);
        Label("IPv6 DNS");ipv6Mode=Combo("DnsIPv6Mode");ipv6Mode.Items.Add(new ComboBoxItem{Content=L.T("Keep current settings")});ipv6Mode.Items.Add(new ComboBoxItem{Content=L.T("Use profile addresses")});ipv6Mode.SelectedIndex=0;ipv6Mode.SelectionChanged+=(_,_)=>ipv6.IsChecked=ipv6Mode.SelectedIndex==1;
        addresses4.TextChanged+=(_,_)=>UpdatePlan();addresses6.TextChanged+=(_,_)=>UpdatePlan();ipv4.Checked+=(_,_)=>UpdatePlan();ipv4.Unchecked+=(_,_)=>UpdatePlan();ipv6.Checked+=(_,_)=>UpdatePlan();ipv6.Unchecked+=(_,_)=>UpdatePlan();
        useDoh=SettingsLayout.Toggle(window,panel,shell,"DnsUseDoh","Use DNS over HTTPS",false);fallback=SettingsLayout.Toggle(window,panel,shell,"DnsFallback","Allow plaintext fallback",false);clearCache=SettingsLayout.Toggle(window,panel,shell,"DnsClearCache","Clear system DNS cache after a verified change",false);
        profileName=Input("DnsCustomName","Profile name");panel.Children.Remove(profileName);var nameLabel=panel.Children[panel.Children.Count-1];panel.Children.Remove(nameLabel);customFields.Children.Insert(0,nameLabel);customFields.Children.Insert(1,profileName);
        alternate4=Input("DnsAlternate4","Alternative IPv4 DNS (optional)");panel.Children.Remove(alternate4);var alternateLabel=panel.Children[panel.Children.Count-1];panel.Children.Remove(alternateLabel);customFields.Children.Insert(4,alternateLabel);customFields.Children.Insert(5,alternate4);
        dohEndpoint=Input("DnsDohEndpoint","HTTPS DoH endpoint (optional)");panel.Children.Remove(dohEndpoint);var dohLabel=panel.Children[panel.Children.Count-1];panel.Children.Remove(dohLabel);customFields.Children.Add(dohLabel);customFields.Children.Add(dohEndpoint);
        saveProfile=SettingsLayout.Button(window,"DnsSaveProfile","Save custom profile");saveProfile.Margin=new Thickness(0,8,0,0);customFields.Children.Add(saveProfile);saveProfile.Click+=(_,_)=>SaveProfile();
        profileName.TextChanged+=(_,_)=>UpdatePlan();alternate4.TextChanged+=(_,_)=>UpdatePlan();dohEndpoint.TextChanged+=(_,_)=>UpdatePlan();useDoh.Checked+=(_,_)=>{if(!configuringProfile)useAdvancedPolicy=true;UpdatePlan();};useDoh.Unchecked+=(_,_)=>{if(!configuringProfile)useAdvancedPolicy=true;UpdatePlan();};fallback.Checked+=(_,_)=>{if(!configuringProfile)useAdvancedPolicy=true;UpdatePlan();};fallback.Unchecked+=(_,_)=>{if(!configuringProfile)useAdvancedPolicy=true;UpdatePlan();};
        var details=Disclosure("Profile details");preview=AddText("IPv6 settings are preserved unless explicitly selected.","Muted");preview.Name="DnsPreview";panel.Children.Remove(preview);details.Children.Add(preview);warning=AddText("DoH changes are not supported in this alpha.","Warning");warning.Name="DnsWarning";panel.Children.Remove(warning);details.Children.Add(warning);
        var diagnostics=Disclosure("Connection check");endpoint=Combo("DnsEndpoint");panel.Children.Remove(endpoint);diagnostics.Children.Add(endpoint);foreach(var uri in WindowsDnsProbeRunner.Endpoints)endpoint.Items.Add(new ComboBoxItem{Content=uri.Host,Tag=uri});endpoint.SelectedIndex=0;
        probeMode=Combo("DnsProbeMode");panel.Children.Remove(probeMode);diagnostics.Children.Add(probeMode);foreach(string caption in new[]{"System DNS and HTTPS","System DNS only","HTTPS only"})probeMode.Items.Add(new ComboBoxItem{Content=L.T(caption)});probeMode.SelectedIndex=0;
        probe=SettingsLayout.Button(window,"DnsProbe","Check connection…");probe.Margin=new Thickness(0,8,0,0);diagnostics.Children.Add(probe);probe.Click+=async(_,_)=>await Probe();
        var disclaimer=AddText("DNS and HTTPS results do not prove that ChatGPT or Gemini works.","Muted");panel.Children.Remove(disclaimer);diagnostics.Children.Add(disclaimer);
        var lookupNote=AddText("HTTPS can resolve names through the system or proxy even when the separate DNS test is off.","Muted");panel.Children.Remove(lookupNote);diagnostics.Children.Add(lookupNote);
        operationEvidence=AddText("","SecondaryText");operationEvidence.Name="DnsOperationEvidence";window.RegisterName("DnsOperationEvidence",operationEvidence);panel.Children.Remove(operationEvidence);diagnostics.Children.Add(operationEvidence);
        probeEvidence=AddText("HTTPS: not tested","SecondaryText");probeEvidence.Name="DnsProbeEvidence";window.RegisterName("DnsProbeEvidence",probeEvidence);panel.Children.Remove(probeEvidence);diagnostics.Children.Add(probeEvidence);
        result=AddText("","SecondaryText");result.Name="DnsResult";window.RegisterName("DnsResult",result);
        reason=new TextBlock{Name="DnsReason",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};ThemeManager.Bind(reason,TextBlock.ForegroundProperty,"Muted");window.RegisterName("DnsReason",reason);
        var actions=SettingsLayout.ActionGrid(window,panel,"DnsActions");apply=SettingsLayout.Button(window,"DnsApply","Apply…");restore=SettingsLayout.Button(window,"DnsRestore","Restore…");SettingsLayout.AddAction(actions,apply,0);SettingsLayout.AddAction(actions,restore,1);ThemeManager.Bind(apply,Control.BackgroundProperty,"PrimaryBackground");ThemeManager.Bind(apply,Control.ForegroundProperty,"PrimaryText");
        apply.Click+=async(_,_)=>await Operation(false);restore.Click+=async(_,_)=>await Operation(true);
        panel.Children.Remove(actions);var scroll=(ScrollViewer)window.Content;window.RegisterName("DnsScroll",scroll);window.Content=null;
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.Children.Add(scroll);
        var footerContent=new StackPanel();footerContent.Children.Add(reason);
        var extra=new Grid{Margin=new Thickness(0,0,0,8)};extra.ColumnDefinitions.Add(new ColumnDefinition());extra.ColumnDefinitions.Add(new ColumnDefinition());
        automatic=SettingsLayout.Button(window,"DnsAutomatic","Automatic DNS…");flush=SettingsLayout.Button(window,"DnsFlush","Clear DNS cache…");automatic.Height=flush.Height=28;automatic.FontSize=flush.FontSize=11;flush.Margin=new Thickness(8,0,0,0);Grid.SetColumn(flush,1);reapply=SettingsLayout.Button(window,"DnsReapply","Apply again…");reapply.Height=28;reapply.FontSize=11;
        more=SettingsLayout.Button(window,"DnsMore","Additional actions");more.Height=28;more.FontSize=11;more.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumnSpan(more,2);extra.Children.Add(more);footerContent.Children.Add(extra);
        var menu=new ContextMenu{Style=(Style)shell.Window.Resources["ZapretMenu"],PlacementTarget=more};ThemeManager.Bind(menu,ContextMenu.ForegroundProperty,"Text");
        MenuItem Entry(string name,string label,Button target){var item=new MenuItem{Header=L.T(label),Style=(Style)shell.Window.Resources["ZapretMenuItem"],Padding=new Thickness(12,7,12,7)};ThemeManager.Bind(item,MenuItem.ForegroundProperty,"Text");menu.Items.Add(item);window.RegisterName(name,item);item.Click+=(_,_)=>target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return item;}
        reapplyItem=Entry("DnsReapplyMenu","Apply again…",reapply);var restoreEntry=Entry("DnsRestoreMenu","Restore previous settings…",restore);automaticItem=Entry("DnsAutomaticMenu","Automatic DNS…",automatic);flushItem=Entry("DnsFlushMenu","Clear DNS cache…",flush);more.ContextMenu=menu;more.Click+=(_,_)=>menu.IsOpen=true;
        void MenuLanguage(){restoreEntry.Header=L.T("Restore previous settings…");reapplyItem.Header=L.T("Apply again…");automaticItem.Header=L.T("Automatic DNS…");flushItem.Header=L.T("Clear DNS cache…");}L.Changed+=MenuLanguage;window.Closed+=(_,_)=>L.Changed-=MenuLanguage;
        reapply.Click+=async(_,_)=>await Operation(false,false,false,true);automatic.Click+=async(_,_)=>await Operation(false,true);flush.Click+=async(_,_)=>await Operation(false,false,true);
        footerContent.Children.Add(actions);actions.Margin=new Thickness(0);
        var footer=new Border{Padding=new Thickness(26,10,26,18),BorderThickness=new Thickness(0,1,0,0),Child=footerContent};ThemeManager.Bind(footer,Border.BorderBrushProperty,"Line");ThemeManager.Bind(footer,Border.BackgroundProperty,"Surface");Grid.SetRow(footer,1);layout.Children.Add(footer);window.Content=layout;
        try{foreach(var item in reader.Adapters())adapters.Items.Add(new ComboBoxItem{Content=item.Name+" · "+L.T(item.Kind)+(item.Virtual?" · "+L.T("read-only"):""),Tag=item});}
        catch(Exception ex){files.Log("DNS adapter inspection: "+ex);current.Text=L.T("DNS settings could not be read safely.")+" "+Error(ex);}
        window.Closing+=(_,e)=>{if(busy){e.Cancel=true;return;}if(window.IsLoaded&&Profile?.Id=="custom"&&customDraftBaseline.Length>0&&CustomDraft()!=customDraftBaseline){string message=L.T("Discard unsaved custom DNS profile values? Save the profile in additional parameters to keep them. Closing does not change Windows DNS.");bool discard;
#if OPENNET_TESTING
        if(shell.Runtime.TestMode&&ConfirmationForTests!=null)discard=ConfirmationForTests(message);else
#endif
        discard=AppDialog.Show(window,message,"Open Net",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;e.Cancel=!discard;}};window.Closed+=(_,_)=>{cancellation.Cancel();cancellation.Dispose();shell.UpdateDnsStatus();};
        var preferred=adapters.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>x.Tag is DnsAdapter d&&d.Connected&&!d.Virtual);var remembered=adapters.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>x.Tag is DnsAdapter d&&d.Id.ToString("D")==shell.Settings.DnsAdapterId);if(remembered!=null)adapters.SelectedItem=remembered;else if(preferred!=null)adapters.SelectedItem=preferred;else if(adapters.Items.Count>0)adapters.SelectedIndex=0;
        if(adapters.Items.Count==0){warning.Text=L.T("No supported network adapters were found.");SetEnabled();}
        try{foreach(var saved in profileStore.Load())profiles.Items.Add(new ComboBoxItem{Content=saved.Name,Tag=saved});}catch(Exception ex){result.Text=L.T("Custom profiles could not be loaded. The original file was preserved.")+" "+Error(ex);}
#if OPENNET_TESTING
        if(shell.Runtime.TestMode){var failures=new ComboBox{Height=32,Margin=new Thickness(0,8,0,0)};window.RegisterName("DnsSimulatedFailure",failures);foreach(string value in new[]{"No injected failure","UAC denied","Apply failure","Restore failure","Verification failure","Cache failure"})failures.Items.Add(new ComboBoxItem{Content=L.T(value)});failures.SelectedIndex=0;diagnostics.Children.Add(new TextBlock{Text=L.T("Simulate an operation failure")});diagnostics.Children.Add(failures);}
#endif
        SelectProfile(shell.Settings.DnsProfileId);Compact();
        ThemeManager.BindTree(window);LocalizedTree.Bind(window);
        void LanguageChanged(){Read();UpdatePlan();}L.Changed+=LanguageChanged;window.Closed+=(_,_)=>L.Changed-=LanguageChanged;
    }
    void Compact()
    {
        window.Height=450;window.MinHeight=360;var main=new StackPanel{Margin=new Thickness(24)};
        var keep=new System.Collections.Generic.HashSet<UIElement>{panel.Children[0],panel.Children[1],adapters,profiles,result};
        foreach(var box in new[]{adapters,profiles}){int index=panel.Children.IndexOf(box);if(index>0)keep.Add(panel.Children[index-1]);}
        foreach(UIElement item in panel.Children.Cast<UIElement>().ToArray()){panel.Children.Remove(item);if(keep.Contains(item))main.Children.Add(item);else advanced.Children.Add(item);}
        simpleStatus=new TextBlock{FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};ThemeManager.Bind(simpleStatus,TextBlock.ForegroundProperty,"Text");main.Children.Add(simpleStatus);
        basicNote=new TextBlock{FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};ThemeManager.Bind(basicNote,TextBlock.ForegroundProperty,"Muted");main.Children.Add(basicNote);
        resolve=SettingsLayout.Button(window,"DnsResolve","Review current settings…");resolve.Margin=new Thickness(0,10,0,0);resolve.Click+=async(_,_)=>await AcceptCurrent();main.Children.Add(resolve);
        var extras=new StackPanel{Margin=new Thickness(0,16,0,0)};window.RegisterName("DnsAdditionalContent",extras);var details=SettingsLayout.Button(window,"DnsAdvanced","DNS parameters and diagnostics…");details.Background=System.Windows.Media.Brushes.Transparent;details.HorizontalAlignment=HorizontalAlignment.Left;details.Padding=new Thickness(0);details.Margin=new Thickness(0,14,0,0);details.Click+=(_,_)=>OpenAdvanced();extras.Children.Add(details);main.Children.Add(extras);
        var layout=new Grid();layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=GridLength.Auto});var compactScroll=new ScrollViewer{Content=main,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};window.UnregisterName("DnsScroll");window.RegisterName("DnsScroll",compactScroll);layout.Children.Add(compactScroll);
        // Controls retain their names/handlers but the old competing Apply/Restore footer is not presented.
        var oldLayout=(Grid)window.Content;var oldFooter=(Border)oldLayout.Children[1];var oldContent=(StackPanel)oldFooter.Child;oldFooter.Child=null;oldContent.Children.Remove(reason);advanced.Children.Add(reason);
        more.Content=CompactUi.Disclosure(L.T("Other actions"));more.Height=32;more.MinWidth=150;more.Padding=new Thickness(12,4,12,4);more.Margin=new Thickness(0,12,0,8);more.ContextMenu!.Opened+=(_,_)=>more.Content=CompactUi.Disclosure(L.T("Other actions"),true);more.ContextMenu.Closed+=(_,_)=>more.Content=CompactUi.Disclosure(L.T("Other actions"));((Panel)more.Parent).Children.Remove(more);advanced.Children.Add(more);main.Children.Remove(result);advanced.Children.Add(result);
        var done=SettingsLayout.Finish(window,"DnsDone","Done");done.Click+=(_,_)=>{try{files.Save(shell.Settings);window.Close();}catch(Exception ex){basicNote.Text=Error(ex);files.Log("DNS preference save: "+ex);}};var disclosure=SettingsLayout.Additional(window,"DnsAdditional",extras);var footer=new Border{Padding=SettingsLayout.FooterPadding,Child=SettingsLayout.Footer(window,done,disclosure)};ThemeManager.Bind(footer,Border.BackgroundProperty,"Surface");Grid.SetRow(footer,1);layout.Children.Add(footer);window.Content=layout;SetEnabled();
    }
    internal Window OpenAdvanced()
    {
        if(advancedWindow!=null){advancedWindow.Show();advancedWindow.WindowState=WindowState.Normal;advancedWindow.Activate();return advancedWindow;}
        var child=SettingsLayout.Create(shell,L.T("DNS advanced settings"),620);child.Owner=window;advancedWindow=child;var content=SettingsLayout.Panel(child);SettingsLayout.Heading(content,L.T("DNS advanced settings"),L.T("Optional parameters and diagnostics. Windows is changed only after confirmation."));
        // Navigation keeps technical flags out of the everyday profile/power scenario.
        var tabs=new TabControl();var parameters=new StackPanel();var diagnostics=new StackPanel();foreach(UIElement item in advanced.Children.Cast<UIElement>().ToArray()){advanced.Children.Remove(item);if(item==current||item==operationEvidence||item==probeEvidence||item==reason||item is Grid g&&g.Name=="DnsCurrentHeader"||item is FrameworkElement fe&&fe.Tag is string caption&&caption is "Profile details" or "Connection check")diagnostics.Children.Add(item);else parameters.Children.Add(item);}
        tabs.Items.Add(new TabItem{Header=L.T("Parameters"),Content=parameters});tabs.Items.Add(new TabItem{Header=L.T("Diagnostics"),Content=diagnostics});content.Children.Add(tabs);if(apply.Parent is Panel oldActions)oldActions.Children.Remove(apply);apply.Content=L.T("Apply these parameters…");apply.Margin=new Thickness(0,12,0,0);parameters.Children.Add(apply);
        var applyChoice=SettingsLayout.Finish(child,"DnsAdvancedDone","Done");applyChoice.MinWidth=136;applyChoice.HorizontalAlignment=HorizontalAlignment.Right;applyChoice.Click+=(_,_)=>child.Close();SettingsLayout.PinActions(child,SettingsLayout.Footer(child,applyChoice));
        child.Closed+=(_,_)=>{foreach(var p in new[]{parameters,diagnostics})foreach(UIElement item in p.Children.Cast<UIElement>().ToArray()){p.Children.Remove(item);if(item!=apply)advanced.Children.Add(item);}advancedWindow=null;};ThemeManager.BindTree(child);child.Show();return child;
    }
    async Task AcceptCurrent()
    {
        if(busy||snapshot?.CanChange!=true)return;Read();if(snapshot?.CanChange!=true)return;var approved=snapshot;
        string message=L.T("Accept the current adapter settings as a new baseline? The previous recovery record will be archived. No DNS settings will be written.")+"\n\n"+approved.AdapterName+"\nIPv4: "+FamilyText(approved.IPv4)+"\nIPv6: "+FamilyText(approved.IPv6);
        bool confirmed;
#if OPENNET_TESTING
        if(shell.Runtime.TestMode&&ConfirmationForTests!=null)confirmed=ConfirmationForTests(message);else
#endif
        confirmed=AppDialog.Show(advancedWindow?.IsVisible==true?advancedWindow:window,message,L.T("DNS recovery review"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;if(!confirmed)return;
        try{busy=true;SetEnabled();
#if OPENNET_TESTING
            if(shell.Runtime.TestMode)new DnsTransaction(reader,(IDnsSettingsWriter)reader,store,"safe-test").AcceptCurrent(approved);else
#endif
            {using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);var request=new DnsRequest(Guid.NewGuid().ToString("N"),process.Id,identity.StartFileTime,DnsRecoveryStore.Owner(),shell.Settings.Language,"adopt",approved,approved,Array.Empty<DnsFamily>(),approved.Fingerprint()){ClearCache=false};var response=await DnsElevated.Execute(files,request,false);if(!response.Success)throw new DnsException(response.Message);}
            operationError="";result.Text=L.T("Current settings accepted; previous recovery record archived.");
        }catch(Exception ex){result.Text=Error(ex);files.Log("DNS acceptance: "+ex);}finally{busy=false;Read();shell.UpdateDnsStatus();}
    }
    StackPanel Disclosure(string caption)
    {
        var content=new StackPanel{Visibility=Visibility.Collapsed,Tag=caption};var header=new Button{Tag=caption,HorizontalAlignment=HorizontalAlignment.Left,MinHeight=28,Margin=new Thickness(0,8,0,0),Padding=new Thickness(8,4,8,4),Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new Thickness(0)};
        void UpdateCaption()=>header.Content=CompactUi.Disclosure(L.T(caption),content.Visibility==Visibility.Visible);UpdateCaption();
        ThemeManager.Bind(header,Control.ForegroundProperty,"SecondaryText");header.Click+=(_,_)=>{content.Visibility=content.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;UpdateCaption();};L.Changed+=UpdateCaption;window.Closed+=(_,_)=>L.Changed-=UpdateCaption;panel.Children.Add(header);panel.Children.Add(content);return content;
    }
    void Label(string text)=>SettingsLayout.Label(panel,text);
    TextBlock AddText(string text,string color){var block=new TextBlock{Text=L.T(text),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};ThemeManager.Bind(block,TextBlock.ForegroundProperty,color);panel.Children.Add(block);return block;}
    ComboBox Combo(string name){var box=new ComboBox{Height=36,Margin=new Thickness(0,0,0,4)};window.RegisterName(name,box);panel.Children.Add(box);return box;}
    TextBox Input(string name,string label){Label(label);var box=new TextBox{Height=36,Padding=new Thickness(6,2,6,2),VerticalContentAlignment=VerticalAlignment.Center};window.RegisterName(name,box);ThemeManager.Bind(box,Control.BackgroundProperty,"Input");ThemeManager.Bind(box,Control.ForegroundProperty,"Text");ThemeManager.Bind(box,Control.BorderBrushProperty,"InputBorder");panel.Children.Add(box);return box;}
    DnsProfile? Profile=>(profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile saved?new("custom",saved.Name,saved.IPv4,saved.IPv6,saved.Doh,"","",true,""):(profiles.SelectedItem as ComboBoxItem)?.Tag as DnsProfile;
    DnsProfile? SelectedPlanProfile=>Profile?.Id=="custom"?new("custom",profileName.Text.Trim(),Array.Empty<string>(),Array.Empty<string>(),string.IsNullOrWhiteSpace(dohEndpoint.Text)?null:dohEndpoint.Text.Trim(),"","",true,""):Profile;
    SavedDnsProfile CustomPlan(){var value=new SavedDnsProfile((profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile stored?stored.Id:Guid.NewGuid().ToString("N"),profileName.Text.Trim(),DnsProfiles.Validate(addresses4.Text+" "+alternate4.Text,DnsFamily.IPv4),string.IsNullOrWhiteSpace(addresses6.Text)?Array.Empty<string>():DnsProfiles.Validate(addresses6.Text,DnsFamily.IPv6),SelectedPlanProfile?.Doh,useDoh.IsChecked==true,fallback.IsChecked==true);DnsProfileStore.Validate(value);return value;}
    void SaveProfile(){try{var value=CustomPlan();profileStore.Save(value);var existing=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(p=>p.Tag is SavedDnsProfile saved&&saved.Id==value.Id);if(existing!=null)profiles.Items.Remove(existing);var item=new ComboBoxItem{Content=value.Name,Tag=value};profiles.Items.Add(item);profiles.SelectedItem=item;result.Text=L.T("Custom profile saved. Windows settings were not changed.");}catch(Exception ex){result.Text=Error(ex);}}
    bool SessionReady=>journal?.Active!=true||snapshot!=null&&journal.Phase==DnsPhase.Applied&&journal.Expected.SameConfiguration(snapshot);
    Guid Adapter=>(adapters.SelectedItem as ComboBoxItem)?.Tag is DnsAdapter value?value.Id:Guid.Empty;
    void Read()
    {
        if(current==null)return;
        try{snapshot=reader.Read(Adapter);journal=store.Load(Adapter);shell.Settings.DnsAdapterId=Adapter.ToString("D");current.Text=L.T("Scope: selected adapter; not an individual Wi-Fi profile.")+"\n"+"IPv4 · "+FamilyText(snapshot.IPv4)+"\nIPv6 · "+FamilyText(snapshot.IPv6)+"\n"+L.T(snapshot.CanChange?snapshot.IPv4.Encryption.Any(p=>(p.Flags&3)!=0)?"Adapter DoH captured; backup is created before applying.":"No active adapter DoH settings detected.":snapshot.Limitation);if(journal?.Active==true){current.Text+="\n"+L.T(DnsTransaction.Recoverable(journal,snapshot)?journal.Phase==DnsPhase.Applied?"A DNS profile is applied by Open Net.":"An unfinished DNS operation needs recovery review.":"DNS settings were changed outside Open Net.");}}
        catch(Exception ex){snapshot=null;journal=null;files.Log("DNS inspection: "+ex);current.Text=L.T("DNS settings could not be read safely.")+" "+Error(ex);}
        UpdatePlan();
    }
    static string FamilySummary(DnsFamilySettings settings)
    {
        string summary=L.T(settings.Mode==DnsMode.Manual?"Manual":"Automatic")+" · "+string.Join(", ",settings.Mode==DnsMode.Manual?settings.Servers:settings.EffectiveServers);
        // Describe each server separately: a mixed policy must not look like uniform fallback.
        foreach(string server in settings.Servers){var policy=settings.Encryption.FirstOrDefault(p=>p.Server==server);summary+="\n"+server+" · "+(policy==null||(policy.Flags&3)==0?L.T("DoH disabled"):"DoH: "+(policy.Template??L.T("Automatic template"))+" · "+L.T((policy.Flags&4)!=0?"Plaintext fallback permitted":"No plaintext fallback"));}
        return summary;
    }
    static string FamilyText(DnsFamilySettings settings)=>L.T(settings.Mode==DnsMode.Manual?"Manual":settings.Mode==DnsMode.Automatic?"Automatic":"Unknown")+" · "+string.Join(", ",settings.Mode==DnsMode.Manual?settings.Servers:settings.EffectiveServers)+(settings.Encryption.Length==0?"":"\n"+string.Join("\n",settings.Encryption.Select(p=>p.Server+" · "+((p.Flags&3)==0?L.T("DoH disabled"):"DoH · "+(p.Template??L.T("Automatic template"))+" · "+L.T((p.Flags&4)==0?"No plaintext fallback":"Plaintext fallback permitted"))+" · flags="+p.Flags)));
    string CustomDraft()=>string.Join("|",profileName.Text,addresses4.Text,alternate4.Text,addresses6.Text,dohEndpoint.Text,useDoh.IsChecked,fallback.IsChecked);
    void ProfileChanged()
    {
        if(Profile is {} selected){shell.Settings.DnsProfileId=(profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile savedChoice?"saved:"+savedChoice.Id:selected.Id;shell.Settings.DnsProfileName=L.T(selected.Name);try{files.Save(shell.Settings);}catch(Exception ex){files.Log("DNS profile choice: "+ex.Message);}}
        if(addresses4==null)return;configuringProfile=true;var profile=Profile;bool custom=profile?.Id=="custom";
        addresses4.Text=profile==null?"":custom?profile.IPv4.FirstOrDefault()??"":string.Join(", ",profile.IPv4);addresses6.Text=profile==null?"":string.Join(", ",profile.IPv6);alternate4.Text=custom&&profile!.IPv4.Length>1?profile.IPv4[1]:"";profileName.Text=profile?.Name??"Custom DNS";dohEndpoint.Text=profile?.Doh??"";
        addresses4.IsReadOnly=addresses6.IsReadOnly=!custom;ipv6.IsChecked=false;ipv6Mode.SelectedIndex=0;customFields.Visibility=custom?Visibility.Visible:Visibility.Collapsed;
        useDoh.IsChecked=(profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile saved?saved.UseDoh:snapshot?.IPv4.Encryption.Any(p=>(p.Flags&3)!=0)==true;fallback.IsChecked=(profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile configured?configured.Fallback:snapshot?.IPv4.Encryption.Any(p=>(p.Flags&4)!=0)==true;configuringProfile=false;useAdvancedPolicy=(profiles.SelectedItem as ComboBoxItem)?.Tag is SavedDnsProfile;customDraftBaseline=CustomDraft();UpdatePlan();
    }
    (DnsSnapshot Target,DnsFamily[] Families) Plan()
    {
        if(snapshot==null)throw new DnsException("DNS settings could not be read safely.");
        if(Profile==null)throw new DnsException("Choose an adapter and a DNS profile.");
        if(Profile.Id=="custom")CustomPlan();
        var target=snapshot;var families=new System.Collections.Generic.List<DnsFamily>();
        if(ipv4.IsChecked==true){var values=DnsProfiles.Validate(addresses4.Text+(Profile.Id=="custom"?" "+alternate4.Text:""),DnsFamily.IPv4);target=target.With(DnsFamily.IPv4,useAdvancedPolicy?DnsProfiles.ProfilePlan(snapshot.IPv4,values,SelectedPlanProfile!,useDoh.IsChecked==true,fallback.IsChecked==true):DnsProfiles.ManualPlan(snapshot.IPv4,values,SelectedPlanProfile!));families.Add(DnsFamily.IPv4);}
        if(ipv6.IsChecked==true){var values=DnsProfiles.Validate(addresses6.Text,DnsFamily.IPv6);target=target.With(DnsFamily.IPv6,DnsProfiles.ProfilePlan(snapshot.IPv6,values,SelectedPlanProfile!,useDoh.IsChecked==true,fallback.IsChecked==true));families.Add(DnsFamily.IPv6);}
        if(ipv6.IsChecked==true&&useDoh.IsChecked==true)throw new DnsException("Native IPv6 DoH cannot be verified independently. Keep IPv6 unchanged or choose plain DNS explicitly.");
        DnsTransaction.Validate(snapshot,target,families.ToArray(),false,true);
        if(reader is WindowsDnsSettings)foreach(var family in families)WindowsDnsSettings.ValidateDestination(target.Family(family).Servers);
        return(target,families.ToArray());
    }
    void UpdatePlan()
    {
        if(preview==null)return;
        var profile=Profile;profileAddresses.Text=profile==null||profile.Id=="custom"?"":"DNS IPv4 · "+string.Join(", ",profile.IPv4);preview.Text=L.T("IPv6 settings are preserved unless explicitly selected.")+"\n"+L.T("Preserved IPv6 DNS can use a different provider.");
        if(profile!=null&&profile.Source!="")preview.Text+="\n"+L.T("Source checked: ")+profile.CheckedOn+" · "+profile.Source;
        if(journal!=null)preview.Text+="\n"+L.T("Session original DNS:")+" "+string.Join(", ",journal.Before.IPv4.Servers)+"\n"+string.Join("\n",journal.History.TakeLast(8).Select(h=>h.Utc+" · "+L.T(h.Action)+" · "+h.ProfileName+" · "+L.T(h.Result)));
        if(snapshot?.SupportsDoh==false)preview.Text+="\n"+L.T("This Windows version cannot apply native DoH. Plain IPv4 DNS remains available.");
        if(SelectedPlanProfile?.Doh!=null)preview.Text+="\nDoH: "+SelectedPlanProfile.Doh+" · "+L.T(useDoh.IsChecked==true?"The displayed DoH endpoint will be applied after confirmation.":"DoH is not selected; plaintext DNS requires confirmation.");
        warning.Text=L.T(profile?.Warning.Length>0?profile.Warning:snapshot?.CanChange==false?snapshot.Limitation:"DNS and DoH changes require confirmation and UAC.");SetEnabled();
    }
    void SetEnabled()
    {
        if(apply==null)return;bool valid=false;string applyReason="";try{Plan();valid=true;}catch(DnsException ex){applyReason=L.T(ex.Message);}
        bool same=valid&&Plan().Target.SameConfiguration(snapshot!);if(same)applyReason=L.T("The selected DNS configuration is already active.");
        apply.IsEnabled=!busy&&valid&&SessionReady&&!same;reapply.IsEnabled=!busy&&valid&&(SessionReady||snapshot!=null&&journal!=null&&DnsTransaction.CanCompletePendingWrite(journal,snapshot))&&same;automatic.IsEnabled=!busy&&snapshot?.CanChange==true&&SessionReady&&snapshot.IPv4.Mode!=DnsMode.Automatic;flush.IsEnabled=!busy&&snapshot!=null;restore.IsEnabled=!busy&&snapshot?.CanChange==true&&journal?.Active==true&&DnsTransaction.Recoverable(journal,snapshot);refresh.IsEnabled=!busy;probe.IsEnabled=!busy&&snapshot!=null;adapters.IsEnabled=profiles.IsEnabled=ipv4.IsEnabled=ipv6.IsEnabled=ipv6Mode.IsEnabled=addresses4.IsEnabled=addresses6.IsEnabled=endpoint.IsEnabled=probeMode.IsEnabled=!busy;
        string restoreReason=snapshot==null?"DNS settings could not be read safely.":journal?.Active!=true?"Restore: no profile has been applied.":!DnsTransaction.Recoverable(journal,snapshot)?"DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.":!snapshot.CanChange?snapshot.Limitation:"Original DNS saved · restoration is available.";
        if(!SessionReady)applyReason=L.T("The managed DNS session needs recovery review before another change.");
        useDoh.IsEnabled=!busy;fallback.IsEnabled=!busy&&useDoh.IsChecked==true;clearCache.IsEnabled=profileName.IsEnabled=alternate4.IsEnabled=dohEndpoint.IsEnabled=saveProfile.IsEnabled=!busy;
        automatic.ToolTip=automatic.IsEnabled?L.T("Return IPv4 DNS assignment to automatic; preserve IP addressing and IPv6."):L.T(snapshot?.Limitation??"DNS settings could not be read safely.");
        reapplyItem.IsEnabled=reapply.IsEnabled;automaticItem.IsEnabled=automatic.IsEnabled;flushItem.IsEnabled=flush.IsEnabled;more.IsEnabled=!busy;reapplyItem.ToolTip=reapply.IsEnabled?L.T("Repeat writes the same DNS and DoH without DHCP or cache clearing."):L.T("Repeat requires the selected manual DNS and DoH to match the current configuration exactly.");
        if(simpleStatus!=null){resolve!.IsEnabled=!busy&&snapshot?.CanChange==true;bool canPower=!busy&&(OffAction?restore.IsEnabled:apply.IsEnabled||reapply.IsEnabled);if(window.FindName("DnsDone") is Button done)done.IsEnabled=!busy;simpleStatus!.Text=Status;basicNote!.Text=operationError.Length>0?operationError:!canPower&&snapshot?.CanChange==false?L.T(snapshot.Limitation):!SessionReady?L.T("Current settings differ from the saved session. Review them once before continuing."):Applied&&!OffAction?L.T("The selected profile has not been applied. Previous settings remain saved."):Applied?L.T("Previous settings are saved and will return when turned off."):same?L.T("These DNS settings already exist. Turning on will save the current settings for restoration."):!valid?applyReason:L.T("Current settings are saved automatically before applying.");resolve!.Visibility=journal?.Active==true&&!SessionReady?Visibility.Visible:Visibility.Collapsed;}
        if(window.FindName("DnsRestoreMenu") is MenuItem restoreEntry)restoreEntry.IsEnabled=restore.IsEnabled;
        reason.Text=(busy?L.T("DNS operation in progress."):(!apply.IsEnabled&&applyReason.Length>0?applyReason+"\n":"")+L.T(restoreReason));apply.ToolTip=apply.IsEnabled?null:applyReason;restore.ToolTip=L.T(restoreReason);
    }
    async Task Operation(bool restoring,bool automaticMode=false,bool cacheOnly=false,bool repeat=false)
    {
        if(busy||snapshot==null||(repeat?!reapply.IsEnabled:cacheOnly?!flush.IsEnabled:automaticMode?!automatic.IsEnabled:restoring?!restore.IsEnabled:!apply.IsEnabled))return;
        bool evidenceStarted=false;
        try{
            Read();if(snapshot==null)return;var before=snapshot;var plan=cacheOnly?(before,Array.Empty<DnsFamily>()):automaticMode?(before.With(DnsFamily.IPv4,new(DnsMode.Automatic,Array.Empty<string>(),Array.Empty<string>())),new[]{DnsFamily.IPv4}):restoring?(journal!.Before,journal.Families):Plan();string action=cacheOnly?"flush":automaticMode?"automatic":restoring?"restore":repeat?"reapply":"apply";var selected=SelectedPlanProfile;var custom=(action is "apply" or "reapply")&&Profile?.Id=="custom"?CustomPlan():null;
            if(repeat)DnsTransaction.ValidateReapply(before,plan.Item1,plan.Item2);
            string message=L.T(repeat?"Write the same DNS and DoH again for the selected adapter, without DHCP or clearing the cache?":cacheOnly?"Clear the system DNS cache without changing DNS settings?":automaticMode?"Switch this adapter IPv4 DNS to automatic?":restoring?"Restore the saved DNS configuration for this adapter?":"Apply the following DNS configuration to this adapter?")+"\n\n"+before.AdapterName+"\nIPv4: "+FamilySummary(before.IPv4)+"\n→ "+FamilySummary(plan.Item1.IPv4)+"\nIPv6: "+FamilySummary(before.IPv6)+"\n→ "+FamilySummary(plan.Item1.IPv6)+"\n\n"+L.T(shell.Runtime.TestMode?"Simulation only. Windows settings are unchanged; no UAC permission is needed.":"The connection may be interrupted. Other adapters and network tools are not changed. A protected recovery record is saved before writing. Windows will request UAC permission.");
            if(automaticMode)message+="\n\n"+L.T("Automatic DNS removes the manual IPv4 DNS and its active adapter DoH binding. DHCP DNS may be unencrypted or unavailable. IP addressing, gateway and IPv6 are unchanged. The original configuration remains recoverable.");
            if(!cacheOnly)message+="\n\n"+L.T("Confirming approves the displayed DNS encryption changes. The original session backup is preserved.");
            if(!shell.Runtime.TestMode&&Profile?.Warning.Length>0&&!restoring&&!automaticMode&&!cacheOnly)message+="\n\n"+L.T(Profile.Warning);
            bool confirmed;
#if OPENNET_TESTING
            if(shell.Runtime.TestMode&&ConfirmationForTests!=null)confirmed=ConfirmationForTests(message);else
#endif
            confirmed=AppDialog.Show(advancedWindow?.IsVisible==true?advancedWindow:window,message,L.T("Open Net — DNS confirmation"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
            if(!confirmed)return;
            lastEvidence=new DnsOperationEvidence();lastEvidence.Begin(action,before);operationBefore=before;operationTarget=plan.Item1;evidenceStarted=true;operationError="";restoringOperation=restoring;busy=true;SetEnabled();shell.UpdateDnsStatus();
#if OPENNET_TESTING
            if(shell.Runtime.TestMode){
                int failure=(window.FindName("DnsSimulatedFailure") as ComboBox)?.SelectedIndex??0;if(failure==1)throw new System.ComponentModel.Win32Exception(1223,"Simulated UAC denial.");
                if(reader is TestsDns.FakeSettings fake){bool once=false;fake.BeforeWrite=f=>{if(!once&&(failure==2&&!restoring||failure==3&&restoring)){once=true;throw new IOException("Simulated DNS write failure.");}};fake.AfterWrite=()=>{if(failure==4&&!once){once=true;fake.ReadOverride=value=>{fake.ReadOverride=null;return value with{DependencyFingerprint="simulated-verification-mismatch"};};}};}
                try{if(cacheOnly&&failure==5){lastEvidence.CacheResult="failed";throw new DnsException("DNS cache clearing failed. DNS settings were not changed.");}if(!cacheOnly){var transaction=new DnsTransaction(reader,(IDnsSettingsWriter)reader,store,"safe-test",lastEvidence);if(restoring)transaction.Restore(Adapter,before.Fingerprint());else if(automaticMode)transaction.Automatic(before);else if(repeat)transaction.Reapply(before,plan.Item1,plan.Item2,selected!.Id,selected.Name);else transaction.Apply(before,plan.Item1,plan.Item2,selected!.Id,true,selected.Name);}result.Text=L.T(repeat?"Simulated repeat completed. DNS values are unchanged; cache clearing and HTTPS checks were not performed.":"Simulated DNS operation completed. System settings were not changed.");if(!repeat&&(cacheOnly||clearCache.IsChecked==true)){lastEvidence.CacheResult=failure==5?"failed":"cleared";result.Text+="\n"+L.T(failure==5?"DNS configuration is verified, but cache clearing failed. Retry cache clearing separately.":DnsCache.Clear(true));}}finally{if(reader is TestsDns.FakeSettings reset){reset.BeforeWrite=null;reset.AfterWrite=null;reset.ReadOverride=null;}}
            }
            else
#endif
            {
                using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);string token=Guid.NewGuid().ToString("N");
                var request=new DnsRequest(token,process.Id,identity.StartFileTime,DnsRecoveryStore.Owner(),shell.Settings.Language,action,before,plan.Item1,plan.Item2,before.Fingerprint()){ProfileId=selected?.Id??"",ProfileName=selected?.Name??"",CustomProfile=custom,EncryptionChangeApproved=true,ClearCache=!repeat&&clearCache.IsChecked==true};
                var response=await DnsElevated.Execute(files,request,false);result.Text=response.Message;lastEvidence=response.Evidence??lastEvidence;if(!response.Success)throw new DnsException(response.Message);
            }
            Read();
            // Export is a readable copy only; privileged restore always uses the protected journal.
            if(journal!=null){string backup=Paths.Owned(files.Data,"Dns/Backups/"+journal.Id.ToString("N")+".json");Directory.CreateDirectory(Path.GetDirectoryName(backup)!);string original=Paths.Owned(files.Data,"Dns/Backups/"+journal.Id.ToString("N")+".original.json");if(!File.Exists(original))Paths.WriteNew(original,JsonSerializer.Serialize(new{Authoritative=false,Original=journal.Before},new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(backup,JsonSerializer.Serialize(journal,new JsonSerializerOptions{WriteIndented=true}));}
        }catch(Exception ex){files.Log("DNS operation: "+ex);operationError=Error(ex);result.Text=operationError;Read();}finally{busy=false;if(evidenceStarted)ShowOperationEvidence();SetEnabled();shell.UpdateDnsStatus();}
    }
    void ShowOperationEvidence(){if(lastEvidence==null)return;operationEvidence.Text=L.T(shell.Runtime.TestMode?"Simulated operation evidence":"Windows operation evidence")+"\n"+lastEvidence.AdapterName+" · "+L.T(lastEvidence.Action)+" · "+lastEvidence.StartedUtc+"\n"+L.T("System write:")+" "+L.T(lastEvidence.WriteResult)+" · "+lastEvidence.AcceptedWrites+"/"+lastEvidence.RequestedWrites+"\n"+L.T("Configuration readback:")+" "+L.T(lastEvidence.VerificationResult)+"\n"+L.T("Recovery check:")+" "+L.T(lastEvidence.RecoveryResult)+"\n"+L.T("Values changed:")+" "+L.T(lastEvidence.ValuesChanged==true?"yes":lastEvidence.ValuesChanged==false?"no":"unknown")+"\n"+L.T("A network-state change and service availability have not been confirmed.");operationEvidence.Text+="\n"+L.T("Cache clearing:")+" "+L.T(lastEvidence.CacheResult);if(lastEvidence.Action=="reapply")operationEvidence.Text+="\n"+L.T("Repeat did not clear the DNS cache.");try{string receipt=Paths.Owned(files.Data,"Dns/OperationReceipts/"+Guid.NewGuid().ToString("N")+".json");Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);File.WriteAllText(receipt,JsonSerializer.Serialize(new{Evidence=lastEvidence,Before=operationBefore,Target=operationTarget,Observed=snapshot,Simulation=shell.Runtime.TestMode},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception ex){files.Log("DNS evidence receipt: "+ex);}}
    async Task Probe()
    {
        var uri=(endpoint.SelectedItem as ComboBoxItem)?.Tag as Uri;if(busy||uri==null)return;
        var kind=(DnsProbeKind)probeMode.SelectedIndex;
        string message=L.T(shell.Runtime.TestMode?"Simulation only. Windows settings are unchanged; no UAC permission is needed.":kind==DnsProbeKind.Names?"Send only a system name lookup for:":kind==DnsProbeKind.Https?"Send only an unauthenticated HTTPS HEAD request to:":"Send a system DNS lookup and an unauthenticated HTTPS HEAD request to:")+"\n"+uri+"\n\n"+L.T("No cookies or account credentials are sent. Normal certificate validation is used. The system proxy and DNS cache may affect the result. This check is not bound to the selected adapter and does not prove that your account works.");
        bool confirmed;
#if OPENNET_TESTING
        if(shell.Runtime.TestMode&&ConfirmationForTests!=null)confirmed=ConfirmationForTests(message);else
#endif
        confirmed=AppDialog.Show(advancedWindow?.IsVisible==true?advancedWindow:window,message,L.T("Open Net — connection check"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;if(!confirmed)return;
        try{busy=true;SetEnabled();var value=await probes.Run(uri,cancellation.Token,kind);probeEvidence.Text=L.T(shell.Runtime.TestMode?"Simulated connection evidence":"Connection evidence")+"\n"+value.Endpoint+"\n"+L.T("System lookup (not bound to the selected adapter):")+" "+L.T(value.NamesRequested?value.DnsResolved?"resolved":"failed":"not performed")+" · "+(value.NamesRequested?L.T(value.DnsDetail):"")+"\nHTTPS: "+(value.HttpsRequested?(value.HttpStatus.HasValue?"HTTP "+value.HttpStatus:"—")+" · "+L.T(value.HttpsDetail):L.T("not tested"));
            string receipt=Paths.Owned(files.Data,"Dns/ProbeReceipts/"+Guid.NewGuid().ToString("N")+".json");Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);File.WriteAllText(receipt,JsonSerializer.Serialize(new{Probe=value,SelectedAdapter=Adapter,BoundToSelectedAdapter=false,Simulation=shell.Runtime.TestMode},new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception ex){probeEvidence.Text=Error(ex);}finally{busy=false;SetEnabled();}
    }
    static string Error(Exception ex)=>ex is System.ComponentModel.Win32Exception {NativeErrorCode:1223}?L.T("Windows permission was declined. DNS settings were not changed."):ex is DnsException?L.T(ex.Message):L.Error(ex);
}
