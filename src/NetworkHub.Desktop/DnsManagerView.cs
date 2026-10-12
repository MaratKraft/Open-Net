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
    readonly TextBlock current,preview,warning,result;readonly Button apply,restore,refresh,probe;readonly CancellationTokenSource cancellation=new();
    DnsSnapshot? snapshot;DnsJournal? journal;bool busy;
    internal Window Window=>window;
    internal bool IsBusy=>busy;
#if OPENNET_TESTING
    internal Func<string,bool>? ConfirmationForTests {get;set;}
    internal Task OperationForTests(bool restoring){if(!shell.Runtime.TestMode)throw new InvalidOperationException();return Operation(restoring);}
#endif
    internal string Status=>snapshot==null?"Open to inspect DNS settings":journal?.Active!=true?"Not changed by Open Net":!DnsTransaction.Recoverable(journal,snapshot)?"DNS settings were changed outside Open Net.":journal.Phase==DnsPhase.Applied?"A DNS profile is applied by Open Net.":"An unfinished DNS operation needs recovery review.";
    internal DnsManagerView(Shell shell,AppFiles files)
    {
        this.shell=shell;this.files=files;
#if OPENNET_TESTING
        if(shell.Runtime.TestMode){var fake=TestsDns.Fixture();reader=fake;store=new DnsFileJournalStore(Paths.Owned(files.Data,"Dns/IsolatedRecovery"),"safe-test");probes=new TestsDns.FakeProbes();}
        else
#endif
        {reader=new WindowsDnsSettings();store=DnsRecoveryStore.Open(DnsRecoveryStore.Owner(),false);probes=new WindowsDnsProbeRunner();}
        window=SettingsLayout.Create(shell,L.T("DNS Manager — Open Net"),760);window.MinHeight=520;panel=SettingsLayout.Panel(window);
        SettingsLayout.Heading(panel,"DNS Manager",L.T("Configure DNS for one network adapter. Network tools are controlled separately."));
        if(shell.Runtime.TestMode)AddText("Safe testing: DNS settings and network checks are simulated.","Warning");
        Label("Network adapter");adapters=Combo("DnsAdapter");foreach(var item in reader.Adapters())adapters.Items.Add(new ComboBoxItem{Content=item.Name+" · "+L.T(item.Kind)+(item.Virtual?" · "+L.T("read-only"):""),Tag=item});
        adapters.SelectionChanged+=(_,_)=>Read();
        current=AddText("Current settings","SecondaryText");current.Margin=new Thickness(0,16,0,12);
        Label("DNS profile");profiles=Combo("DnsProfile");profiles.Items.Add(new ComboBoxItem{Content=L.T("Choose a profile"),Tag=null});foreach(var item in DnsProfiles.All)profiles.Items.Add(new ComboBoxItem{Content=L.T(item.Name),Tag=item});profiles.SelectedIndex=0;
        profiles.SelectionChanged+=(_,_)=>ProfileChanged();
        ipv4=SettingsLayout.Toggle(window,panel,shell,"DnsIPv4","Change IPv4 DNS",true);ipv6=SettingsLayout.Toggle(window,panel,shell,"DnsIPv6","Change IPv6 DNS",false);
        addresses4=Input("DnsAddresses4","IPv4 DNS addresses, in order");addresses6=Input("DnsAddresses6","IPv6 DNS addresses, in order");
        addresses4.TextChanged+=(_,_)=>UpdatePlan();addresses6.TextChanged+=(_,_)=>UpdatePlan();ipv4.Checked+=(_,_)=>UpdatePlan();ipv4.Unchecked+=(_,_)=>UpdatePlan();ipv6.Checked+=(_,_)=>UpdatePlan();ipv6.Unchecked+=(_,_)=>UpdatePlan();
        preview=AddText("IPv6 settings are preserved unless explicitly selected.","Muted");warning=AddText("DoH changes are not supported in this alpha.","Warning");
        var actions=SettingsLayout.ActionGrid(window,panel,"DnsActions");apply=SettingsLayout.Button(window,"DnsApply","Apply…");refresh=SettingsLayout.Button(window,"DnsRefresh","Check settings");restore=SettingsLayout.Button(window,"DnsRestore","Restore…");SettingsLayout.AddAction(actions,apply,0);SettingsLayout.AddAction(actions,refresh,1);SettingsLayout.AddAction(actions,restore,2);ThemeManager.Bind(apply,Control.BackgroundProperty,"PrimaryBackground");ThemeManager.Bind(apply,Control.ForegroundProperty,"PrimaryText");
        apply.Click+=async(_,_)=>await Operation(false);restore.Click+=async(_,_)=>await Operation(true);refresh.Click+=(_,_)=>Read();
        Label("Optional connection check");endpoint=Combo("DnsEndpoint");foreach(var uri in WindowsDnsProbeRunner.Endpoints)endpoint.Items.Add(new ComboBoxItem{Content=uri.Host,Tag=uri});endpoint.SelectedIndex=0;
        probe=SettingsLayout.Button(window,"DnsProbe","Check connection…");probe.Margin=new Thickness(0,8,0,0);panel.Children.Add(probe);probe.Click+=async(_,_)=>await Probe();
        result=AddText("DNS and HTTPS results do not prove that ChatGPT or Gemini works.","Muted");
        var close=SettingsLayout.Button(window,"DnsClose","Close");close.Click+=(_,_)=>window.Close();SettingsLayout.AddAction(actions,close,3);
        // Keep Apply/Restore available while long status, provider and probe details scroll.
        panel.Children.Remove(actions);var scroll=(ScrollViewer)window.Content;window.RegisterName("DnsScroll",scroll);window.Content=null;
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.Children.Add(scroll);
        var footer=new Border{Padding=new Thickness(26,8,26,18),BorderThickness=new Thickness(0,1,0,0),Child=actions};ThemeManager.Bind(footer,Border.BorderBrushProperty,"Line");ThemeManager.Bind(footer,Border.BackgroundProperty,"Surface");Grid.SetRow(footer,1);layout.Children.Add(footer);window.Content=layout;
        window.Closing+=(_,e)=>{if(busy)e.Cancel=true;};window.Closed+=(_,_)=>{cancellation.Cancel();cancellation.Dispose();shell.UpdateDnsStatus();};
        var preferred=adapters.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>x.Tag is DnsAdapter d&&d.Connected&&!d.Virtual);if(preferred!=null)adapters.SelectedItem=preferred;else if(adapters.Items.Count>0)adapters.SelectedIndex=0;
        if(adapters.Items.Count==0){warning.Text=L.T("No supported network adapters were found.");SetEnabled();}
        ThemeManager.BindTree(window);LocalizedTree.Bind(window);
        void LanguageChanged(){Read();UpdatePlan();}L.Changed+=LanguageChanged;window.Closed+=(_,_)=>L.Changed-=LanguageChanged;
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
        try{snapshot=reader.Read(Adapter);journal=store.Load(Adapter);current.Text=L.T("Current settings")+"\nIPv4 · "+FamilyText(snapshot.IPv4)+"\nIPv6 · "+FamilyText(snapshot.IPv6)+"\n"+L.T(snapshot.CanChange?"No encrypted DNS override detected by the alpha backend.":snapshot.Limitation);if(journal?.Active==true){current.Text+="\n"+L.T(DnsTransaction.Recoverable(journal,snapshot)?journal.Phase==DnsPhase.Applied?"A DNS profile is applied by Open Net.":"An unfinished DNS operation needs recovery review.":"DNS settings were changed outside Open Net.");}}
        catch(Exception ex){snapshot=null;journal=null;files.Log("DNS inspection: "+ex);current.Text=L.T("DNS settings could not be read safely.")+" "+Error(ex);}
        UpdatePlan();shell.UpdateDnsStatus();
    }
    static string FamilyText(DnsFamilySettings settings)=>L.T(settings.Mode==DnsMode.Manual?"Manual":settings.Mode==DnsMode.Automatic?"Automatic":"Unknown")+" · "+string.Join(", ",settings.Mode==DnsMode.Manual?settings.Servers:settings.EffectiveServers);
    void ProfileChanged()
    {
        if(addresses4==null)return;var profile=Profile;addresses4.Text=profile==null?"":string.Join(", ",profile.IPv4);addresses6.Text=profile==null?"":string.Join(", ",profile.IPv6);addresses4.IsReadOnly=addresses6.IsReadOnly=profile?.Id!="custom";ipv6.IsChecked=false;UpdatePlan();
    }
    (DnsSnapshot Target,DnsFamily[] Families) Plan()
    {
        if(snapshot==null||Profile==null)throw new DnsException("Choose an adapter and a DNS profile.");
        if(!Profile.Available)throw new DnsException(Profile.Warning);
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
        var profile=Profile;preview.Text=L.T("IPv6 settings are preserved unless explicitly selected.")+"\n"+L.T("Preserved IPv6 DNS can use a different provider.");
        if(profile!=null&&profile.Source!="")preview.Text+="\n"+L.T("Source checked: ")+profile.CheckedOn+" · "+profile.Source;
        if(profile?.Doh!=null)preview.Text+="\nDoH: "+profile.Doh+" · "+L.T("information only");
        warning.Text=L.T(profile?.Warning.Length>0?profile.Warning:snapshot?.CanChange==false?snapshot.Limitation:"DoH changes are not supported in this alpha.");SetEnabled();
    }
    void SetEnabled()
    {
        if(apply==null)return;bool valid=false;try{Plan();valid=true;}catch(DnsException){}
        apply.IsEnabled=!busy&&valid&&journal?.Active!=true;restore.IsEnabled=!busy&&snapshot?.CanChange==true&&journal?.Active==true&&DnsTransaction.Recoverable(journal,snapshot);refresh.IsEnabled=!busy;probe.IsEnabled=!busy&&snapshot!=null;adapters.IsEnabled=profiles.IsEnabled=ipv4.IsEnabled=ipv6.IsEnabled=addresses4.IsEnabled=addresses6.IsEnabled=endpoint.IsEnabled=!busy;
        if(window.FindName("DnsClose") is Button close)close.IsEnabled=!busy;
        if(Profile?.Available==true&&snapshot?.CanChange==true){if(journal?.Active==true)warning.Text=L.T(DnsTransaction.Recoverable(journal,snapshot)?"Restore the existing DNS transaction before applying another profile.":"DNS settings were changed outside Open Net. Restoration is blocked to preserve those changes.");else if(!valid)try{Plan();}catch(DnsException ex){warning.Text=L.T(ex.Message);}}
    }
    async Task Operation(bool restoring)
    {
        if(busy||snapshot==null)return;
        try{
            var before=snapshot;var plan=restoring?(journal!.Before,journal.Families):Plan();
            string message=L.T(restoring?"Restore the saved DNS configuration for this adapter?":"Apply the following DNS configuration to this adapter?")+"\n\n"+before.AdapterName+"\nIPv4: "+FamilyText(before.IPv4)+"\n→ "+FamilyText(plan.Item1.IPv4)+"\nIPv6: "+FamilyText(before.IPv6)+"\n→ "+FamilyText(plan.Item1.IPv6)+"\n\n"+L.T("The connection may be interrupted. Other adapters and network tools are not changed. A protected recovery record is saved before writing. Windows will request UAC permission.");
            bool confirmed;
#if OPENNET_TESTING
            if(shell.Runtime.TestMode&&ConfirmationForTests!=null)confirmed=ConfirmationForTests(message);else
#endif
            confirmed=AppDialog.Show(window,message,L.T("Open Net — DNS confirmation"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
            if(!confirmed)return;
            busy=true;SetEnabled();
#if OPENNET_TESTING
            if(shell.Runtime.TestMode){var transaction=new DnsTransaction(reader,(IDnsSettingsWriter)reader,store,"safe-test");if(restoring)transaction.Restore(Adapter,before.Fingerprint());else transaction.Apply(before,plan.Item1,plan.Item2);result.Text=L.T("Simulated DNS operation completed. System settings were not changed.");}
            else
#endif
            {
                using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);string token=Guid.NewGuid().ToString("N");
                var request=new DnsRequest(token,process.Id,identity.StartFileTime,DnsRecoveryStore.Owner(),shell.Settings.Language,restoring?"restore":"apply",before,plan.Item1,plan.Item2,before.Fingerprint());
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
        string message=L.T("Send a system DNS lookup and an unauthenticated HTTPS HEAD request to:")+"\n"+uri+"\n\n"+L.T("No cookies or account credentials are sent. Normal certificate validation is used. The system proxy and DNS cache may affect the result. This check is not bound to the selected adapter and does not prove that your account works.");
        if(AppDialog.Show(window,message,L.T("Open Net — connection check"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{busy=true;SetEnabled();var value=await probes.Run(uri,cancellation.Token);result.Text=value.Endpoint+"\nDNS: "+L.T(value.DnsResolved?"resolved":"failed")+" · "+L.T(value.DnsDetail)+"\nHTTPS: "+(value.HttpStatus.HasValue?"HTTP "+value.HttpStatus:"—")+" · "+L.T(value.HttpsDetail);}
        catch(Exception ex){result.Text=Error(ex);}finally{busy=false;SetEnabled();}
    }
    static string Error(Exception ex)=>ex is DnsException?L.T(ex.Message):L.Error(ex);
}
