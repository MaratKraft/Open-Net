using System;
using System.Windows;
using System.Windows.Controls;
namespace NetworkHub.Desktop;
public sealed class CapsuleBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        CornerRadius=new CornerRadius(Math.Max(0,finalSize.Height/2));return base.ArrangeOverride(finalSize);
    }
}

