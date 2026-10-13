using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using NetworkHub.App;
namespace NetworkHub.Desktop;
public static class LocalizedTree
{
    public static IEnumerable<DependencyObject> Walk(DependencyObject parent)
    {
        yield return parent;
        foreach(object child in LogicalTreeHelper.GetChildren(parent))if(child is DependencyObject dependency)
            foreach(var item in Walk(dependency))yield return item;
    }
    public static void Bind(Window window)
    {
        var values=new List<(DependencyObject Target,DependencyProperty Property,string Key)>();
        void Add(DependencyObject target,DependencyProperty property)
        {
            if(target.GetValue(property) is not string text)return;
            string? key=L.Russian.ContainsKey(text)?text:L.Russian.FirstOrDefault(x=>x.Value==text).Key;
            if(key!=null)values.Add((target,property,key));
        }
        foreach(var item in Walk(window))
        {
            if(item is FrameworkElement element&&new[]{"DnsState","DnsCurrent","DnsPreview","DnsWarning","DnsResult","DnsOperationEvidence","DnsProbeEvidence","DnsReason","DnsProfileAddresses","Summary","Selection","ZapretState","TgState","ZapretStrategy","Message","StrategySupport","ModuleStatus","ModuleDiagnostic","StartupStatus"}.Contains(element.Name))continue;
            if(item is TextBlock)Add(item,TextBlock.TextProperty);
            if(item is ContentControl)Add(item,ContentControl.ContentProperty);
            if(item is FrameworkElement){Add(item,FrameworkElement.ToolTipProperty);Add(item,AutomationProperties.NameProperty);}
            if(item is Window)Add(item,Window.TitleProperty);
            if(item is Button button)button.Loaded+=(_,_)=>Capsule(button);
        }
        void Apply(){foreach(var value in values)value.Target.SetValue(value.Property,L.T(value.Key));}
        Apply();L.Changed+=Apply;window.Closed+=(_,_)=>L.Changed-=Apply;
    }
    static void Capsule(Button button)
    {
        button.ApplyTemplate();
        if(button.Template.FindName("Bd",button) is Border border)
        {void Update()=>border.CornerRadius=new CornerRadius(border.ActualHeight/2);border.SizeChanged+=(_,_)=>Update();Update();}
    }
}




