#if OPENNET_TESTING
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NetworkHub.App;
namespace NetworkHub.Desktop;
internal static class TestsUiPolish
{
    static System.Collections.Generic.IEnumerable<DependencyObject> Visuals(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Visuals(VisualTreeHelper.GetChild(root,i)))yield return item;}
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void CapturePart(FrameworkElement part,string path)
    {
        // Nested visuals carry a parent offset; a VisualBrush captures their local bounds.
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(part){Stretch=Stretch.Fill},null,new Rect(0,0,part.ActualWidth,part.ActualHeight));
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(part.ActualWidth),(int)Math.Ceiling(part.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    internal static async Task Ui(Shell shell,NetworkHub.App.AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("C first shown window fits 1 2 3 modules without a second resize",async()=>{var saved=shell.Settings.Copy();try{foreach(int count in new[]{1,2,3}){var isolated=new AppFiles(Path.Combine(files.Root,"test-results/c-first-"+count+"-"+Guid.NewGuid().ToString("N")));var settings=saved.Copy();settings.Theme="dark";settings.Backdrop="solid";settings.Workspace.ManualHeight=false;settings.Workspace.Visible=WorkspaceSettings.Modules.Take(count).ToArray();isolated.Save(settings);using var instance=new SingleInstance(isolated.Data,isolated.Log);var fresh=new Shell(isolated,true,instance);fresh.Tray.Visible=false;try{fresh.Window.Show();await Task.Delay(100);await fresh.Refresh();fresh.Window.UpdateLayout();Check(fresh.Find<ScrollViewer>("MainScroll").ScrollableHeight<1,"First-open module overflow: "+count);TestsR2.Capture((FrameworkElement)fresh.Window.Content,Path.Combine(files.Data,"c-first-"+count+".png"));}finally{fresh.Dispose();fresh.Window.Close();Application.Current.Resources.MergedDictionaries.Remove(fresh.Window.Resources);}}}finally{shell.ApplySettings(saved);}});
        await test("C bounded second row preserves actions and pinned module footers",async()=>{
            var saved=shell.Settings.Copy();Window? module=null;
            try{foreach(string language in new[]{"ru","en"}){
                var next=saved.Copy();next.Language=language;next.Workspace.ManualHeight=false;next.Workspace.Visible=(string[])WorkspaceSettings.Modules.Clone();next.PendingStrategy="general (ALT2).bat";shell.ApplySettings(next);await shell.Refresh();shell.Window.UpdateLayout();
                var row=shell.Find<Border>("ZapretRow");var grid=(Grid)row.Child;var line=grid.Children.OfType<ModuleStatusLine>().Single();Check(Grid.GetRow(line)==1&&Grid.GetColumn(line)==1,"Profile escaped second row");
                int launches=shell.Runtime.LaunchCount;double height=shell.Window.Height;var actions=Visuals(row).OfType<ModuleActionGroup>().Single();double edge=actions.TranslatePoint(new Point(),row).X;
                line.Update("ALT13",new string('W',160),true);shell.Window.UpdateLayout();Check(actions.TranslatePoint(new Point(),row).X==edge&&shell.Window.Height==height,"Long profile moved actions or resized window");Check(line.Selector.ActualHeight==28&&row.ActualHeight>=70,"Compact geometry lost");var status=shell.Find<TextBlock>("ZapretState");Check(status.TranslatePoint(new Point(status.ActualWidth,0),line).X<=line.Selector.TranslatePoint(new Point(),line).X,"Status overlaps profile");Check(!shell.Find<TextBlock>("ZapretState").Text.Contains("PID"),"PID still in main status");Check(shell.Runtime.LaunchCount==launches,"Presentation launched tool");
                TestsR2.Capture((FrameworkElement)shell.Window.Content,Path.Combine(files.Data,"c-long-"+language+".png"));
            }
            module=shell.OpenModuleSettings("zapret");module.Height=450;module.UpdateLayout();var save=(Button)module.FindName("SaveModuleSettings");Check(!Visuals(module.Content as DependencyObject ?? module).OfType<ScrollViewer>().Any(v=>Visuals(v).Contains(save)),"Save still scrolls with content");Check(save.TranslatePoint(new Point(0,save.ActualHeight),module).Y<module.ActualHeight,"Save clipped at short height");module.Close();module=null;
            }finally{module?.Close();shell.ApplySettings(saved);}
        });
        await test("C profile selection does not restart the active owned stub",async()=>{var saved=shell.Settings.Copy();try{var directory=Path.Combine(files.Data,"c-list-only");Directory.CreateDirectory(directory);foreach(string name in new[]{"general (ALT13).bat","general (ALT2).bat"})File.WriteAllText(Path.Combine(directory,name),"listing only; never executed");var next=saved.Copy();next.Language="ru";next.Theme="dark";next.Backdrop="solid";next.Workspace.ManualHeight=false;next.Workspace.Visible=(string[])WorkspaceSettings.Modules.Clone();next.ZapretDirectory=directory;next.Strategy="general (ALT13).bat";next.PendingStrategy="";shell.ApplySettings(next);await shell.StartSelected(new[]{"zapret","tg"});shell.ShowMessage("");shell.Window.UpdateLayout();TestsR2.Capture((FrameworkElement)shell.Window.Content,Path.Combine(files.Data,"c-active-stubs.png"));int count=shell.Runtime.LaunchCount;var quick=shell.Find<Button>("ZapretQuick");quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var menu=quick.ContextMenu!;try{menu.Items.OfType<MenuItem>().Single(i=>i.Header as string=="general (ALT2)").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));}finally{menu.IsOpen=false;}shell.Window.UpdateLayout();var line=((Grid)shell.Find<Border>("ZapretRow").Child).Children.OfType<ModuleStatusLine>().Single();Check(shell.Runtime.ActiveZapretProfile=="general (ALT13).bat"&&line.ProfileText=="ALT13 → ALT2"&&count==shell.Runtime.LaunchCount,"Selection changed running strategy");TestsR2.Capture((FrameworkElement)shell.Window.Content,Path.Combine(files.Data,"c-pending-stubs.png"));}finally{await shell.StopSelected(true);shell.ApplySettings(saved);}});
        await test("R3 themed popup text keyboard focus and secondary action fit",async()=>{
            var saved=shell.Settings.Copy();Window? settings=null;DnsManagerView? dns=null;Window? modules=null;
            try{foreach(string theme in new[]{"dark","light"}){
                var next=saved.Copy();next.Language="ru";next.Theme=theme;next.Backdrop="solid";next.Workspace.ManualHeight=false;next.Workspace.Visible=(string[])WorkspaceSettings.Modules.Clone();shell.ApplySettings(next);shell.Window.UpdateLayout();
                var entry=shell.Find<Button>("ModulesEntry");Check(entry.Style==(Style)shell.Window.FindResource("QuietLink")&&entry.ActualHeight==28,"Modules footer lost quiet 28-DIP styling");
                TestsR2.Capture((FrameworkElement)shell.Window.Content,Path.Combine(files.Data,$"r3-main-{theme}.png"));CapturePart((FrameworkElement)entry.Parent,Path.Combine(files.Data,$"r3-footer-{theme}.png"));
                settings=shell.OpenSettings();settings.UpdateLayout();FocusCues.SetMode(true);foreach(var c in LocalizedTree.Walk(settings).OfType<Control>().Where(c=>c is not Window&&c is not ScrollViewer)){Check(c.FocusVisualStyle==null||c.FocusVisualStyle==(Style)settings.FindResource("KeyboardFocus"),"Native focus visual survived");}TestsR2.Capture((FrameworkElement)settings.Content,Path.Combine(files.Data,$"r3-settings-{theme}.png"));
                dns=new DnsManagerView(shell,files);dns.Window.Show();dns.SelectProfile("comss");dns.Window.UpdateLayout();var combo=(ComboBox)dns.Window.FindName("DnsProfile");combo.IsDropDownOpen=true;await Task.Delay(50);var popup=(Popup)combo.Template.FindName("PART_Popup",combo);var expected=((SolidColorBrush)ThemeManager.Brush("Text")).Color;var text=Visuals(popup.Child).OfType<TextBlock>().First(t=>t.Text=="ComssDNS");Check(((SolidColorBrush)text.Foreground).Color==expected,"Actual popup text ignored theme");TestsR2.Capture((FrameworkElement)popup.Child,Path.Combine(files.Data,$"r3-combo-{theme}.png"));combo.IsDropDownOpen=false;
                TestsR2.Capture((FrameworkElement)dns.Window.Content,Path.Combine(files.Data,$"r3-dns-{theme}.png"));var advanced=dns.OpenAdvanced();advanced.UpdateLayout();var other=(Button)dns.Window.FindName("DnsMore");Check(other.ActualHeight>=32&&other.Padding.Top<=4,"Other actions text squeezed");TestsR2.Capture((FrameworkElement)advanced.Content,Path.Combine(files.Data,$"r3-advanced-{theme}.png"));advanced.Close();dns.Window.Close();dns=null;
                shell.Workspace.OpenManager();modules=shell.Window.OwnedWindows.Cast<Window>().Single(w=>w.FindName("Visibledns")!=null);modules.UpdateLayout();TestsR2.Capture((FrameworkElement)modules.Content,Path.Combine(files.Data,$"r3-modules-{theme}.png"));var edit=LocalizedTree.Walk(modules).OfType<Button>().First(b=>b.Content as string==NetworkHub.App.L.T("Change order…"));edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));modules.UpdateLayout();var done=LocalizedTree.Walk(modules).OfType<Button>().First(b=>b.Content as string==NetworkHub.App.L.T("Save order"));var formatted=new FormattedText((string)done.Content,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(done.FontFamily,done.FontStyle,done.FontWeight,done.FontStretch),done.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(done).PixelsPerDip);Check(done.ActualWidth>=formatted.WidthIncludingTrailingWhitespace+done.Padding.Left+done.Padding.Right,"Save order label clipped");TestsR2.Capture((FrameworkElement)modules.Content,Path.Combine(files.Data,$"r3-order-{theme}.png"));modules.Close();modules=null;settings.Close();settings=null;
            }}finally{modules?.Close();dns?.Window.Close();settings?.Close();FocusCues.SetMode(false);shell.ApplySettings(saved);}
        });
    }
}
#endif

