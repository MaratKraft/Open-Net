using System;
using System.Windows;
using System.Windows.Controls;
namespace NetworkHub.Desktop;

// Two 28 DIP actions and a measured switch, with the approved 4 DIP gaps.
public sealed class ModuleActionGroup : Panel
{
 public ModuleActionGroup(){UseLayoutRounding=true;SnapsToDevicePixels=true;}
 protected override Size MeasureOverride(Size available){double width=0;foreach(UIElement child in InternalChildren){child.Measure(new Size(double.PositiveInfinity,28));width+=Math.Max(28,child.DesiredSize.Width)+4;}return new Size(Math.Max(0,width-4),28);}
 protected override Size ArrangeOverride(Size size){double left=0;foreach(UIElement child in InternalChildren){double width=Math.Max(28,child.DesiredSize.Width);child.Arrange(new Rect(left,Math.Max(0,(size.Height-28)/2),width,28));left+=width+4;}return size;}
}
