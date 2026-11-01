#if OPENNET_TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
namespace NetworkHub.Desktop;
internal static class TestsPre3
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    internal static async Task Ui(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
    {
        await test("Pre3 WPF proposals preserve production glyph and hit area",()=>{
            var saved=shell.Settings.Copy();var styled=saved.Copy();styled.Theme="dark";styled.Backdrop="solid";shell.ApplySettings(styled);
            var gear=shell.Find<System.Windows.Shapes.Path>("SettingsGear");var approved=gear.Data;var approvedTransform=gear.RenderTransform;var original=(System.Windows.Media.Geometry)shell.Window.FindResource("GearGeometry");
            var module=(System.Windows.Shapes.Path)shell.Find<Button>("ZapretSettings").Content;var transform=module.RenderTransform;
            try{
                foreach(string theme in new[]{"dark","light"}){var capture=styled.Copy();capture.Theme=theme;shell.ApplySettings(capture);SelfTests.Capture(shell.Window,Path.Combine(files.Data,"pre3-approved-"+theme+".png"));}shell.ApplySettings(styled);
                string data=original.ToString(System.Globalization.CultureInfo.InvariantCulture);int end=data.IndexOf('Z');if(end<0)end=data.IndexOf('z');
                var filled=new System.Windows.Media.GeometryGroup{FillRule=System.Windows.Media.FillRule.EvenOdd};filled.Children.Add(System.Windows.Media.Geometry.Parse(data.Substring(0,end+1)));filled.Children.Add(new System.Windows.Media.EllipseGeometry(new Point(10,10),2.5,2.5));
                foreach(string variant in new[]{"A","B","C"}){
                    gear.Data=variant=="A"?original:filled;gear.RenderTransform=variant=="C"?new System.Windows.Media.ScaleTransform(.94,.94,10,10):System.Windows.Media.Transform.Identity;
                    shell.Window.UpdateLayout();SelfTests.Capture(shell.Window,Path.Combine(files.Data,"pre3-general-gear-"+variant+".png"));
                }
                var preview=new StackPanel{Margin=new Thickness(20),Background=ThemeManager.Brush("Surface")};
                foreach(bool corrected in new[]{false,true}){
                    var clone=new Button{Style=(Style)shell.Window.FindResource("ModuleAction"),Width=28,Height=28,Background=ThemeManager.Brush("Hover"),Content=new System.Windows.Shapes.Path{Data=module.Data,Style=(Style)shell.Window.FindResource("IconGear"),RenderTransform=corrected?System.Windows.Media.Transform.Identity:new System.Windows.Media.TranslateTransform(-1,0)}};clone.Measure(new Size(28,28));clone.Arrange(new Rect(0,0,28,28));clone.UpdateLayout();
                    preview.Children.Add(new TextBlock{Text=corrected?"Proposal: glyph centred in existing 28 DIP area":"Current: glyph shifted inside existing 28 DIP area",Foreground=ThemeManager.Brush("Text"),Margin=new Thickness(0,12,0,8)});
                    var image=new System.Windows.Controls.Image{Width=280,Height=280,Stretch=System.Windows.Media.Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left};
                    var drawing=new System.Windows.Media.DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new System.Windows.Media.VisualBrush(clone) {Stretch=System.Windows.Media.Stretch.Fill},null,new Rect(0,0,28,28));
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(280,280,960,960,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(drawing);image.Source=bitmap;preview.Children.Add(image);
                }
                var window=new Window{Content=preview,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.ToolWindow,ShowInTaskbar=false};try{window.Show();SelfTests.Capture(window,Path.Combine(files.Data,"pre3-module-gear-centering.png"));}finally{window.Close();}
                return Task.CompletedTask;
            }finally{gear.Data=approved;gear.RenderTransform=approvedTransform;module.RenderTransform=transform;shell.Find<Button>("ZapretSettings").ClearValue(Control.BackgroundProperty);shell.ApplySettings(saved);}
        });
        await test("Pre3 stopped profile selection clears stale pending strategy before first launch",async()=>{
            var saved=shell.Settings.Copy();
            try{
                await shell.StopSelected(true);
                string directory=Path.Combine(files.Data,"pre3-profile-listing");Directory.CreateDirectory(directory);
                foreach(string name in new[]{"general (ALT13).bat","general.bat"})File.WriteAllText(Path.Combine(directory,name),"listing only; never executed");
                var next=saved.Copy();next.ConfirmStartStop=false;next.ZapretDirectory=directory;next.Strategy="general.bat";next.PendingStrategy="general.bat";shell.ApplySettings(next);
                var quick=shell.Find<Button>("ZapretQuick");quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var menu=quick.ContextMenu!;
                try{menu.Items.OfType<MenuItem>().Single(i=>i.Header as string=="general (ALT13)").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));}finally{menu.IsOpen=false;}
                Check(shell.Settings.Strategy=="general (ALT13).bat"&&shell.Settings.PendingStrategy==""&&files.Load().PendingStrategy=="","Old pending profile overrides new selection");
                await shell.StartSelected(new[]{"zapret"});Check(shell.Runtime.ActiveZapretProfile=="general (ALT13).bat","First start used a different profile");
                await shell.StopSelected(false,new[]{"zapret"});await shell.StartSelected(new[]{"zapret"});Check(shell.Runtime.ActiveZapretProfile=="general (ALT13).bat","Repeat launch used a different profile");
            }finally{await shell.StopSelected(true);shell.ApplySettings(saved);}
        });

    }
}
#endif
