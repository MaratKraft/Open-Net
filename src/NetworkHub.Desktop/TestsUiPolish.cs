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
        await test("R3 themed popup text keyboard focus and secondary action fit",async()=>{
            var saved=shell.Settings.Copy();Window? settings=null;DnsManagerView? dns=null;Window? modules=null;
            try{foreach(string theme in new[]{"dark","light"}){
                var next=saved.Copy();next.Language="ru";next.Theme=theme;next.Backdrop="solid";next.Workspace.ManualHeight=false;next.Workspace.Visible=(string[])WorkspaceSettings.Modules.Clone();shell.ApplySettings(next);shell.Window.UpdateLayout();
                var entry=shell.Find<Button>("ModulesEntry");Check(entry.Background==ThemeManager.Brush("ButtonSurface")&&entry.ActualWidth>=104,"Modules is still a text link");
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

