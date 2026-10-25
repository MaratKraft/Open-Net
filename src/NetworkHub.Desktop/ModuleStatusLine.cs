using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NetworkHub.Desktop;

// A bounded second row. Profile text may trim; the action column never moves.
internal sealed class ModuleStatusLine : Grid
{
    readonly TextBlock profile=new(){FontSize=12,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
    internal Button Selector {get;}
    internal string ProfileText=>profile.Text;
    internal ModuleStatusLine(Window resources,TextBlock status)
    {
        ColumnDefinitions.Add(new(){Width=GridLength.Auto});ColumnDefinitions.Add(new(){Width=GridLength.Auto});ColumnDefinitions.Add(new());
        SizeChanged+=(_,_)=>status.MaxWidth=Math.Max(64,ActualWidth*.58);
        SetColumn(status,0);SetRow(status,0);SetColumnSpan(status,1);
        status.TextWrapping=TextWrapping.NoWrap;status.TextTrimming=TextTrimming.CharacterEllipsis;status.Margin=new Thickness(0);status.VerticalAlignment=VerticalAlignment.Center;Children.Add(status);
        var dot=new TextBlock{Text=" · ",FontSize=12,VerticalAlignment=VerticalAlignment.Center};ThemeManager.Bind(dot,TextBlock.ForegroundProperty,"Muted");SetColumn(dot,1);Children.Add(dot);
        Selector=new Button{Style=(Style)resources.FindResource("QuietLink"),Height=28,Padding=new Thickness(2,0,2,0),HorizontalAlignment=HorizontalAlignment.Left,HorizontalContentAlignment=HorizontalAlignment.Left};
        var content=new Grid();content.ColumnDefinitions.Add(new());content.ColumnDefinitions.Add(new(){Width=new GridLength(14)});ThemeManager.Bind(profile,TextBlock.ForegroundProperty,"Muted");content.Children.Add(profile);
        var chevron=new System.Windows.Shapes.Path{Data=Geometry.Parse("M1,3 L4,6 L7,3"),Width=8,Height=8,StrokeThickness=1.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};ThemeManager.Bind(chevron,System.Windows.Shapes.Path.StrokeProperty,"Muted");SetColumn(chevron,1);content.Children.Add(chevron);Selector.Content=content;SetColumn(Selector,2);Children.Add(Selector);
        Height=18;Margin=new Thickness(0,2,0,0);Selector.Margin=new Thickness(0,-5,0,-5);
    }
    internal void Update(string active,string selected,bool running)
    {
        profile.Text=Description(active,selected,running);Selector.ToolTip=profile.Text;System.Windows.Automation.AutomationProperties.SetName(Selector,profile.Text);
    }
    internal static string Description(string active,string selected,bool running)=>running&&!string.Equals(active,selected,StringComparison.OrdinalIgnoreCase)?(active.Length==0?NetworkHub.App.L.T("Unknown profile"):active)+" → "+selected:selected;
}
