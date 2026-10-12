using System;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
namespace NetworkHub.Desktop;
// Application-owned confirmations follow the same palette; Windows UAC is untouched.
public static class AppDialog
{
 public static Window Create(Window owner,string message,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult defaultResult,Action<MessageBoxResult> complete){
  var window=new Window{Owner=owner,Title=caption,Icon=owner.Icon,Width=520,MinWidth=360,SizeToContent=SizeToContent.Height,MaxHeight=Math.Max(200,SystemParameters.WorkArea.Height-48),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=owner.FontFamily,FontSize=14};SecondaryAppearance.Attach(window);FocusCues.Attach(window);window.SourceInitialized+=(_,_)=>WindowSizing.Fit(window);
  var panel=new Grid{Margin=new Thickness(24)};panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});window.Content=panel;
  var messageGrid=new Grid();messageGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});messageGrid.ColumnDefinitions.Add(new ColumnDefinition());panel.Children.Add(messageGrid);
  if(image!=MessageBoxImage.None){var mark=new TextBlock{Text=image==MessageBoxImage.Question?"?":image==MessageBoxImage.Information?"i":"!",FontSize=28,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,16,0)};ThemeManager.Bind(mark,TextBlock.ForegroundProperty,image==MessageBoxImage.Error?"Error":image==MessageBoxImage.Warning?"Warning":"Muted");messageGrid.Children.Add(mark);}
  var text=new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,LineHeight=22};var scroll=new ScrollViewer{Content=text,MaxHeight=360,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,FocusVisualStyle=null};Grid.SetColumn(scroll,1);messageGrid.Children.Add(scroll);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};Grid.SetRow(actions,1);panel.Children.Add(actions);
  void Add(string caption,MessageBoxResult response,bool cancel=false){var button=new Button{Content=L.T(caption),MinWidth=88,Height=36,Margin=new Thickness(8,0,0,0),IsDefault=response==defaultResult,IsCancel=cancel};ThemeManager.Bind(button,Control.BackgroundProperty,response==defaultResult?"PrimaryBackground":"ButtonSurface");ThemeManager.Bind(button,Control.ForegroundProperty,response==defaultResult?"PrimaryText":"Text");button.Click+=(_,_)=>complete(response);actions.Children.Add(button);}
  if(buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel){Add("Yes",MessageBoxResult.Yes);Add("No",MessageBoxResult.No,buttons==MessageBoxButton.YesNo);if(buttons==MessageBoxButton.YesNoCancel)Add("Cancel",MessageBoxResult.Cancel,true);}else{Add("OK",MessageBoxResult.OK);if(buttons==MessageBoxButton.OKCancel)Add("Cancel",MessageBoxResult.Cancel,true);}
  return window;
 }
 public static MessageBoxResult Show(Window owner,string message,string caption,MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.No){MessageBoxResult result=buttons==MessageBoxButton.OK?MessageBoxResult.OK:buttons==MessageBoxButton.YesNo?MessageBoxResult.No:MessageBoxResult.Cancel;Window? window=null;window=Create(owner,message,caption,buttons,image,defaultResult,value=>{result=value;window!.DialogResult=true;});window.ShowDialog();return result;}
}
