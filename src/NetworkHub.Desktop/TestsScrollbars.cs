using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetworkHub.App;
namespace NetworkHub.Desktop;
internal static class TestsScrollbars
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static System.Collections.Generic.IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Children<T>(child))yield return nested;}
    }
    internal static async Task Ui(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("033 long licenses/log: rounded minimum thumbs fit both tracks at start middle end in both themes and three render scales",async()=>{
            string saved=ThemeManager.Mode;
            try{foreach(string theme in new[]{"dark","light"})foreach(string kind in new[]{"licenses","log"}){
                ThemeManager.SetMode(theme);string content=kind=="licenses"?Notices.Text():string.Join("\n",Enumerable.Repeat(new string('x',300),20000));var view=LogView.Create(shell.Window,content);view.Show();
                try{
                    await Task.Delay(120);var field=(TextBox)view.FindName("LogText");view.UpdateLayout();
                    foreach(double fraction in new[]{0d,0.5,1d}){
                        field.ScrollToVerticalOffset(field.ExtentHeight*fraction);field.ScrollToHorizontalOffset(field.ExtentWidth*fraction);view.UpdateLayout();await Task.Delay(40);
                        var bars=Children<ScrollBar>(field).Where(b=>b.IsVisible&&b.Maximum>0).ToArray();Check(bars.Any(b=>b.Orientation==Orientation.Vertical),"No vertical scrollbar fixture");
                        foreach(var bar in bars){bar.ApplyTemplate();var track=(Track)bar.Template.FindName("PART_Track",bar);var thumb=track.Thumb;thumb.ApplyTemplate();var grip=(Border)thumb.Template.FindName("Grip",thumb);bool vertical=bar.Orientation==Orientation.Vertical;double length=vertical?thumb.ActualHeight:thumb.ActualWidth;double painted=vertical?grip.ActualHeight:grip.ActualWidth;Point pos=thumb.TransformToAncestor(track).Transform(new Point());double offset=vertical?pos.Y:pos.X,available=vertical?track.ActualHeight:track.ActualWidth;
                            Check(length>=20-0.1&&painted>=16-0.1&&grip.CornerRadius.TopLeft==4,"Thumb is too short or loses rounding");Check(offset>=-0.1&&offset+length<=available+0.1,"Thumb clipped by its track");
                        }
                        if(fraction==1)Check(field.VerticalOffset>0,"Scrolling stopped working");
                        if(fraction==0)foreach(double scale in new[]{1d,1.25,1.5}){var image=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(Path.Combine(files.Data,$"033-scroll-{kind}-{theme}-{scale*100}.png"));png.Save(output);}
                    }
                    Check(field.IsReadOnly,"Read-only text changed");
                }finally{view.Close();}
            }}finally{ThemeManager.SetMode(saved);}
        });
    }
}
