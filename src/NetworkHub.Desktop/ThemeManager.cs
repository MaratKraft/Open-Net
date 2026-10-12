using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace NetworkHub.Desktop;
// A shared palette for all application windows; material is independent of theme.
public static class ThemeManager
{
 public static string Mode {get;private set;}="system";
 public static bool IsDark {get;private set;}=true;
 public static event Action? Changed;
 static bool? lastContrast;
#if OPENNET_TESTING
 public static Func<bool>? SystemDarkForTests {get;set;}
#endif
 public static bool WindowsIsDark(){
#if OPENNET_TESTING
  if(SystemDarkForTests!=null)return SystemDarkForTests();
#endif
  try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0;}catch(Exception ex)when(ex is System.Security.SecurityException or UnauthorizedAccessException){return false;}
 }
 public static string Resolve(string mode,bool windowsDark)=>mode=="dark"?"dark":mode=="light"?"light":windowsDark?"dark":"light";
 public static void SetMode(string mode){Mode=mode is "dark" or "light"?mode:"system";Refresh();}
 public static void Refresh(){bool dark=Mode=="dark"||(Mode=="system"&&WindowsIsDark());if(Application.Current==null)return;if(Application.Current.Resources.Contains("ThemeReady")&&IsDark==dark&&lastContrast==SystemParameters.HighContrast)return;IsDark=dark;lastContrast=SystemParameters.HighContrast;
  var colors=new Dictionary<string,(string Dark,string Light)>{
   ["Surface"]=("#191919","#F3F3F3"),["Input"]=("#252525","#FAFAFA"),["Text"]=("#F3F3F3","#202020"),["Muted"]=("#999999","#626262"),["SecondaryText"]=("#CFCFCF","#4A4A4A"),["Line"]=("#303030","#D6D6D6"),["InputBorder"]=("#444444","#C9C9C9"),["ButtonSurface"]=("#2B2B2B","#E6E6E6"),["Hover"]=("#353535","#DDDDDD"),["Pressed"]=("#454545","#CECECE"),["PopupSurface"]=("#252525","#F7F7F7"),["PopupBorder"]=("#555555","#CCCCCC"),["PopupHover"]=("#414141","#E1E1E1"),["PrimaryBackground"]=("#F3F3F3","#242424"),["PrimaryText"]=("#171717","#FFFFFF"),["SelectionBackground"]=("#F3F3F3","#242424"),["SelectionText"]=("#191919","#FFFFFF"),["Focus"]=("#8EADE5","#3569B1"),["FocusBorder"]=("#A0A0A0","#606060"),["FocusSurface"]=("#0FFFFFFF","#10000000"),["ToggleSurface"]=("#333333","#DEDEDE"),["ToggleBorder"]=("#777777","#949494"),["ToggleThumb"]=("#CCCCCC","#FFFFFF"),["ToggleOn"]=("#379164","#238554"),["White"]=("#FFFFFF","#FFFFFF"),["ScrollSurface"]=("#1E1E1E","#EEEEEE"),["ScrollThumb"]=("#666666","#AAAAAA"),["ScrollHover"]=("#999999","#888888"),["Success"]=("#47BC7C","#167B43"),["Warning"]=("#DAB46C","#8C5B00"),["Error"]=("#E7917D","#B42318"),["LogSurface"]=("#1E1E1E","#FAFAFA"),["LogSelection"]=("#476B88","#BED6F0")
  };
  foreach(var item in colors){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?item.Value.Dark:item.Value.Light));brush.Freeze();Application.Current.Resources[item.Key]=brush;}
  if(SystemParameters.HighContrast){foreach(string key in new[]{"Text","Muted","SecondaryText","Success","Warning","Error","FocusBorder"})Application.Current.Resources[key]=SystemColors.WindowTextBrush;foreach(string key in new[]{"Surface","Input","ButtonSurface","PopupSurface","LogSurface","ScrollSurface"})Application.Current.Resources[key]=SystemColors.WindowBrush;Application.Current.Resources["SelectionBackground"]=SystemColors.HighlightBrush;Application.Current.Resources["SelectionText"]=SystemColors.HighlightTextBrush;}
  Application.Current.Resources["ThemeReady"]=true;Changed?.Invoke();
 }
 public static Brush Brush(string key)=>(Brush)Application.Current.Resources[key];
 public static void Bind(FrameworkElement element,DependencyProperty property,string key)=>element.SetResourceReference(property,key);
 public static void Attach(Window window){Bind(window,Window.BackgroundProperty,"Surface");Bind(window,Window.ForegroundProperty,"Text");window.Loaded+=(_,_)=>BindTree(window);}
 public static void BindTree(Window window){foreach(var item in LocalizedTree.Walk(window)){
  if(item is TextBlock text&&text.ReadLocalValue(TextBlock.ForegroundProperty)!=DependencyProperty.UnsetValue)BindOld(text,TextBlock.ForegroundProperty);
  if(item is Control control){bool primary=control is Button&&control.Background is SolidColorBrush bg&&bg.Color==Colors.White&&control.Foreground is SolidColorBrush fg&&fg.Color==Colors.Black;if(primary){Bind(control,Control.BackgroundProperty,"PrimaryBackground");Bind(control,Control.ForegroundProperty,"PrimaryText");}else{BindOld(control,Control.BackgroundProperty);BindOld(control,Control.ForegroundProperty);}BindOld(control,Control.BorderBrushProperty);}
  if(item is TextBox input){Bind(input,TextBox.CaretBrushProperty,"Text");if(input.Name=="LogText"){Bind(input,Control.BackgroundProperty,"LogSurface");Bind(input,TextBox.SelectionBrushProperty,"LogSelection");}}
 }}
 static void BindOld(FrameworkElement element,DependencyProperty property){if(element.ReadLocalValue(property)?.GetType().Name=="ResourceReferenceExpression")return;if(element.GetValue(property) is not SolidColorBrush brush)return;string? key=brush.Color.ToString().ToUpperInvariant() switch{
  "#FFFFFFFF" or "#FFF3F3F3"=>"Text","#FF000000"=>"Text","#FF191919"=>"Surface","#FF252525"=>"Input","#FF1E1E1E"=>"LogSurface","#FF2B2B2B"=>"ButtonSurface","#FF808080" or "#FF999999"=>"Muted","#FFD3D3D3" or "#FFCCCCCC"=>"SecondaryText","#FF333333" or "#FF444444"=>"InputBorder","#FFFA8072"=>"Error",_=>null};if(key!=null)Bind(element,property,key);}
 public static void StyleTray(Forms.ContextMenuStrip menu){menu.BackColor=ToDrawing("PopupSurface");menu.ForeColor=ToDrawing("Text");menu.Renderer=new Forms.ToolStripProfessionalRenderer(new TrayColors());foreach(Forms.ToolStripItem item in menu.Items){item.ForeColor=menu.ForeColor;item.BackColor=menu.BackColor;}}
 static System.Drawing.Color ToDrawing(string key){var c=((SolidColorBrush)Brush(key)).Color;return System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B);}
 sealed class TrayColors:Forms.ProfessionalColorTable{public override System.Drawing.Color ToolStripDropDownBackground=>ToDrawing("PopupSurface");public override System.Drawing.Color MenuBorder=>ToDrawing("Line");public override System.Drawing.Color MenuItemSelected=>ToDrawing("Hover");public override System.Drawing.Color MenuItemBorder=>ToDrawing("Line");public override System.Drawing.Color ImageMarginGradientBegin=>ToDrawing("PopupSurface");public override System.Drawing.Color ImageMarginGradientMiddle=>ToDrawing("PopupSurface");public override System.Drawing.Color ImageMarginGradientEnd=>ToDrawing("PopupSurface");public override System.Drawing.Color SeparatorDark=>ToDrawing("Line");public override System.Drawing.Color SeparatorLight=>ToDrawing("PopupSurface");}
}
