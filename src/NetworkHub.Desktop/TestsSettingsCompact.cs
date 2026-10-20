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
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static async Task Ui(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("033 compact settings: approved layout, complete actions and diagnostics at three render scales in both languages/themes",()=>{
            var saved=shell.Settings.Copy();
            try{foreach(string language in new[]{"ru","en"})foreach(string theme in new[]{"dark","light"})foreach(double scale in new[]{1d,1.25,1.5}){
                var next=saved.Copy();next.Language=language;next.Theme=theme;next.Backdrop="solid";shell.ApplySettings(next);var view=shell.OpenSettings();
                try{
                    // Simulated 1000-pixel available desktop height, without changing Windows DPI.
                    view.Height=Math.Min(730,1000/scale);view.UpdateLayout();
                    var scroll=(ScrollViewer)view.Content;var appearance=(Grid)view.FindName("AppearanceFields");var languageField=(ComboBox)view.FindName("Language");var themeField=(ComboBox)view.FindName("Theme");var material=(ComboBox)view.FindName("Backdrop");
                    Point P(FrameworkElement e)=>e.TransformToAncestor(scroll).Transform(new Point());
                    Check(P(languageField).Y<P(themeField).Y&&Math.Abs(P(themeField).Y-P(material).Y)<1&&P(themeField).X<P(material).X,"Approved field order");
                    var save=(Button)view.FindName("SaveSettings");var import=(Button)view.FindName("ImportSettings");
                    Check(save.ActualWidth==import.ActualWidth&&save.ActualHeight==import.ActualHeight&&Math.Abs(P(save).Y-P(import).Y)<1,"Unequal action geometry");
                    foreach(var button in new[]{save,import}){var text=new FormattedText(button.Content.ToString()!,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(button.FontFamily,button.FontStyle,button.FontWeight,button.FontStretch),button.FontSize,Brushes.White,scale);Check(text.Width+button.Padding.Left+button.Padding.Right<=button.ActualWidth,"Footer caption clipped");Check(P(button).Y+button.ActualHeight<=scroll.ViewportHeight&&P(button).Y>=0,"Footer outside viewport");}
                    Check(scroll.ScrollableHeight<1,"Collapsed settings unexpectedly require scrolling");
                    var toggles=new[]{"CloseToTray","ConfirmStartStop","StartWithWindows","AutoStartTools","StartupInTray"}.Select(name=>(CheckBox)view.FindName(name)).ToArray();Check(toggles.All(t=>Math.Abs(P(t).X-P(toggles[0]).X)<1&&t.Margin.Top==14),"Toggle layout changed globally or nested");
                    var details=(Expander)view.FindName("StartupDetails");Check(!details.IsExpanded,"Details expanded by default");
                    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(files.Data,$"033-compact-{language}-{theme}-{scale*100}.png")))png.Save(output);
                    details.IsExpanded=true;view.UpdateLayout();Check(((TextBlock)view.FindName("StartupStatus")).Text.Contains(L.T("Saved startup preference:")),"Detailed diagnostic data lost");scroll.ScrollToEnd();view.UpdateLayout();Check(P(save).Y+save.ActualHeight<=scroll.ViewportHeight+1,"Expanded details make Save unreachable");
                }finally{view.Close();}
            }}finally{shell.ApplySettings(saved);}return Task.CompletedTask;
        });
    }
}
