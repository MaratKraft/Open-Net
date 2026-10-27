using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetworkHub.App;
using NetworkHub.Dns;
namespace NetworkHub.Desktop;
// One controller and one factual state feed both the workspace and this optional window.
internal sealed class SimpleDnsView
{
 readonly Shell shell;readonly AppFiles files;internal readonly HbDnsController Controller;readonly ComboBox profiles=new(){MinHeight=36},adapters=new(){MinHeight=36};readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap},message=new(){TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,12,0,0)};readonly Button power,cache,add;bool filling,busy;string inspectionError="";readonly DispatcherTimer transient=new(){Interval=TimeSpan.FromSeconds(5)};
 internal Window Window {get;}
 internal bool IsBusy=>busy;
 internal bool Applied=>inspectionError.Length==0&&Controller.Active!=null;
 internal string CurrentProfile=>inspectionError.Length==0?Controller.Active?.Name??"":"";
 internal string Status=>LineStatus;
 internal string LineStatus=>busy?L.T("Applying…"):inspectionError.Length>0?L.T("Error"):Controller.Current==null?L.T("Choose a connected adapter"):Applied?L.T("Running"):Controller.Current.IPv4.Mode==DnsMode.Automatic?L.T("Automatic DNS"):L.T("Manual DNS");
 internal string StatusTone=>inspectionError.Length>0?"Error":Applied?"Success":"Muted";
 internal DnsProfile? Selected=>profiles.SelectedItem is ComboBoxItem item?item.Tag as DnsProfile:null;
#if OPENNET_TESTING
 internal Func<string,bool>? ConfirmationForTests;
