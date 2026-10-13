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
    readonly Window window;readonly StackPanel panel;readonly ComboBox adapters,profiles,endpoint;readonly CheckBox ipv4,ipv6;readonly TextBox addresses4,addresses6;
    readonly TextBlock current,preview,warning,result,reason,profileAddresses;readonly StackPanel customFields;readonly ComboBox ipv6Mode;readonly Button apply,restore,refresh,probe;readonly CancellationTokenSource cancellation=new();
    DnsSnapshot? snapshot;DnsJournal? journal;bool busy;
    internal Window Window=>window;
    internal bool IsBusy=>busy;
#if OPENNET_TESTING
    internal Func<string,bool>? ConfirmationForTests {get;set;}
    internal static IDnsSettingsReader? ReaderForTests {get;set;}
    internal Task OperationForTests(bool restoring){if(!shell.Runtime.TestMode)throw new InvalidOperationException();return Operation(restoring);}
#endif
    internal string Status=>shell.Runtime.TestMode?"Preview mode":snapshot==null?"DNS settings could not be read safely.":journal?.Active!=true?"System DNS":!DnsTransaction.Recoverable(journal,snapshot)?"Settings changed externally":journal.Phase==DnsPhase.Applied?ProfileStatus(journal):"An unfinished DNS operation needs recovery review.";
    internal static string ProfileStatus(DnsJournal record)=>record.ProfileId switch {"comss"=>"Profile ComssDNS","xbox"=>"Profile Xbox DNS","custom"=>"Profile Custom DNS",_=>"A DNS profile is applied by Open Net."};
    // Delay protected-store access until inspection, so errors are visible inside the window.
    sealed class ReadOnlyRecovery : IDnsJournalStore {
        public DnsJournal? Load(Guid id)=>DnsRecoveryStore.Open(DnsRecoveryStore.Owner(),false).Load(id);
        public void Save(DnsJournal record)=>throw new InvalidOperationException("Only the elevated helper writes protected recovery records.");
    }
    internal DnsManagerView(Shell shell,AppFiles files)
    {
        this.shell=shell;this.files=files;
#if OPENNET_TESTING
        if(shell.Runtime.TestMode){reader=ReaderForTests??TestsDns.Fixture(files.Data);store=new DnsFileJournalStore(Paths.Owned(files.Data,"Dns/IsolatedRecovery"),"safe-test");probes=new TestsDns.FakeProbes();}
        else
#endif
        {reader=new WindowsDnsSettings();store=new ReadOnlyRecovery();probes=new WindowsDnsProbeRunner();}
        window=SettingsLayout.Create(shell,L.T("DNS Manager — Open Net"),680);window.MinHeight=460;panel=SettingsLayout.Panel(window);
        SettingsLayout.Heading(panel,"DNS Manager",L.T(shell.Runtime.TestMode?"Preview mode · Windows settings are unchanged.":"Configure DNS for one network adapter. Network tools are controlled separately."));
        Label("Network adapter");adapters=Combo("DnsAdapter");adapters.SelectionChanged+=(_,_)=>Read();
        var currentHeader=new Grid{Margin=new Thickness(0,12,0,0)};currentHeader.Children.Add(new TextBlock{Text=L.T("Current DNS"),VerticalAlignment=VerticalAlignment.Center});
        refresh=SettingsLayout.IconButton(window,"DnsRefresh","Check settings","RefreshGeometry");refresh.HorizontalAlignment=HorizontalAlignment.Right;
        var arrow=new System.Windows.Shapes.Path{Data=(System.Windows.Media.Geometry)shell.Window.Resources["RefreshGeometry"],Style=(Style)shell.Window.Resources["IconArrow"]};refresh.Content=new Grid{Width=18,Height=18,Children={arrow}};currentHeader.Children.Add(refresh);panel.Children.Add(currentHeader);refresh.Click+=(_,_)=>Read();
        current=AddText("Current settings","SecondaryText");current.Name="DnsCurrent";current.Margin=new Thickness(0,4,0,8);
        Label("DNS profile");profiles=Combo("DnsProfile");profiles.Items.Add(new ComboBoxItem{Content=L.T("Choose a profile"),Tag=null});foreach(var item in DnsProfiles.All)profiles.Items.Add(new ComboBoxItem{Content=L.T(item.Name),Tag=item});profiles.SelectedIndex=0;
        profiles.SelectionChanged+=(_,_)=>ProfileChanged();
        profileAddresses=AddText("","SecondaryText");profileAddresses.Name="DnsProfileAddresses";
        customFields=new StackPanel{Visibility=Visibility.Collapsed};
        addresses4=Input("DnsAddresses4","IPv4 DNS addresses, in order");var label4=panel.Children[panel.Children.Count-2];panel.Children.Remove(label4);panel.Children.Remove(addresses4);customFields.Children.Add(label4);customFields.Children.Add(addresses4);
        addresses6=Input("DnsAddresses6","IPv6 DNS addresses, in order");var label6=panel.Children[panel.Children.Count-2];panel.Children.Remove(label6);panel.Children.Remove(addresses6);customFields.Children.Add(label6);customFields.Children.Add(addresses6);panel.Children.Add(customFields);
        ipv4=SettingsLayout.Toggle(window,panel,shell,"DnsIPv4","Change IPv4 DNS",true);
        // The retained checkbox is the internal family flag; the visible choice states preservation explicitly.
        ipv6=new CheckBox{IsChecked=false};window.RegisterName("DnsIPv6",ipv6);
        Label("IPv6 DNS");ipv6Mode=Combo("DnsIPv6Mode");ipv6Mode.Items.Add(new ComboBoxItem{Content=L.T("Keep current settings")});ipv6Mode.Items.Add(new ComboBoxItem{Content=L.T("Use profile addresses")});ipv6Mode.SelectedIndex=0;ipv6Mode.SelectionChanged+=(_,_)=>ipv6.IsChecked=ipv6Mode.SelectedIndex==1;
        addresses4.TextChanged+=(_,_)=>UpdatePlan();addresses6.TextChanged+=(_,_)=>UpdatePlan();ipv4.Checked+=(_,_)=>UpdatePlan();ipv4.Unchecked+=(_,_)=>UpdatePlan();ipv6.Checked+=(_,_)=>UpdatePlan();ipv6.Unchecked+=(_,_)=>UpdatePlan();
        var details=Disclosure("Profile details");preview=AddText("IPv6 settings are preserved unless explicitly selected.","Muted");preview.Name="DnsPreview";panel.Children.Remove(preview);details.Children.Add(preview);warning=AddText("DoH changes are not supported in this alpha.","Warning");warning.Name="DnsWarning";panel.Children.Remove(warning);details.Children.Add(warning);
        var diagnostics=Disclosure("Connection check");endpoint=Combo("DnsEndpoint");panel.Children.Remove(endpoint);diagnostics.Children.Add(endpoint);foreach(var uri in WindowsDnsProbeRunner.Endpoints)endpoint.Items.Add(new ComboBoxItem{Content=uri.Host,Tag=uri});endpoint.SelectedIndex=0;
        probe=SettingsLayout.Button(window,"DnsProbe","Check connection…");probe.Margin=new Thickness(0,8,0,0);diagnostics.Children.Add(probe);probe.Click+=async(_,_)=>await Probe();
        var disclaimer=AddText("DNS and HTTPS results do not prove that ChatGPT or Gemini works.","Muted");panel.Children.Remove(disclaimer);diagnostics.Children.Add(disclaimer);
        result=AddText("","SecondaryText");result.Name="DnsResult";
        reason=new TextBlock{Name="DnsReason",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};ThemeManager.Bind(reason,TextBlock.ForegroundProperty,"Muted");window.RegisterName("DnsReason",reason);
        var actions=SettingsLayout.ActionGrid(window,panel,"DnsActions");apply=SettingsLayout.Button(window,"DnsApply","Apply…");restore=SettingsLayout.Button(window,"DnsRestore","Restore…");SettingsLayout.AddAction(actions,apply,0);SettingsLayout.AddAction(actions,restore,1);ThemeManager.Bind(apply,Control.BackgroundProperty,"PrimaryBackground");ThemeManager.Bind(apply,Control.ForegroundProperty,"PrimaryText");
        apply.Click+=async(_,_)=>await Operation(false);restore.Click+=async(_,_)=>await Operation(true);
        panel.Children.Remove(actions);var scroll=(ScrollViewer)window.Content;window.RegisterName("DnsScroll",scroll);window.Content=null;
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.Children.Add(scroll);
        var footerContent=new StackPanel();footerContent.Children.Add(reason);footerContent.Children.Add(actions);actions.Margin=new Thickness(0);
        var footer=new Border{Padding=new Thickness(26,10,26,18),BorderThickness=new Thickness(0,1,0,0),Child=footerContent};ThemeManager.Bind(footer,Border.BorderBrushProperty,"Line");ThemeManager.Bind(footer,Border.BackgroundProperty,"Surface");Grid.SetRow(footer,1);layout.Children.Add(footer);window.Content=layout;
        try{foreach(var item in reader.Adapters())adapters.Items.Add(new ComboBoxItem{Content=item.Name+" · "+L.T(item.Kind)+(item.Virtual?" · "+L.T("read-only"):""),Tag=item});}
        catch(Exception ex){files.Log("DNS adapter inspection: "+ex);current.Text=L.T("DNS settings could not be read safely.")+" "+Error(ex);}
        window.Closing+=(_,e)=>{if(busy)e.Cancel=true;};window.Closed+=(_,_)=>{cancellation.Cancel();cancellation.Dispose();shell.UpdateDnsStatus();};
        var preferred=adapters.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>x.Tag is DnsAdapter d&&d.Connected&&!d.Virtual);if(preferred!=null)adapters.SelectedItem=preferred;else if(adapters.Items.Count>0)adapters.SelectedIndex=0;
        if(adapters.Items.Count==0){warning.Text=L.T("No supported network adapters were found.");SetEnabled();}
        ThemeManager.BindTree(window);LocalizedTree.Bind(window);
        void LanguageChanged(){Read();UpdatePlan();}L.Changed+=LanguageChanged;window.Closed+=(_,_)=>L.Changed-=LanguageChanged;
    }
    StackPanel Disclosure(string caption)
    {
        var content=new StackPanel{Visibility=Visibility.Collapsed};var header=new Button{Content="› "+L.T(caption),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0),Padding=new Thickness(0,5,0,5),Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new Thickness(0)};
        void UpdateCaption()=>header.Content=(content.Visibility==Visibility.Visible?"⌄ ":"› ")+L.T(caption);
        ThemeManager.Bind(header,Control.ForegroundProperty,"SecondaryText");header.Click+=(_,_)=>{content.Visibility=content.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;UpdateCaption();};L.Changed+=UpdateCaption;window.Closed+=(_,_)=>L.Changed-=UpdateCaption;panel.Children.Add(header);panel.Children.Add(content);return content;
    }
    void Label(string text)=>SettingsLayout.Label(panel,text);
    TextBlock AddText(string text,string color){var block=new TextBlock{Text=L.T(text),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};ThemeManager.Bind(block,TextBlock.ForegroundProperty,color);panel.Children.Add(block);return block;}
    ComboBox Combo(string name){var box=new ComboBox{Height=36,Margin=new Thickness(0,0,0,4)};window.RegisterName(name,box);panel.Children.Add(box);return box;}
    TextBox Input(string name,string label){Label(label);var box=new TextBox{Height=36,Padding=new Thickness(6,2,6,2),VerticalContentAlignment=VerticalAlignment.Center};window.RegisterName(name,box);ThemeManager.Bind(box,Control.BackgroundProperty,"Input");ThemeManager.Bind(box,Control.ForegroundProperty,"Text");ThemeManager.Bind(box,Control.BorderBrushProperty,"InputBorder");panel.Children.Add(box);return box;}
    DnsProfile? Profile=>(profiles.SelectedItem as ComboBoxItem)?.Tag as DnsProfile;
    Guid Adapter=>(adapters.SelectedItem as ComboBoxItem)?.Tag is DnsAdapter value?value.Id:Guid.Empty;
    void Read()
    {
        if(current==null)return;
        try{snapshot=reader.Read(Adapter);journal=store.Load(Adapter);current.Text="IPv4 · "+FamilyText(snapshot.IPv4)+"\nIPv6 · "+FamilyText(snapshot.IPv6)+"\n"+L.T(snapshot.CanChange?"No encrypted DNS override detected by the alpha backend.":snapshot.Limitation);if(journal?.Active==true){current.Text+="\n"+L.T(DnsTransaction.Recoverable(journal,snapshot)?journal.Phase==DnsPhase.Applied?"A DNS profile is applied by Open Net.":"An unfinished DNS operation needs recovery review.":"DNS settings were changed outside Open Net.");}}
        catch(Exception ex){snapshot=null;journal=null;files.Log("DNS inspection: "+ex);current.Text=L.T("DNS settings could not be read safely.")+" "+Error(ex);}
        UpdatePlan();shell.UpdateDnsStatus();
    }
    static string FamilyText(DnsFamilySettings settings)=>L.T(settings.Mode==DnsMode.Manual?"Manual":settings.Mode==DnsMode.Automatic?"Automatic":"Unknown")+" · "+string.Join(", ",settings.Mode==DnsMode.Manual?settings.Servers:settings.EffectiveServers);
    void ProfileChanged()
    {
        if(addresses4==null)return;var profile=Profile;addresses4.Text=profile==null?"":string.Join(", ",profile.IPv4);addresses6.Text=profile==null?"":string.Join(", ",profile.IPv6);addresses4.IsReadOnly=addresses6.IsReadOnly=profile?.Id!="custom";ipv6.IsChecked=false;ipv6Mode.SelectedIndex=0;customFields.Visibility=profile?.Id=="custom"?Visibility.Visible:Visibility.Collapsed;UpdatePlan();
    }
    (DnsSnapshot Target,DnsFamily[] Families) Plan()
    {
        if(snapshot==null)throw new DnsException("DNS settings could not be read safely.");
        if(Profile==null)throw new DnsException("Choose an adapter and a DNS profile.");
        if(!shell.Runtime.TestMode&&!Profile.Available)throw new DnsException(Profile.Warning);
        var target=snapshot;var families=new System.Collections.Generic.List<DnsFamily>();
        if(ipv4.IsChecked==true){var values=DnsProfiles.Validate(addresses4.Text,DnsFamily.IPv4);target=target.With(DnsFamily.IPv4,new(DnsMode.Manual,values,values));families.Add(DnsFamily.IPv4);}
        if(ipv6.IsChecked==true){var values=DnsProfiles.Validate(addresses6.Text,DnsFamily.IPv6);target=target.With(DnsFamily.IPv6,new(DnsMode.Manual,values,values));families.Add(DnsFamily.IPv6);}
        DnsTransaction.Validate(snapshot,target,families.ToArray());
        if(reader is WindowsDnsSettings)foreach(var family in families)WindowsDnsSettings.ValidateDestination(target.Family(family).Servers);
        return(target,families.ToArray());
    }
    void UpdatePlan()
    {
        if(preview==null)return;
        var profile=Profile;profileAddresses.Text=profile==null||profile.Id=="custom"?"":"DNS IPv4 · "+string.Join(", ",profile.IPv4);preview.Text=L.T("IPv6 settings are preserved unless explicitly selected.")+"\n"+L.T("Preserved IPv6 DNS can use a different provider.");
        if(profile!=null&&profile.Source!="")preview.Text+="\n"+L.T("Source checked: ")+profile.CheckedOn+" · "+profile.Source;
        if(profile?.Doh!=null)preview.Text+="\nDoH: "+profile.Doh+" · "+L.T("information only");
        warning.Text=L.T(profile?.Warning.Length>0?profile.Warning:snapshot?.CanChange==false?snapshot.Limitation:"DoH changes are not supported in this alpha.");SetEnabled();
    }
    void SetEnabled()
    {
        if(apply==null)return;bool valid=false;string applyReason="";try{Plan();valid=true;}catch(DnsException ex){applyReason=L.T(ex.Message);}
        apply.IsEnabled=!busy&&valid&&journal?.Active!=true;restore.IsEnabled=!busy&&snapshot?.CanChange==true&&journal?.Active==true&&DnsTransaction.Recoverable(journal,snapshot);refresh.IsEnabled=!busy;probe.IsEnabled=!busy&&snapshot!=null;adapters.IsEnabled=profiles.IsEnabled=ipv4.IsEnabled=ipv6.IsEnabled=ipv6Mode.IsEnabled=addresses4.IsEnabled=addresses6.IsEnabled=endpoint.IsEnabled=!busy;
        string restoreReason=snapshot==null?"DNS settings could not be read safely.":journal?.Active!=true?"Restore: no profile has been applied.":!DnsTransaction.Recoverable(journal,snapshot)?"DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.":!snapshot.CanChange?snapshot.Limitation:"Original DNS saved · restoration is available.";
        if(journal?.Active==true)applyReason=L.T("Restore the existing DNS transaction before applying another profile.");
        reason.Text=(busy?L.T("DNS operation in progress."):(!apply.IsEnabled&&applyReason.Length>0?applyReason+"\n":"")+L.T(restoreReason));apply.ToolTip=apply.IsEnabled?null:applyReason;restore.ToolTip=L.T(restoreReason);
    }
    async Task Operation(bool restoring)
    {
        if(busy||snapshot==null||(restoring?!restore.IsEnabled:!apply.IsEnabled))return;
        try{
            var before=snapshot;var plan=restoring?(journal!.Before,journal.Families):Plan();
            string message=L.T(restoring?"Restore the saved DNS configuration for this adapter?":"Apply the following DNS configuration to this adapter?")+"\n\n"+before.AdapterName+"\nIPv4: "+FamilyText(before.IPv4)+"\n→ "+FamilyText(plan.Item1.IPv4)+"\nIPv6: "+FamilyText(before.IPv6)+"\n→ "+FamilyText(plan.Item1.IPv6)+"\n\n"+L.T(shell.Runtime.TestMode?"Simulation only. Windows settings are unchanged; no UAC permission is needed.":"The connection may be interrupted. Other adapters and network tools are not changed. A protected recovery record is saved before writing. Windows will request UAC permission.");
            bool confirmed;
#if OPENNET_TESTING
            if(shell.Runtime.TestMode&&ConfirmationForTests!=null)confirmed=ConfirmationForTests(message);else
#endif
            confirmed=AppDialog.Show(window,message,L.T("Open Net — DNS confirmation"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
            if(!confirmed)return;
            busy=true;SetEnabled();
#if OPENNET_TESTING
            if(shell.Runtime.TestMode){var transaction=new DnsTransaction(reader,(IDnsSettingsWriter)reader,store,"safe-test");if(restoring)transaction.Restore(Adapter,before.Fingerprint());else transaction.Apply(before,plan.Item1,plan.Item2,Profile!.Id);result.Text=L.T("Simulated DNS operation completed. System settings were not changed.");}
            else
#endif
            {
                using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);string token=Guid.NewGuid().ToString("N");
                var request=new DnsRequest(token,process.Id,identity.StartFileTime,DnsRecoveryStore.Owner(),shell.Settings.Language,restoring?"restore":"apply",before,plan.Item1,plan.Item2,before.Fingerprint()){ProfileId=Profile?.Id??""};
                var response=await DnsElevated.Execute(files,request,false);result.Text=response.Message;
            }
            Read();
            // Export is a readable copy only; privileged restore always uses the protected journal.
            if(journal!=null){string backup=Paths.Owned(files.Data,"Dns/Backups/"+journal.Id.ToString("N")+".json");Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.WriteAllText(backup,JsonSerializer.Serialize(journal,new JsonSerializerOptions{WriteIndented=true}));}
        }catch(Exception ex){result.Text=Error(ex);Read();}finally{busy=false;SetEnabled();shell.UpdateDnsStatus();}
    }
    async Task Probe()
    {
        var uri=(endpoint.SelectedItem as ComboBoxItem)?.Tag as Uri;if(busy||uri==null)return;
        string message=L.T(shell.Runtime.TestMode?"Simulate a DNS and HTTPS check for:":"Send a system DNS lookup and an unauthenticated HTTPS HEAD request to:")+"\n"+uri+"\n\n"+L.T("No cookies or account credentials are sent. Normal certificate validation is used. The system proxy and DNS cache may affect the result. This check is not bound to the selected adapter and does not prove that your account works.");
        if(AppDialog.Show(window,message,L.T("Open Net — connection check"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{busy=true;SetEnabled();var value=await probes.Run(uri,cancellation.Token);result.Text=value.Endpoint+"\nDNS: "+L.T(value.DnsResolved?"resolved":"failed")+" · "+L.T(value.DnsDetail)+"\nHTTPS: "+(value.HttpStatus.HasValue?"HTTP "+value.HttpStatus:"—")+" · "+L.T(value.HttpsDetail);}
        catch(Exception ex){result.Text=Error(ex);}finally{busy=false;SetEnabled();}
    }
    static string Error(Exception ex)=>ex is DnsException?L.T(ex.Message):L.Error(ex);
}
