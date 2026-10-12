using System;
using System.Windows;
using System.Windows.Controls;
namespace NetworkHub.Desktop;

// Three fixed 28 DIP cells separated by 4 DIP; no fractional UniformGrid widths.
public sealed class ModuleActionGroup : Panel
{
 public ModuleActionGroup(){UseLayoutRounding=true;SnapsToDevicePixels=true;}
 protected override Size MeasureOverride(Size available){foreach(UIElement child in InternalChildren)child.Measure(new Size(28,28));return new Size(92,28);}
 protected override Size ArrangeOverride(Size size){for(int i=0;i<InternalChildren.Count;i++)InternalChildren[i].Arrange(new Rect(i*32,Math.Max(0,(size.Height-28)/2),28,28));return size;}
}