#endif
 internal SimpleDnsView(Shell shell,AppFiles files,HbDnsController controller)
 {
  this.shell=shell;this.files=files;Controller=controller;Window=SettingsLayout.Create(shell,L.T("DNS Manager — experiment"),490);var panel=SettingsLayout.Panel(Window);SettingsLayout.Heading(panel,"DNS Manager",L.T("Experimental · IPv4 DNS on one physical adapter."));
  SettingsLayout.Label(panel,"Network adapter");panel.Children.Add(adapters);Window.RegisterName("HbAdapter",adapters);SettingsLayout.Label(panel,"DNS profile");panel.Children.Add(profiles);Window.RegisterName("HbProfile",profiles);status.Margin=new Thickness(0,14,0,0);panel.Children.Add(status);panel.Children.Add(message);Window.RegisterName("HbStatus",status);Window.RegisterName("HbMessage",message);ThemeManager.Bind(message,TextBlock.ForegroundProperty,"Muted");
  var actions=SettingsLayout.ActionGrid(Window,panel,"HbActions");cache=SettingsLayout.Button(Window,"HbClearCache","Clear DNS cache");add=SettingsLayout.Button(Window,"HbAddDns","Add DNS");SettingsLayout.AddAction(actions,cache,0);SettingsLayout.AddAction(actions,add,1);cache.Click+=async(_,_)=>await ClearCache();add.Click+=(_,_)=>EditCustom();
  power=SettingsLayout.Finish(Window,"HbPower","Turn on");power.Click+=async(_,_)=>await Power();SettingsLayout.PinActions(Window,SettingsLayout.Footer(Window,power));
  Fill();adapters.SelectionChanged+=(_,_)=>{if(filling)return;shell.Settings.HbDnsAdapterId=(adapters.SelectedItem as ComboBoxItem)?.Tag is Guid id?id.ToString("D"):"";try{files.Save(shell.Settings);}catch(Exception ex){ShowError(ex);}RefreshStatus();shell.UpdateDnsStatus();};
  profiles.SelectionChanged+=async(_,e)=>{if(filling||Selected==null)return;if(busy){Fill();return;}bool apply=Applied;shell.Settings.HbDnsProfileId=Selected.Id;try{files.Save(shell.Settings);}catch(Exception ex){ShowError(ex);return;}if(apply){var previous=e.RemovedItems.OfType<ComboBoxItem>().FirstOrDefault()?.Tag as DnsProfile;if(!await Power(false)&&previous!=null){shell.Settings.HbDnsProfileId=previous.Id;files.Save(shell.Settings);Fill();}}else Render();};
  transient.Tick+=(_,_)=>{transient.Stop();message.Text="";};Window.Closed+=(_,_)=>transient.Stop();Window.Closing+=(_,e)=>{if(busy)e.Cancel=true;};RefreshStatus();LocalizedTree.Bind(Window);
 }
 internal void Fill()
 {
  filling=true;try{adapters.Items.Clear();var choices=Controller.Adapters;foreach(var a in choices)adapters.Items.Add(new ComboBoxItem{Content=a.Name+(a.Connected?"":" · "+L.T("Disconnected")),Tag=a.Id});var chosen=adapters.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>((Guid)i.Tag).ToString("D")==shell.Settings.HbDnsAdapterId);if(chosen==null&&shell.Settings.HbDnsAdapterId.Length==0&&choices.Count(a=>a.Connected)==1)chosen=adapters.Items.Cast<ComboBoxItem>().Single(i=>choices.Single(a=>a.Connected).Id==(Guid)i.Tag);adapters.SelectedItem=chosen;
   profiles.Items.Clear();foreach(var p in Controller.Profiles)profiles.Items.Add(new ComboBoxItem{Content=p.Name,Tag=p,ToolTip=p.Warning.Length==0?string.Join(", ",p.IPv4):L.T(p.Warning)});profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>((DnsProfile)i.Tag).Id==shell.Settings.HbDnsProfileId)??profiles.Items.Cast<ComboBoxItem>().First();
  }catch(Exception ex){ShowError(ex);}finally{filling=false;}
 }
 internal void RefreshStatus(){if(busy)return;try{Controller.Read();inspectionError="";}catch(Exception ex){inspectionError=L.Error(ex);files.Log("Experimental DNS inspection: "+ex);}Render();}
 void Render(){status.Text=(shell.Runtime.TestMode?L.T("Preview mode")+" · ":"")+LineStatus+(CurrentProfile.Length>0?" · "+CurrentProfile:"");ThemeManager.Bind(status,TextBlock.ForegroundProperty,StatusTone);power.Content=L.T(Applied?"Turn off":"Turn on");power.IsEnabled=!busy&&Controller.Current?.CanChange==true&&inspectionError.Length==0;power.ToolTip=Controller.Current?.CanChange==false?L.T(Controller.Current.Limitation):null;adapters.IsEnabled=profiles.IsEnabled=add.IsEnabled=!busy;cache.IsEnabled=!busy&&Controller.Current!=null;}
 bool Confirm(string text){
#if OPENNET_TESTING
 if(shell.Runtime.TestMode&&ConfirmationForTests!=null)return ConfirmationForTests(text);
#endif
 return AppDialog.Show(Window.IsVisible?Window:shell.Window,L.T(text),L.T("DNS Manager — experiment"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;}
 internal async Task<bool> Power(bool? restore=null)
 {
  if(busy||shell.Busy)return false;RefreshStatus();var before=Controller.Current;if(before==null||!before.CanChange||inspectionError.Length>0){ShowError(new DnsException(inspectionError.Length>0?inspectionError:before?.Limitation??"Choose a connected adapter"));return false;}bool off=restore??Applied;var profile=off?null:Selected;if(!off&&profile==null)return false;
  try{
   if(!shell.Settings.HbDnsPolicyAccepted){string notice="Turning off restores automatic IPv4 DNS, not earlier manual addresses. IPv6 is unchanged. An emergency snapshot is kept. Continue?";if(!Confirm(notice))return false;shell.Settings.HbDnsPolicyAccepted=true;files.Save(shell.Settings);}
   if(!off&&!profile!.Available)throw new DnsException(profile.Warning);if(!off&&profile!.Warning.Length>0&&!shell.Settings.HbDnsWarningsAccepted.Contains(profile.Id)){if(!Confirm(profile.Warning))return false;shell.Settings.HbDnsWarningsAccepted=shell.Settings.HbDnsWarningsAccepted.Append(profile.Id).ToArray();files.Save(shell.Settings);}
   bool allowPlain=false;DnsSnapshot target;try{target=SimpleDns.Plan(before,profile);}catch(DnsException)when(!off&&before.IPv4.Encryption.Any(p=>(p.Flags&3)!=0)&&profile!.Doh==null){if(!Confirm("This profile has no verified DoH endpoint. Apply unencrypted IPv4 DNS on this adapter?"))return false;allowPlain=true;target=SimpleDns.Plan(before,profile,true);}
   busy=true;Render();shell.UpdateDnsStatus();var response=await Controller.Change(before,target,profile,allowPlain);if(!response.Success)throw new DnsException(response.Message);Controller.Read();if(Controller.Current==null||!Controller.Current.SameConfiguration(target))throw new DnsException("DNS verification failed. The emergency snapshot has been kept.");files.Log("Experimental DNS: "+response.Message);message.Text=L.T(response.Message);transient.Start();return true;
  }catch(Exception ex){ShowError(ex);return false;}finally{busy=false;RefreshStatus();shell.UpdateDnsStatus();}
 }
 internal async Task ClearCache(){if(busy)return;RefreshStatus();var before=Controller.Current;if(before==null)return;try{busy=true;Render();var response=await Controller.Change(before,before,null,cacheOnly:true);if(!response.Success)throw new DnsException(response.Message);message.Text=L.T(response.Message);files.Log("Experimental DNS cache: "+response.Message);transient.Start();}catch(Exception ex){ShowError(ex);}finally{busy=false;RefreshStatus();shell.UpdateDnsStatus();}}
 void ShowError(Exception ex){transient.Stop();message.Text=ex is DnsException?L.T(ex.Message):L.Error(ex);files.Log("Experimental DNS: "+ex);shell.ShowMessage(message.Text);}
 internal void SelectProfile(string id){var item=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>((DnsProfile)i.Tag).Id==id);if(item!=null)profiles.SelectedItem=item;}
 internal void EditCustom()
 {
  if(busy)return;var selected=Selected;string? id=selected?.Id.StartsWith("hb-custom:")==true?selected.Id.Substring(10):null;var editor=SettingsLayout.Create(shell,L.T("Custom DNS"),480);editor.Owner=Window.IsVisible?Window:shell.Window;var panel=SettingsLayout.Panel(editor);SettingsLayout.Heading(panel,L.T("Custom DNS"),L.T("One preferred and an optional alternate IPv4 address."));
  TextBox Field(string name,string label,string value){SettingsLayout.Label(panel,label);var box=new TextBox{Text=value,Height=36,Padding=new Thickness(12,4,12,4)};editor.RegisterName(name,box);panel.Children.Add(box);return box;}
  var name=Field("HbCustomName","Name",id==null?"":selected!.Name);var first=Field("HbCustomFirst","Preferred IPv4 DNS",id==null?"":selected!.IPv4[0]);var second=Field("HbCustomSecond","Alternate IPv4 DNS",id==null?"":selected!.IPv4.Skip(1).FirstOrDefault()??"");var error=SettingsLayout.Error(editor,panel,"HbCustomError");
  var utilities=SettingsLayout.ActionGrid(editor,panel,"HbCustomActions");utilities.Visibility=id==null?Visibility.Collapsed:Visibility.Visible;var fresh=SettingsLayout.Button(editor,"HbCustomNew","New profile");fresh.Click+=(_,_)=>{id=null;utilities.Visibility=Visibility.Collapsed;name.Text=first.Text=second.Text="";};SettingsLayout.AddAction(utilities,fresh,0);var delete=SettingsLayout.Button(editor,"HbCustomDelete","Delete profile");delete.IsEnabled=id!=null;SettingsLayout.AddAction(utilities,delete,1);
  delete.Click+=(_,_)=>{if(id==null)return;if(Controller.Active?.Id=="hb-custom:"+id){error.Text=L.T("Turn off this custom DNS before deleting its profile.");return;}if(!Confirm("Delete the custom DNS profile? Windows settings will not change."))return;try{Controller.Delete(id);shell.Settings.HbDnsProfileId="hb:comss";files.Save(shell.Settings);Fill();editor.Close();RefreshStatus();}catch(Exception ex){error.Text=L.Error(ex);}};
  var save=SettingsLayout.Finish(editor,"HbCustomSave","Save");save.Click+=async(_,_)=>{if(busy)return;try{string profileId=id??Guid.NewGuid().ToString("N");var value=SimpleDns.Custom(profileId,name.Text,first.Text,second.Text);var active=Controller.Active;var previous=Controller.Profiles.FirstOrDefault(p=>p.Id=="hb-custom:"+profileId);Controller.Save(value);shell.Settings.HbDnsProfileId="hb-custom:"+profileId;files.Save(shell.Settings);Fill();if(active!=null&&!await Power(false)){if(previous!=null)Controller.Save(new SavedDnsProfile(profileId,previous.Name,previous.IPv4,Array.Empty<string>(),null,false,false));shell.Settings.HbDnsProfileId=active.Id;files.Save(shell.Settings);Fill();error.Text=L.T("DNS was not applied. Your previous profile remains selected.");return;}editor.Close();Render();}catch(Exception ex){error.Text=ex is DnsException?L.T(ex.Message):L.Error(ex);}};SettingsLayout.PinActions(editor,SettingsLayout.Footer(editor,save));ThemeManager.BindTree(editor);LocalizedTree.Bind(editor);editor.Show();
 }
}
