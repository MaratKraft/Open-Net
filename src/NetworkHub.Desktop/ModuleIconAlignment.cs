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
        // Approved compact glyphs share fixed centered hit areas. Keep the gear stable
        // across Download, Refresh and progress rather than chasing asymmetric ink.
        if(settings.Content is Path gear)gear.RenderTransform=Transform.Identity;
    }
    public static void Attach(Button install,Button settings,CheckBox selection)
    {
        void Align(object? sender,EventArgs args)=>Update(install,settings,selection);
        settings.Loaded+=(_,_)=>{settings.LayoutUpdated+=Align;Update(install,settings,selection);};
        settings.Unloaded+=(_,_)=>settings.LayoutUpdated-=Align;
    }
}
