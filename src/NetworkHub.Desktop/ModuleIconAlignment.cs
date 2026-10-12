using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace NetworkHub.Desktop;

// Equal hit areas stay fixed. The gear is centered in the visible gap,
// measured from the current download/refresh ink to the selection circle.
public static class ModuleIconAlignment
{
    public static Rect Ink(Path path,Visual relative)
    {
        var pen=path.Stroke==null?null:new Pen(path.Stroke,path.StrokeThickness){StartLineCap=path.StrokeStartLineCap,EndLineCap=path.StrokeEndLineCap,LineJoin=path.StrokeLineJoin};
        return path.TransformToAncestor(relative).TransformBounds(path.RenderedGeometry.GetRenderBounds(pen));
    }
    public static void Update(Button install,Button settings,CheckBox selection)
    {
        if(settings.Content is not Path gear||install.Content is not Grid grid)return;
        var first=grid.Children.OfType<Path>().First();selection.ApplyTemplate();
        // Progress replaces the glyph without moving its neighbors.
        if(first.Visibility!=Visibility.Visible)return;
        if(selection.Template.FindName("Box",selection) is not Border circle)return;
        var group=(Visual)settings.Parent;
        var left=Ink(first,group);
        var right=circle.TransformToAncestor(group).TransformBounds(new Rect(circle.RenderSize));
        if(left.IsEmpty||right.Width==0)return;
        double current=gear.RenderTransform is TranslateTransform shift?shift.X:0;
        // The unchanged, symmetric gear fills its centered icon content box.
        // Use the fixed button center so a pending render transform cannot accumulate.
        double center=settings.TranslatePoint(new Point(settings.ActualWidth/2,settings.ActualHeight/2),(UIElement)group).X;
        double wanted=(left.Right+right.Left)/2-center;
        if(Math.Abs(wanted-current)>.01)gear.RenderTransform=new TranslateTransform(wanted,0);
    }
    public static void Attach(Button install,Button settings,CheckBox selection)
    {
        void Align(object? sender,EventArgs args)=>Update(install,settings,selection);
        settings.Loaded+=(_,_)=>{settings.LayoutUpdated+=Align;Update(install,settings,selection);};
        settings.Unloaded+=(_,_)=>settings.LayoutUpdated-=Align;
    }
}
