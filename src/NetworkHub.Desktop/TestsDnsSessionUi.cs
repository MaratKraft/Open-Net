using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;
internal static class TestsDnsSessionUi
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("DNS UI automatic, profile switching, restart and original restoration",async()=>{
            var fake=TestsDns.Fixture();var original=fake.State;DnsManagerView.ReaderForTests=fake;
            try{var view=new DnsManagerView(shell,files);try{view.Window.Show();view.ConfirmationForTests=_=>true;
                ((CheckBox)view.Window.FindName("DnsClearCache")).IsChecked=false;
                ((Button)view.Window.FindName("DnsAutomatic")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(30);
                Check(fake.State.IPv4.Mode==DnsMode.Automatic&&fake.State.IPv6.SameConfiguration(original.IPv6),"Automatic click changed wrong family");
                foreach(int index in new[]{1,2}){((ComboBox)view.Window.FindName("DnsProfile")).SelectedIndex=index;((Button)view.Window.FindName("DnsApply")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(30);Check(fake.State.IPv4.Servers.SequenceEqual(DnsProfiles.All[index-1].IPv4),"Profile switch event failed");}
                var restarted=new DnsManagerView(shell,files);try{restarted.ConfirmationForTests=_=>true;await restarted.OperationForTests(true);Check(fake.State.SameConfiguration(original),"Restart did not restore first original");}finally{restarted.Window.Close();}
            }finally{view.Window.Close();}}finally{DnsManagerView.ReaderForTests=null;}
        });
        await test("DNS UI custom profile save reload and apply without system writes",async()=>{
            var fake=TestsDns.Fixture();var original=fake.State;DnsManagerView.ReaderForTests=fake;
            try{var view=new DnsManagerView(shell,files);try{view.ConfirmationForTests=_=>true;((ComboBox)view.Window.FindName("DnsProfile")).SelectedIndex=3;
                ((TextBox)view.Window.FindName("DnsCustomName")).Text="UI fixture custom";((TextBox)view.Window.FindName("DnsAddresses4")).Text="192.0.2.90";((TextBox)view.Window.FindName("DnsAlternate4")).Text="192.0.2.91";
                ((TextBox)view.Window.FindName("DnsDohEndpoint")).Text="https://dns.example/dns-query";((CheckBox)view.Window.FindName("DnsUseDoh")).IsChecked=true;
                ((Button)view.Window.FindName("DnsSaveProfile")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(fake.Writes==0,"Saving profile wrote DNS");
                await view.OperationForTests(false);Check(fake.State.IPv4.Servers.SequenceEqual(new[]{"192.0.2.90","192.0.2.91"})&&fake.State.IPv4.Encryption.All(p=>p.Flags==2),"Custom UI plan mismatch");await view.OperationForTests(true);Check(fake.State.SameConfiguration(original),"Custom original lost");
                var next=new DnsManagerView(shell,files);try{Check(((ComboBox)next.Window.FindName("DnsProfile")).Items.Cast<ComboBoxItem>().Any(p=>p.Tag is SavedDnsProfile s&&s.Name=="UI fixture custom"),"Saved profile disappeared");}finally{next.Window.Close();}
            }finally{view.Window.Close();}}finally{DnsManagerView.ReaderForTests=null;}
        });
        await test("DNS UI UAC write verification restore and cache failures remain isolated",async()=>{
            var fake=TestsDns.Fixture();var original=fake.State;DnsManagerView.ReaderForTests=fake;
            try{var view=new DnsManagerView(shell,files);try{view.ConfirmationForTests=_=>true;((ComboBox)view.Window.FindName("DnsProfile")).SelectedIndex=1;var failures=(ComboBox)view.Window.FindName("DnsSimulatedFailure");
                foreach(int failure in new[]{1,2,4}){failures.SelectedIndex=failure;await view.OperationForTests(false);Check(fake.State.SameConfiguration(original),"Failed apply did not preserve original: "+failure);Check(((TextBlock)view.Window.FindName("DnsResult")).Text.Length>0,"Missing failure result");}
                failures.SelectedIndex=5;await view.OperationForTests(false);var applied=fake.State;Check(applied.IPv4.Servers.SequenceEqual(DnsProfiles.All[0].IPv4),"Cache error rolled back valid DNS");
                failures.SelectedIndex=3;await view.OperationForTests(true);Check(fake.State.SameConfiguration(applied),"Failed restore reported an incorrect state");failures.SelectedIndex=0;await view.OperationForTests(true);Check(fake.State.SameConfiguration(original),"Restore could not resume");
                failures.SelectedIndex=5;await view.OperationForTests(false,false,true);Check(fake.State.SameConfiguration(original),"Cache-only error wrote DNS");
            }finally{view.Window.Close();}}finally{DnsManagerView.ReaderForTests=null;}
        });
    }
}
