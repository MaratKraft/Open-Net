using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetworkHub.App;

namespace NetworkHub.Desktop;
internal static class TestsSettingsCompact
{
    sealed class Tasks:IScheduledStartupStore
    {
        internal string? Xml;
        public string? Read(string name,string sid)=>Xml;
        public void Write(string name,string sid,string xml)=>Xml=xml;
        public void Delete(string name,string sid)=>Xml=null;
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static async Task Ui(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("033 settings status: ready disabled and foreign tasks keep diagnostics and footer accessible without changing registration",async()=>{
            var isolated=new AppFiles(files.Root,Path.Combine(files.Data,"behavior-status"));var settings=isolated.Defaults();settings.Language="ru";settings.Theme="dark";settings.Backdrop="solid";
            var tasks=new Tasks();var store=new WindowsStartupStore(true,new IsolatedStartupStore(),tasks,"S-1-5-21-1-2-3-1001");var service=new AutostartService(isolated,store,Environment.ProcessPath!);service.Configure(settings,()=>isolated.Save(settings));
            using var instance=new SingleInstance(isolated.Data,isolated.Log);var preview=new Shell(isolated,true,instance,null,null,store);preview.Window.Show();
            try{
                string good=tasks.Xml!;foreach(string state in new[]{"ready","disabled","foreign"}){
                    tasks.Xml=state=="ready"?good:state=="disabled"?good.Replace("<Enabled>true</Enabled>","<Enabled>false</Enabled>"):good.Replace("Open Net startup:","Foreign:");string before=tasks.Xml;
                    var view=preview.OpenSettings();try{view.UpdateLayout();await Task.Delay(80);string text=((TextBlock)view.FindName("StartupSummary")).Text;var repair=(Button)view.FindName("RepairStartup");
                        Check(state=="ready"?text.Contains(L.T("Windows startup is configured. Selected tools will start at logon.")):text==service.Inspect(preview.Settings).Message,"Status does not describe observed registration");
                        Check(repair.Visibility==(state=="disabled"?Visibility.Visible:Visibility.Collapsed),"Unsafe or missing repair action");
                        Check(tasks.Xml==before&&preview.Runtime.LaunchCount==0,"Opening settings changed task or started tools");
                        SelfTests.Capture(view,Path.Combine(files.Data,"033-behavior-"+state+".png"));
                    }finally{view.Close();}
                }
                string log=File.ReadAllText(Path.Combine(isolated.Data,"Logs/hub.log"));Check(log.Contains(L.T("Windows registration:"))&&log.Contains(L.T("Saved startup preference:"))&&log.Contains("Startup diagnostics:"),"Detailed diagnostics lost");
            }finally{preview.Dispose();preview.Window.Close();shell.ApplySettings(shell.Settings.Copy());}
        });
        await test("033 compact settings: approved layout, complete actions and diagnostics at three render scales in both languages/themes",async()=>{
            var saved=shell.Settings.Copy();
            try{foreach(string language in new[]{"ru","en"})foreach(string theme in new[]{"dark","light"})foreach(double scale in new[]{1d,1.25,1.5}){
                var next=saved.Copy();next.Language=language;next.Theme=theme;next.Backdrop="solid";shell.ApplySettings(next);var view=shell.OpenSettings();
                try{
                    // Simulated 1000-pixel available desktop height, without changing Windows DPI.
                    view.Height=Math.Min(730,1000/scale);view.UpdateLayout();await Task.Delay(80);
                    var scroll=(ScrollViewer)view.FindName("SettingsScroll");var surface=(FrameworkElement)view.Content;var appearance=(Grid)view.FindName("AppearanceFields");var languageField=(ComboBox)view.FindName("Language");var themeField=(ComboBox)view.FindName("Theme");var material=(ComboBox)view.FindName("Backdrop");
                    Point P(FrameworkElement e)=>e.TransformToAncestor(surface).Transform(new Point());
                    Check(P(languageField).Y<P(themeField).Y&&Math.Abs(P(themeField).Y-P(material).Y)<1&&P(themeField).X<P(material).X,"Approved field order");
                    var save=(Button)view.FindName("SaveSettings");var import=(Button)view.FindName("SettingsAdditional");
                    Check(save.ActualWidth>=136&&import.ActualWidth>0&&save.ActualHeight==import.ActualHeight&&Math.Abs(P(save).Y-P(import).Y)<1,"Unequal action geometry");
                    foreach(var button in new[]{save,import}){var text=new FormattedText(button.Content is string caption?caption:string.Join(" ",LocalizedTree.Walk((FrameworkElement)button.Content).OfType<TextBlock>().Select(t=>t.Text)),System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(button.FontFamily,button.FontStyle,button.FontWeight,button.FontStretch),button.FontSize,Brushes.White,scale);Check(text.Width+button.Padding.Left+button.Padding.Right<=button.ActualWidth,"Footer caption clipped");Check(P(button).Y+button.ActualHeight<=surface.ActualHeight&&P(button).Y>=0,"Footer outside viewport");}
                    if(view.Height>=730)Check(scroll.ScrollableHeight<1,"Standard settings unexpectedly require scrolling");
                    var toggles=new[]{"CloseToTray","ConfirmStartStop","StartWithWindows","AutoStartTools","StartupInTray"}.Select(name=>(CheckBox)view.FindName(name)).ToArray();Check(toggles.All(t=>Math.Abs(P(t).X-P(toggles[0]).X)<1)&&toggles.Skip(1).All(t=>t.Margin.Top==14)&&toggles[0].Margin.Top==0,"Toggle layout changed globally or nested");
                    var group=(StackPanel)view.FindName("BehaviorGroup");Check(group.Margin.Top==20&&group.Margin.Bottom==20,"Unequal group margins");Check(Math.Abs(P((TextBlock)view.FindName("StartupSummary")).Y-(P(toggles.Last()).Y+toggles.Last().ActualHeight)-20)<1,"Unequal trailing group gap");Check(view.FindName("StartupDetails")==null,"Unapproved details expander remains");Check(File.ReadAllText(Path.Combine(files.Data,"Logs/hub.log")).Contains(L.T("Saved startup preference:")),"Diagnostic data missing from journal");
                    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(files.Data,$"033-compact-{language}-{theme}-{scale*100}.png")))png.Save(output);

                    scroll.ScrollToEnd();view.UpdateLayout();var summary=(TextBlock)view.FindName("StartupSummary");Check(summary.TransformToAncestor(scroll).Transform(new Point()).Y+summary.ActualHeight<=scroll.ViewportHeight+1,"Summary unreachable on a constrained desktop");
                }finally{view.Close();}
            }}finally{shell.ApplySettings(saved);}
        });
    }
}
