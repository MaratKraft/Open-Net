using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;

// Independent of Shell, MainView, user settings, modules, GitHub and connection probes.
internal static class DnsEmergencyRecovery
{
    internal static int Run(string[] args)
    {
        if(args.Length>2||args.Length==2&&args[1]!="--verify-only")return 2;
        string root=Path.GetDirectoryName(Environment.ProcessPath!)!;
        L.Set("ru");
        var files=new AppFiles(root,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Open Net HB Experiment","RecoveryRequests",Guid.NewGuid().ToString("N")));
        string owner=DnsRecoveryStore.Owner();var reader=new WindowsDnsSettings();var store=DnsRecoveryStore.Open(owner,false);
        var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};L.Set("ru");ThemeManager.SetMode("system");
        var window=new Window{Title=L.T("Open Net — offline DNS recovery"),Width=560,Height=510,MinWidth=440,MinHeight=380,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        ThemeManager.Bind(window,Window.BackgroundProperty,"Surface");ThemeManager.Bind(window,Window.ForegroundProperty,"Text");
        var panel=new StackPanel{Margin=new Thickness(24)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text=L.T("Offline DNS recovery"),FontSize=22,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text=L.T("Only this machine's protected original DNS record is accepted. No internet connection is required."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,14)});
        var choice=new ComboBox{Height=34};panel.Children.Add(choice);var detail=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,14)};panel.Children.Add(detail);
        var restore=new Button{Content=L.T("Restore original DNS…"),Height=36,IsEnabled=false};panel.Children.Add(restore);bool busy=false;DnsSnapshot? current=null;DnsJournal? journal=null;
        void Inspect(){try{var id=choice.SelectedItem is ComboBoxItem item&&item.Tag is Guid guid?guid:Guid.Empty;journal=store.Load(id);current=reader.Read(id);restore.IsEnabled=journal?.Active==true&&current.CanChange&&DnsTransaction.Recoverable(journal,current);detail.Text=L.T("Current DNS:")+"\n"+Describe(current)+"\n\n"+L.T("Saved original DNS:")+"\n"+(journal==null?L.T("No protected DNS backup exists for this adapter."):Describe(journal.Before));if(!restore.IsEnabled)detail.Text+="\n"+L.T(current.CanChange?"Restoration is unavailable or an external change was detected.":current.Limitation);}catch(Exception ex){restore.IsEnabled=false;detail.Text=L.Error(ex);}}
        foreach(var adapter in reader.Adapters())try{if(store.Load(adapter.Id)?.Active==true)choice.Items.Add(new ComboBoxItem{Content=adapter.Name,Tag=adapter.Id});}catch(Exception ex){detail.Text=L.Error(ex);}
        choice.SelectionChanged+=(_,_)=>Inspect();if(choice.Items.Count>0)choice.SelectedIndex=0;else detail.Text=L.T("No protected DNS backup exists for this adapter. A read-only diagnostic copy cannot authorize privileged restoration.");
        restore.Click+=async(_,_)=>{if(busy||current==null||journal==null)return;Inspect();if(!restore.IsEnabled)return;
            string message=L.T("Restore only the saved original DNS configuration for this adapter?")+"\n\n"+Describe(journal.Before)+"\n\n"+L.T("Windows will request UAC. Other adapters, IPv6 protocol, IP addressing and running tools are unchanged.");
            if(MessageBox.Show(window,message,window.Title,MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            try{busy=true;restore.IsEnabled=choice.IsEnabled=false;using var process=System.Diagnostics.Process.GetCurrentProcess();var identity=ProcessIdentity.Read(process.Id);var request=new DnsRequest(Guid.NewGuid().ToString("N"),process.Id,identity.StartFileTime,owner,"ru","restore",current,journal.Before,journal.Families,current.Fingerprint()){ClearCache=true};var response=await DnsElevated.Execute(files,request,false);Inspect();detail.Text+="\n\n"+response.Message;}catch(Exception ex){detail.Text=L.Error(ex);}finally{busy=false;choice.IsEnabled=true;}}
        ;window.Closing+=(_,e)=>{if(busy)e.Cancel=true;};
        if(args.Length==2)window.ContentRendered+=(_,_)=>{string output=Path.Combine(root,"test-results","recovery-probe.json");NetworkHub.Core.Paths.NoReparseAncestors(output);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(new{Visible=window.IsVisible,NetworkRequired=false,ProtectedRecords=choice.Items.Count,RestoreEnabled=restore.IsEnabled,VerifiedOnly=true,Current=current,ProtectedJournal=journal},new JsonSerializerOptions{WriteIndented=true}));window.Close();};
        return app.Run(window);
    }
    internal static string Describe(DnsSnapshot value)=>value.AdapterName+"\nIPv4: "+value.IPv4.Mode+" · "+string.Join(", ",value.IPv4.Servers)+"\n"+string.Join("\n",value.IPv4.Encryption.Select(p=>p.Server+" · "+(p.Template??"automatic template")+" · flags="+p.Flags))+"\nIPv6: "+value.IPv6.Mode+" · "+string.Join(", ",value.IPv6.Servers);
}
