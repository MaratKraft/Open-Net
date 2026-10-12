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

public static class TestsFluent
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Force(DependencyObject item,Type owner,string name,bool value=true){var key=(DependencyPropertyKey)owner.GetField(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;item.SetValue(key,value);}
 public static void Geometry(Shell shell)
 {
  foreach(string prefix in new[]{"Zapret","Tg"})
  {
   var controls=new Control[]{shell.Find<Button>(prefix+"Install"),shell.Find<Button>(prefix+"Settings"),shell.Find<CheckBox>(prefix+"Check")};
   foreach(var c in controls){c.ApplyTemplate();var surface=(Border)c.Template.FindName("IconSurface",c);Check(c.Width==28&&c.Height==28&&surface.CornerRadius.TopLeft==6&&FocusCues.GetOwnFocusIndicator(c),"Shared icon control geometry");Check(surface.ActualWidth==28&&surface.ActualHeight==28,"Equal hover surfaces");}
   var glyph=shell.Find<System.Windows.Shapes.Path>(prefix+"InstallGlyph");Check(glyph.Stroke!=null&&glyph.Fill==null&&glyph.StrokeThickness==1.4&&glyph.Width==18&&glyph.Height==18,"Approved rounded contour uses the fixed viewport");
   var group=(Visual)controls[0].Parent;
   Check(ModuleIconAlignment.Ink(glyph,group).Width>=10&&ModuleIconAlignment.Ink(glyph,group).Width<15.5,"Compact arrow ink stays inside the unchanged hit area");
   Check(controls.All(c=>c.Foreground==ThemeManager.Brush("ActionForeground"))&&glyph.Stroke==ThemeManager.Brush("ActionForeground"),"All enabled actions share the theme foreground");
   var selection=(CheckBox)controls[2];var selected=selection.IsChecked;
   try{selection.IsChecked=false;var circle=(Border)selection.Template.FindName("Box",selection);Check(circle.BorderBrush==ThemeManager.Brush("ActionForeground"),"Unselected circle uses the same contour color");Check(Math.Abs(circle.TransformToAncestor(group).TransformBounds(new Rect(circle.RenderSize)).Width-16)<.01,"Selection ink is reduced without shrinking the hit area");}finally{selection.IsChecked=selected;}
   var marker=shell.Find<System.Windows.Shapes.Path>(prefix+"ModuleIcon");var row=(Grid)((Border)shell.Find<Border>(prefix+"Row")).Child;
   Check(row.ColumnDefinitions[0].Width.Value==32&&row.ColumnDefinitions[2].Width.Value==92,"Shared marker gap preserves the right action group");
   var markerBounds=ModuleIconAlignment.Ink(marker,row);var status=shell.Find<TextBlock>(prefix+"State");Check(Math.Abs(status.TranslatePoint(new Point(),row).X-markerBounds.Right-12)<.1,"Both module rows have a twelve DIP visible marker-to-text gap");
   var gear=(System.Windows.Shapes.Path)((Button)controls[1]).Content;var transform=gear.RenderTransform.Value;var visibility=glyph.Visibility;
   try{glyph.Visibility=Visibility.Collapsed;shell.Window.UpdateLayout();ModuleIconAlignment.Update((Button)controls[0],(Button)controls[1],(CheckBox)controls[2]);shell.Window.UpdateLayout();Check(gear.RenderTransform.Value==transform,"Loading keeps neighboring glyphs fixed");}
   finally{glyph.Visibility=visibility;shell.Window.UpdateLayout();ModuleIconAlignment.Update((Button)controls[0],(Button)controls[1],(CheckBox)controls[2]);shell.Window.UpdateLayout();}
  }
  var plane=shell.Find<System.Windows.Shapes.Path>("TgModuleIcon");Check(plane.Data!=null&&plane.Fill==ThemeManager.Brush("SecondaryText")&&plane.RenderedGeometry.Bounds.Width>=16,"Theme-aware plane without background/padding");
  Check(shell.Find<System.Windows.Shapes.Path>("ZapretModuleIcon").Fill==plane.Fill,"Module marker palette matches");
 }
 public static async Task Run(Shell shell,AppFiles files,Func<string,Func<Task>,Task> test)
 {
  await test("032 Fluent icon hover/pressed/disabled geometry, theme resources and independent select",()=>{
   var saved=shell.Settings.Copy();
   try{foreach(string mode in new[]{"dark","light"})
   {
    var settings=saved.Copy();settings.Theme=mode;shell.ApplySettings(settings);shell.Window.UpdateLayout();Geometry(shell);
    foreach(string prefix in new[]{"Zapret","Tg"})
    {
     var button=shell.Find<Button>(prefix+"Install");var selection=shell.Find<CheckBox>(prefix+"Check");var initial=selection.IsChecked;
     foreach(Control control in new Control[]{button,shell.Find<Button>(prefix+"Settings"),selection})
     {
      var size=control.RenderSize;Force(control,typeof(UIElement),"IsMouseOverPropertyKey");control.UpdateLayout();var surface=(Border)control.Template.FindName("IconSurface",control);Check(surface.Background==ThemeManager.Brush("Hover")&&control.RenderSize==size,"Equal hover without size change");
      Force(control,typeof(System.Windows.Controls.Primitives.ButtonBase),"IsPressedPropertyKey");Check(surface.Background==ThemeManager.Brush("Pressed")&&control.RenderSize==size,"Pressed without movement");
      control.IsEnabled=false;Check(control.Opacity<=.4&&surface.Background!=ThemeManager.Brush("Pressed"),"Disabled does not keep pressed surface");
      Force(control,typeof(UIElement),"IsMouseOverPropertyKey",false);Force(control,typeof(System.Windows.Controls.Primitives.ButtonBase),"IsPressedPropertyKey",false);control.IsEnabled=true;
     }
     Check(selection.IsChecked==initial,"Visual states do not change selection");
    }
    var module=shell.OpenModuleSettings("tg");try{var folder=(Button)module.FindName("TgPathBrowse");Check(FocusCues.GetOwnFocusIndicator(folder)&&folder.ToolTip!=null&&folder.Content is System.Windows.Shapes.Path,"Shared folder button with localized tooltip");}finally{module.Close();}
   }}finally{shell.ApplySettings(saved);}return Task.CompletedTask;
  });
  await test("032 Fluent keyboard-only focus and visual matrix",async()=>{
   var saved=shell.Settings.Copy();
   try{foreach(string mode in new[]{"dark","light"})
   {
    var settings=saved.Copy();settings.Theme=mode;shell.ApplySettings(settings);TestActivation.Own(shell.Window);
    foreach(Control c in new Control[]{shell.Find<Button>("ZapretInstall"),shell.Find<CheckBox>("ZapretCheck")})
    {c.Focus();FocusCues.SetMode(true);await Task.Delay(20);var ring=(Border)c.Template.FindName("IconFocus",c);Check(ring.Visibility==Visibility.Visible&&c.FocusVisualStyle==null,"One explicit keyboard focus ring");FocusCues.SetMode(false);Check(ring.Visibility==Visibility.Collapsed&&c.IsKeyboardFocused,"Mouse mode clears ring without losing focus");}
    var general=shell.OpenSettings();try{TestActivation.Own(general);var toggle=(CheckBox)general.FindName("CloseToTray");toggle.Focus();FocusCues.SetMode(true);await Task.Delay(20);var ring=(Border)toggle.Template.FindName("ToggleFocus",toggle);Check(ring.Visibility==Visibility.Visible,"Switch retains keyboard focus");FocusCues.SetMode(false);Check(ring.Visibility==Visibility.Collapsed&&toggle.IsKeyboardFocused,"Switch mouse focus leaves no outline");}finally{general.Close();}
    var preview=new StackPanel{Background=ThemeManager.Brush("Surface")};
    foreach(bool installed in new[]{false,true})foreach(string state in new[]{"normal","hover","pressed","focus","disabled"})
    {
     var group=new ModuleActionGroup{Margin=new Thickness(12,8,12,8),HorizontalAlignment=HorizontalAlignment.Left};var glyph=FluentIcons.Glyph(shell.Window,installed?"RefreshGeometry":"DownloadGeometry");glyph.Style=(Style)shell.Window.Resources["IconArrow"];var content=new Grid{Width=18,Height=18};content.Children.Add(glyph);
     group.Children.Add(new Button{Style=(Style)shell.Window.Resources["ModuleAction"],Content=content});group.Children.Add(new Button{Style=(Style)shell.Window.Resources["IconButton"],Content=new System.Windows.Shapes.Path{Data=(System.Windows.Media.Geometry)shell.Window.Resources["GearGeometry"],Style=(Style)shell.Window.Resources["IconGear"]}});group.Children.Add(new CheckBox{Style=(Style)shell.Window.Resources["ModuleSelection"],IsChecked=installed});preview.Children.Add(group);
     foreach(Control c in group.Children)
     {c.ApplyTemplate();if(state=="hover"||state=="pressed")Force(c,typeof(UIElement),"IsMouseOverPropertyKey");if(state=="pressed")Force(c,typeof(System.Windows.Controls.Primitives.ButtonBase),"IsPressedPropertyKey");if(state=="focus"){FocusCues.SetKeyboardNavigation(c,true);Force(c,typeof(UIElement),"IsKeyboardFocusedPropertyKey");}if(state=="disabled")c.IsEnabled=false;}
    }
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})Render(preview,Path.Combine(files.Data,$"fluent-icons-{mode}-{scale}.png"),scale);
    SelfTests.Capture(shell.Window,Path.Combine(files.Data,$"fluent-main-render-{mode}.png"));
   }}finally{FocusCues.SetMode(false);shell.ApplySettings(saved);}
  });
 }
 static void Render(FrameworkElement element,string path,double scale){element.Measure(new Size(150,600));element.Arrange(new Rect(new Size(150,element.DesiredSize.Height)));element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);}
}
