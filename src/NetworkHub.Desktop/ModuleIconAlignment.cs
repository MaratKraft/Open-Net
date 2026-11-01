using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace NetworkHub.Desktop;

// Shared ink measurements and stable centering for the approved compact controls.
public static class ModuleIconAlignment
{
    public static Rect Ink(Path path,Visual relative)
    {
        var pen=path.Stroke==null?null:new Pen(path.Stroke,path.StrokeThickness){StartLineCap=path.StrokeStartLineCap,EndLineCap=path.StrokeEndLineCap,LineJoin=path.StrokeLineJoin};
        return path.TransformToAncestor(relative).TransformBounds(path.RenderedGeometry.GetRenderBounds(pen));
    }
    public static void Update(Button install,Button settings,CheckBox selection)
    {
        // Approved pre.3 correction: glyph, hover and focus share the same centre.
        if(settings.Content is Path gear)gear.RenderTransform=Transform.Identity;
    }
    public static void Attach(Button install,Button settings,CheckBox selection)
    {
        settings.Loaded+=(_,_)=>Update(install,settings,selection);
        Update(install,settings,selection);
    }
    public static void GeneralSettings(Path gear)
    {
        string outline=gear.Data.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int end=outline.IndexOf('Z');if(end<0)end=outline.IndexOf('z');
        var filled=new GeometryGroup{FillRule=FillRule.EvenOdd};
        filled.Children.Add(Geometry.Parse(outline.Substring(0,end+1)));
        filled.Children.Add(new EllipseGeometry(new Point(10,10),2.5,2.5));
        gear.Data=filled;gear.RenderTransform=new ScaleTransform(.94,.94,10,10);
    }
}
