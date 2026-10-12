using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
internal static class SettingsLayout
{
    public static Window Create(Shell shell,string title,double height)
    {
        var window=new Window{Title=title,Width=560,Height=height,MinWidth=430,MinHeight=360,Owner=shell.Window,ResizeMode=ResizeMode.CanResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(25,25,25)),Foreground=Brushes.White,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Icon=shell.Window.Icon};
        window.NameScopeSetup();SecondaryAppearance.Attach(window);FocusCues.Attach(window);window.SourceInitialized+=(_,_)=>WindowSizing.Fit(window);window.DpiChanged+=(_,_)=>WindowSizing.Fit(window);return window;
    }
    public static StackPanel Panel(Window window)
    {
        var panel=new StackPanel{Margin=new Thickness(26)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,FocusVisualStyle=null};return panel;
    }
    public static void Heading(StackPanel panel,string title,string description)
    {
        panel.Children.Add(new TextBlock{Text=title,FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
        panel.Children.Add(new TextBlock{Text=description,Foreground=Brushes.Gray,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
    }
    public static void Label(StackPanel panel,string text)=>panel.Children.Add(new TextBlock{Text=L.T(text),Margin=new Thickness(0,8,0,6),Foreground=Brushes.LightGray});
    public static TextBlock Error(Window window,StackPanel panel,string name)
    {
        var error=new TextBlock{Foreground=Brushes.Salmon,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,4)};window.RegisterName(name,error);panel.Children.Add(error);return error;
    }
    public static CheckBox Toggle(Window window,StackPanel panel,Shell shell,string name,string caption,bool value,string? tooltip=null)
    {
        var toggle=new CheckBox{Content=L.T(caption),IsChecked=value,Margin=new Thickness(0,14,0,0),Style=(Style)shell.Window.Resources["OptionToggle"],ToolTip=tooltip==null?null:L.T(tooltip)};window.RegisterName(name,toggle);panel.Children.Add(toggle);return toggle;
    }
    public static Button Button(Window window,string name,string caption)
    {
        var button=new Button{Content=L.T(caption),Height=36,FontSize=12,Background=new SolidColorBrush(Color.FromRgb(43,43,43)),Padding=new Thickness(10,6,10,6)};window.RegisterName(name,button);return button;
    }
    public static Button IconButton(Window window,string name,string caption,string geometry)
    {
        var button=new Button{Style=(Style)window.FindResource("IconButton"),Content=FluentIcons.Glyph(window,geometry),ToolTip=L.T(caption)};
        System.Windows.Automation.AutomationProperties.SetName(button,L.T(caption));window.RegisterName(name,button);return button;
    }
    public static Grid ActionGrid(Window window,StackPanel panel,string name)
    {
        var grid=new Grid{Margin=new Thickness(0,12,0,0)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());window.RegisterName(name,grid);panel.Children.Add(grid);return grid;
    }
    public static void AddAction(Grid grid,Button button,int index)
    {
        while(grid.RowDefinitions.Count<=index/2)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(button,index%2);Grid.SetRow(button,index/2);button.Margin=new Thickness(index%2==0?0:8,index<2?0:8,0,0);grid.Children.Add(button);
    }
    public static Button Save(Window window,StackPanel panel,string name="SaveSettings")
    {
        var save=Button(window,name,"Save");save.Height=38;save.Background=Brushes.White;save.Foreground=Brushes.Black;save.Padding=new Thickness(20,8,20,8);save.HorizontalAlignment=HorizontalAlignment.Right;save.Margin=new Thickness(0,12,0,0);panel.Children.Add(save);return save;
    }
    public static void OpenFolder(Shell shell,AppFiles files,string path)
    {
        var plan=ZapretActions.FolderPlan(path);if(shell.Runtime.TestMode)files.Log("Safe folder-open plan: "+plan.FileName);else Process.Start(plan);
    }
    public static async Task ImportRelease(Shell shell,string id,Window owner)
    {
        var definition=ModuleCatalog.Get(id);var picker=new OpenFileDialog{Title=L.T("Import official release"),Filter=definition.SettingsKind==ModuleSettingsKind.Bundle?L.T("Zapret ZIP|*.zip"):L.T("TG Windows EXE|*.exe")};if(picker.ShowDialog(owner)==true)await shell.ImportRelease(id,picker.FileName,owner);
    }
    public static void BrowseBundle(Shell shell,AppFiles files,Window owner,TextBox input,Button button)
    {
        var menu=new ContextMenu{Style=(Style)shell.Window.Resources["ZapretMenu"],Foreground=Brushes.White,PlacementTarget=button};
        MenuItem Item(string label){var item=new MenuItem{Header=L.T(label),Style=(Style)shell.Window.Resources["ZapretMenuItem"],Padding=new Thickness(12,7,12,7),Foreground=Brushes.White};ThemeManager.Bind(item,MenuItem.ForegroundProperty,"Text");menu.Items.Add(item);return item;}
        ThemeManager.Bind(menu,ContextMenu.ForegroundProperty,"Text");
        Item("Open Zapret folder").Click+=(_,_)=>{try{OpenFolder(shell,files,files.Resolve(input.Text.Trim()));}catch(Exception ex){shell.ShowMessage(L.Error(ex));}};
        Item("Configure Zapret").Click+=async(_,_)=>await shell.ConfigureZapret(files.Resolve(input.Text.Trim()),owner);
        Item("Select folder").Click+=(_,_)=>{using var picker=new Forms.FolderBrowserDialog{Description=L.T("Choose the portable Zapret folder"),UseDescriptionForTitle=true,SelectedPath=input.Text};if(picker.ShowDialog()==Forms.DialogResult.OK)input.Text=picker.SelectedPath;};
        button.ContextMenu=menu;menu.IsOpen=true;
    }
}
