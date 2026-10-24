using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace NetworkHub.Desktop;

// Shared content only; existing button templates own hover, focus and hit areas.
internal static class CompactUi
{
    internal static FrameworkElement Disclosure(string caption,bool expanded=false)
    {
        var content=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        var circle=new Grid{Width=14,Height=14,Margin=new Thickness(0,0,7,0),VerticalAlignment=VerticalAlignment.Center};
        var fill=new Ellipse();ThemeManager.Bind(fill,Shape.FillProperty,"Text");circle.Children.Add(fill);
        var arrow=new Path{Data=Geometry.Parse("M3.5,5.5 L7,9 L10.5,5.5"),StrokeThickness=1.25,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(expanded?180:0)};
        ThemeManager.Bind(arrow,Shape.StrokeProperty,"Surface");circle.Children.Add(arrow);content.Children.Add(circle);
        var label=new TextBlock{Text=caption,FontSize=12,VerticalAlignment=VerticalAlignment.Center};ThemeManager.Bind(label,TextBlock.ForegroundProperty,"Text");content.Children.Add(label);return content;
    }
    internal static FrameworkElement Modules(string caption)
    {
        var content=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        var label=new TextBlock{Text=caption,FontSize=12,VerticalAlignment=VerticalAlignment.Center};ThemeManager.Bind(label,TextBlock.ForegroundProperty,"Text");content.Children.Add(label);
        var icon=new Grid{Width=14,Height=14,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};
        foreach(int x in new[]{1,8})foreach(int y in new[]{1,8}){var square=new Border{Width=5,Height=5,CornerRadius=new CornerRadius(1.2),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(x,y,0,0)};ThemeManager.Bind(square,Border.BackgroundProperty,"SecondaryText");icon.Children.Add(square);}content.Children.Add(icon);return content;
    }
}
