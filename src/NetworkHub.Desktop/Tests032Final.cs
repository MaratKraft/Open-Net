using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using NetworkHub.App;
namespace NetworkHub.Desktop;

public static class Tests032Final
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static Rect Bounds(FrameworkElement item,Visual relative)=>item.TransformToAncestor(relative).TransformBounds(new Rect(item.RenderSize));
 static double Luminance(Brush brush){var c=((SolidColorBrush)brush).Color;double Part(byte v){double s=v/255.0;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}return .2126*Part(c.R)+.7152*Part(c.G)+.0722*Part(c.B);}
 public static void Geometry(Shell shell)
 {
  shell.Window.UpdateLayout();
  foreach(string prefix in new[]{"Zapret","Tg"})
  {
   var controls=new FrameworkElement[]{shell.Find<Button>(prefix+"Install"),shell.Find<Button>(prefix+"Settings"),shell.Find<CheckBox>(prefix+"Check")};
   var rects=controls.Select(c=>Bounds(c,(Visual)shell.Window.Content)).ToArray();
   Check(rects.All(r=>Math.Abs(r.Width-rects[0].Width)<.1&&Math.Abs(r.Height-rects[0].Height)<.1),"Equal hit areas");
   Check(Math.Abs((rects[1].Left-rects[0].Right)-(rects[2].Left-rects[1].Right))<.1&&rects[1].Left>=rects[0].Right&&rects[2].Left>=rects[1].Right,"Equal gaps, no overlap");
   Check(rects.All(r=>Math.Abs(r.Top+r.Height/2-rects[0].Top-rects[0].Height/2)<.1),"Vertical alignment");
   var selection=(CheckBox)controls[2];selection.ApplyTemplate();var box=(Border)selection.Template.FindName("Box",selection);var circle=Bounds(box,(Visual)shell.Window.Content);
   Check(Math.Abs(circle.Left+circle.Width/2-rects[2].Left-rects[2].Width/2)<.1,"Selection optically centered");
   Check(VisualTreeHelper.HitTest(selection,new Point(1,selection.ActualHeight/2))!=null&&VisualTreeHelper.HitTest(selection,new Point(selection.ActualWidth-1,selection.ActualHeight/2))!=null,"Selection visual hits use the full shared area");
   if(shell.Window.IsVisible)Check(selection.InputHitTest(new Point(1,selection.ActualHeight/2))!=null&&selection.InputHitTest(new Point(selection.ActualWidth-1,selection.ActualHeight/2))!=null,"Selection input hits use the full shared area");
   var glyph=shell.Find<System.Windows.Shapes.Path>(prefix+"InstallGlyph");var original=glyph.Data;
   try{foreach(bool installed in new[]{false,true})
   {
    glyph.Data=GeometryParse(installed);shell.Window.UpdateLayout();ModuleIconAlignment.Update((Button)controls[0],(Button)controls[1],selection);shell.Window.UpdateLayout();
    var gear=(System.Windows.Shapes.Path)((Button)controls[1]).Content;var left=ModuleIconAlignment.Ink(glyph,(Visual)shell.Window.Content);var middle=ModuleIconAlignment.Ink(gear,(Visual)shell.Window.Content);
    Check(Math.Abs((middle.Left-left.Right)-(circle.Left-middle.Right))<.1,"Gear is centered between visible icon edges for both glyphs");
   }}finally{glyph.Data=original;shell.Window.UpdateLayout();ModuleIconAlignment.Update((Button)controls[0],(Button)controls[1],selection);}
  }
 }
 public static void Toggles(Shell shell)
 {
  foreach(bool enabled in new[]{true,false})foreach(bool on in new[]{false,true})foreach(string state in new[]{"normal","hover","pressed"})
  {
   var toggle=new CheckBox{Style=(Style)shell.Window.Resources["OptionToggle"],IsChecked=on,IsEnabled=enabled};
   toggle.Measure(new Size(200,40));toggle.Arrange(new Rect(0,0,200,40));toggle.ApplyTemplate();toggle.UpdateLayout();
   if(state=="hover")Force(toggle,typeof(UIElement),"IsMouseOverPropertyKey");if(state=="pressed")Force(toggle,typeof(System.Windows.Controls.Primitives.ButtonBase),"IsPressedPropertyKey");toggle.UpdateLayout();
   var track=(Border)toggle.Template.FindName("Track",toggle);var thumb=(System.Windows.Shapes.Ellipse)toggle.Template.FindName("Thumb",toggle);
   var a=Bounds(track,toggle);var b=Bounds(thumb,toggle);
   double diameter=enabled&&state=="hover"?16:enabled&&state=="pressed"?12:14,inset=(22-diameter)/2;
   Check(a.Width==42&&a.Height==22&&b.Width==diameter&&b.Height==diameter&&track.BorderThickness==new Thickness(1),"Stable track and thumb");
   Check(Math.Abs(b.Top-a.Top-inset)<.1&&Math.Abs(a.Bottom-b.Bottom-inset)<.1,"Balanced vertical inset");
   Check(Math.Abs(on?a.Right-b.Right-inset:b.Left-a.Left-inset)<.1,"State matches thumb position");
   Check(thumb.HorizontalAlignment==(on?HorizontalAlignment.Right:HorizontalAlignment.Left),"Checked value drives position");
   double x=Luminance(track.Background),y=Luminance(thumb.Fill);Check((Math.Max(x,y)+.05)/(Math.Min(x,y)+.05)>=3,"Visible control contrast");
  }
 }
 public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
 {
  await test("032 final equal action spacing and centered toggles",()=>{Geometry(shell);Toggles(shell);return Task.CompletedTask;});
  await test("032 final logo activation, localization, safe URL and browser failure",async()=>{
   string language=L.Language;int invoked=0;
   try
   {
    var button=shell.Find<Button>("LogoLink");Check(button.Width==26&&button.Height==26&&button.Cursor==Cursors.Hand&&button.Focusable,"Logo-only accessible area");
    shell.RepositoryLaunchForTests=plan=>{Check(plan.FileName==RepositoryLink.Address&&plan.UseShellExecute&&plan.Verb=="open"&&plan.Arguments=="","Fixed HTTPS shell plan");invoked++;};
    foreach(string locale in new[]{"en","ru"}){L.Set(locale);Check(button.ToolTip?.ToString()==L.T("Open Open Net on GitHub"),"Localized tooltip");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    var peer=new ButtonAutomationPeer(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();await Task.Delay(30);Check(invoked==3,"Button invocation reaches launcher");
    TestActivation.Own(shell.Window);button.Focus();FocusCues.SetMode(true);
    foreach(var key in new[]{Key.Enter,Key.Space})
    {
     var source=PresentationSource.FromVisual(button)!;
     button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.KeyDownEvent});
     button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.KeyUpEvent});
    }
    Check(invoked==5,"Enter and Space routed keyboard activation");
    shell.RepositoryLaunchForTests=_=>throw new InvalidOperationException("simulated unavailable browser");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(shell.Find<TextBlock>("Message").Text==L.T("Could not open the browser. Open github.com/MaratKraft/Open-Net manually."),"Browser failure is readable and nonfatal");
   }finally{shell.RepositoryLaunchForTests=null;L.Set(language);}
  });
  await test("032 final visual matrix at four rendered DPI scales",async()=>{
   var saved=shell.Settings.Copy();
   try{foreach(string mode in new[]{"dark","light"})
   {
    var next=saved.Copy();next.Theme=mode;shell.ApplySettings(next);
    var preview=new StackPanel{Background=ThemeManager.Brush("Surface"),Margin=new Thickness(8)};
    foreach(bool installed in new[]{false,true})foreach(string prefix in new[]{"Zapret","Tg"})
    {
     var group=new ModuleActionGroup{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(8)};
     var glyph=new System.Windows.Shapes.Path{Data=GeometryParse(installed),Fill=ThemeManager.Brush("Text"),Width=18,Height=18,Stretch=Stretch.Fill};
     var installContent=new Grid{Width=18,Height=18};installContent.Children.Add(glyph);
     group.Children.Add(new Button{Style=(Style)shell.Window.Resources["ModuleAction"],Content=installContent});
     group.Children.Add(new Button{Style=(Style)shell.Window.Resources["ModuleAction"],Content=new System.Windows.Shapes.Path{Data=(System.Windows.Media.Geometry)shell.Window.Resources["GearGeometry"],Fill=ThemeManager.Brush("Text"),Stretch=Stretch.Fill,Width=18,Height=18}});
     group.Children.Add(new CheckBox{Style=(Style)shell.Window.Resources["ModuleSelection"],IsChecked=prefix=="Zapret"});preview.Children.Add(group);
     ModuleIconAlignment.Attach((Button)group.Children[0],(Button)group.Children[1],(CheckBox)group.Children[2]);
     foreach(DependencyObject action in group.Children)if(prefix=="Tg")Force(action,typeof(UIElement),"IsMouseOverPropertyKey");
    }
    // Render controls from their actual shared templates, including hover/pressed/disabled values.
    foreach(bool on in new[]{false,true})foreach(string state in new[]{"normal","hover","pressed","disabled"})
    {
     var c=new CheckBox{Content=state,Style=(Style)shell.Window.Resources["OptionToggle"],IsChecked=on,IsEnabled=state!="disabled",Margin=new Thickness(8)};preview.Children.Add(c);c.ApplyTemplate();
     if(state=="hover")Force(c,typeof(UIElement),"IsMouseOverPropertyKey");
     if(state=="pressed")Force(c,typeof(System.Windows.Controls.Primitives.ButtonBase),"IsPressedPropertyKey");
    }
    preview.Measure(new Size(420,600));preview.Arrange(new Rect(new Size(420,preview.DesiredSize.Height)));preview.UpdateLayout();
    foreach(var group in preview.Children.OfType<ModuleActionGroup>())ModuleIconAlignment.Update((Button)group.Children[0],(Button)group.Children[1],(CheckBox)group.Children[2]);
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})Render(preview,Path.Combine(files.Data,$"032-final-toggles-{mode}-{scale}.png"),scale);
    Geometry(shell);Toggles(shell);SelfTests.Capture(shell.Window,Path.Combine(files.Data,$"032-final-main-{mode}.png"));
    var general=shell.OpenSettings();
    try{TestActivation.Own(general);FocusCues.SetMode(true);var toggle=(CheckBox)general.FindName("CloseToTray");toggle.Focus();await Task.Delay(50);var track=(Border)toggle.Template.FindName("Track",toggle);Check(track.BorderThickness==new Thickness(1),"Keyboard focus does not alter track geometry");SelfTests.Capture(general,Path.Combine(files.Data,$"032-final-focus-{mode}.png"));}finally{general.Close();FocusCues.SetMode(false);}
   }}finally{shell.ApplySettings(saved);}
  });
 }
 static System.Windows.Media.Geometry GeometryParse(bool installed)=>System.Windows.Media.Geometry.Parse(ModuleActionPresentation.Glyph(installed));
 // Test-only synthetic visual state; not a physical mouse/monitor test.
 static void Force(DependencyObject item,Type owner,string name){var field=owner.GetField(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;item.SetValue((DependencyPropertyKey)field.GetValue(null)!,true);}
 static void Render(FrameworkElement element,string path,double scale){element.Measure(new Size(420,600));element.Arrange(new Rect(new Size(420,element.DesiredSize.Height)));element.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);}
}
