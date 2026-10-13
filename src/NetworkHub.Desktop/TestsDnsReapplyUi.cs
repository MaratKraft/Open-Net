using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
using NetworkHub.Core;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;
internal static class TestsDnsReapplyUi
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static AppFiles Isolated(AppFiles files)=>new(files.Root,Paths.Owned(files.Data,"Repeat-ui/"+Guid.NewGuid().ToString("N")));
    internal static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("DNS repeat UI: compact menu actual repeat events preserve Google and independent probes",async()=>{
            var data=Isolated(files);var fake=TestsDns.Fixture();fake.State=fake.State with{IPv4=new(DnsMode.Manual,new[]{"8.8.8.8","8.8.4.4"},Array.Empty<string>())};var google=fake.State;DnsManagerView.ReaderForTests=fake;
            try{var view=new DnsManagerView(shell,data);try{view.Window.Show();view.ConfirmationForTests=_=>true;var profile=(ComboBox)view.Window.FindName("DnsProfile");profile.SelectedIndex=1;var repeat=(MenuItem)view.Window.FindName("DnsReapplyMenu");Check(!repeat.IsEnabled,"Different profile enabled repeat");await view.OperationForTests(false);Check(repeat.IsEnabled,"Matching profile repeat disabled");((ComboBox)view.Window.FindName("DnsSimulatedFailure")).SelectedIndex=5;
                ((Button)view.Window.FindName("DnsMore")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((Button)view.Window.FindName("DnsMore")).ContextMenu.IsOpen,"Additional actions did not open");((Button)view.Window.FindName("DnsMore")).ContextMenu.IsOpen=false;
                string originalCopy=Directory.GetFiles(Path.Combine(data.Data,"Dns/Backups"),"*.original.json").Single();string originalBytes=File.ReadAllText(originalCopy);
                for(int i=0;i<5;i++){repeat.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(25);}Check(File.ReadAllText(originalCopy)==originalBytes,"The original readable backup was overwritten");Check(fake.Writes==6,"Repeat did not write or cleared cache failure stopped it");var receipts=Directory.GetFiles(Path.Combine(data.Data,"Dns/OperationReceipts"),"*.json").Select(p=>JsonDocument.Parse(File.ReadAllText(p))).ToArray();try{var repeated=receipts.Where(d=>d.RootElement.GetProperty("Evidence").GetProperty("Action").GetString()=="reapply").ToArray();Check(repeated.Length==5&&repeated.All(d=>d.RootElement.GetProperty("Evidence").GetProperty("CacheResult").GetString()=="not performed"&&d.RootElement.GetProperty("Evidence").GetProperty("ValuesChanged").GetBoolean()==false),"Cache or network claims were not independent");}finally{foreach(var d in receipts)d.Dispose();}
                string evidence=((TextBlock)view.Window.FindName("DnsOperationEvidence")).Text;((ComboBox)view.Window.FindName("DnsProbeMode")).SelectedIndex=1;await view.ProbeForTests();Check(((TextBlock)view.Window.FindName("DnsOperationEvidence")).Text==evidence,"Lookup overwrote write evidence");((ComboBox)view.Window.FindName("DnsProbeMode")).SelectedIndex=2;await view.ProbeForTests();Check(((TextBlock)view.Window.FindName("DnsProbeEvidence")).Text.Contains("403"),"HTTP refusal lost");
                foreach(string mode in new[]{"dark","light"}){ThemeManager.SetMode(mode);view.Window.UpdateLayout();SelfTests.Capture(view.Window,Path.Combine(data.Data,"repeat-"+mode+".png"));}
                var restarted=new DnsManagerView(shell,data);try{restarted.ConfirmationForTests=_=>true;await restarted.OperationForTests(true);Check(fake.State.SameConfiguration(google),"Stale Xbox restored instead of Google");}finally{restarted.Window.Close();}
            }finally{view.Window.Close();}}finally{DnsManagerView.ReaderForTests=null;}
        });
        await test("DNS repeat UI: cancellation UAC write and verification failures leave original recoverable",async()=>{
            var fake=TestsDns.Fixture();fake.State=fake.State with{IPv4=new(DnsMode.Manual,DnsProfiles.All[0].IPv4,DnsProfiles.All[0].IPv4)};var original=fake.State;var data=Isolated(files);DnsManagerView.ReaderForTests=fake;
            try{var view=new DnsManagerView(shell,data);try{((ComboBox)view.Window.FindName("DnsProfile")).SelectedIndex=1;((CheckBox)view.Window.FindName("DnsUseDoh")).IsChecked=false;view.ConfirmationForTests=_=>false;await view.OperationForTests(false,false,false,true);Check(fake.Writes==0,"Cancellation wrote DNS");view.ConfirmationForTests=_=>true;
                foreach(int error in new[]{1,2,4}){((ComboBox)view.Window.FindName("DnsSimulatedFailure")).SelectedIndex=error;await view.OperationForTests(false,false,false,true);Check(fake.State.SameConfiguration(original),"Repeat error changed baseline");Check(((TextBlock)view.Window.FindName("DnsResult")).Text.Length>0,"Error not visible");}
                ((ComboBox)view.Window.FindName("DnsSimulatedFailure")).SelectedIndex=0;await view.OperationForTests(false,false,false,true);Check(((MenuItem)view.Window.FindName("DnsReapplyMenu")).IsEnabled,"Repeat could not resume");await view.OperationForTests(true);Check(fake.State.SameConfiguration(original),"External plain original lost");
            }finally{view.Window.Close();}}finally{DnsManagerView.ReaderForTests=null;}
        });
    }
}
