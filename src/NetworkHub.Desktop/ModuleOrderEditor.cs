using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NetworkHub.App;
namespace NetworkHub.Desktop;

// Presentation-only drag editor. It cannot access runtimes, DNS or startup stores.
internal sealed class ModuleOrderEditor : UserControl
{
    readonly StackPanel panel=new();readonly Dictionary<string,Border> rows=new();readonly Window resources;
    string? dragging;Point origin;double grab;bool moving;DragImage? image;AdornerLayer? layer;string[] start=Array.Empty<string>();
    internal string[] Order=>panel.Children.Cast<FrameworkElement>().Select(r=>(string)r.Tag).ToArray();
    internal event Action? Changed;
    internal event Action? Committed;
    internal bool IsDragging=>dragging!=null;
    internal void SetOrder(string[] order){panel.Children.Clear();foreach(string id in order)panel.Children.Add(rows[id]);}
    internal ModuleOrderEditor(Window resources,IEnumerable<string> ids,bool iconHandle=false,Action<string,Grid>? configure=null)
    {
        this.resources=resources;Content=new AdornerDecorator{Child=panel};
        foreach(string id in ids){var grid=new Grid{Height=62};grid.ColumnDefinitions.Add(new(){Width=new GridLength(28)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(28)});grid.ColumnDefinitions.Add(new());
            var marker=Marker(id);var handle=new Button{Style=(Style)resources.FindResource("ModuleAction"),ToolTip=L.T("Drag or use Alt+Up/Down"),Cursor=Cursors.SizeAll};System.Windows.Automation.AutomationProperties.SetName(handle,L.T("Move")+" "+WorkspaceController.Name(id));
            if(iconHandle){handle.Content=marker;Grid.SetColumn(handle,1);}else{handle.Content=Dots();Grid.SetColumn(marker,1);grid.Children.Add(marker);}grid.Children.Add(handle);
            var name=new TextBlock{Text=WorkspaceController.Name(id),FontSize=14,VerticalAlignment=VerticalAlignment.Center};ThemeManager.Bind(name,TextBlock.ForegroundProperty,"Text");Grid.SetColumn(name,2);grid.Children.Add(name);
            var row=new Border{Tag=id,Child=grid,BorderThickness=new Thickness(0,0,0,1),CornerRadius=new CornerRadius(6),Padding=new Thickness(4,0,4,0)};ThemeManager.Bind(row,Border.BorderBrushProperty,"Line");panel.Children.Add(row);rows[id]=row;configure?.Invoke(id,grid);
            handle.PreviewMouseLeftButtonDown+=(_,e)=>{dragging=id;origin=e.GetPosition(panel);grab=e.GetPosition(row).Y;start=Order;handle.CaptureMouse();e.Handled=true;};
            handle.PreviewMouseMove+=(_,e)=>{if(dragging!=id||e.LeftButton!=MouseButtonState.Pressed)return;Point point=e.GetPosition(panel);if(!moving&&Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance&&Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance)return;Begin();DragTo(point.Y);e.Handled=true;};
            handle.PreviewMouseLeftButtonUp+=(_,e)=>{if(dragging!=id)return;End(false);handle.ReleaseMouseCapture();e.Handled=true;};handle.LostMouseCapture+=(_,_)=>{if(dragging==id)End(true);};
            handle.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&dragging!=null){End(true);handle.ReleaseMouseCapture();e.Handled=true;}else if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)&&e.Key is Key.Up or Key.Down){Move(id,e.Key==Key.Up?-1:1);e.Handled=true;}};
            var menu=new ContextMenu{Style=(Style)resources.FindResource("ZapretMenu")};foreach(int delta in new[]{-1,1}){var item=new MenuItem{Header=L.T(delta<0?"Move up":"Move down"),Padding=new Thickness(12,7,12,7),Style=(Style)resources.FindResource("ZapretMenuItem")};item.Click+=(_,_)=>Move(id,delta);menu.Items.Add(item);}handle.ContextMenu=menu;
        }
        Unloaded+=(_,_)=>{if(dragging!=null)End(true);};
    }
    FrameworkElement Marker(string id)
    {
        if(id!="dns"){var p=new Path{Data=(Geometry)resources.FindResource(id=="zapret"?"ZapretGeometry":"TelegramGeometry"),Width=18,Height=18,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};ThemeManager.Bind(p,Path.FillProperty,"SecondaryText");return p;}
        // Reuse the actual DNS geometry instead of inventing a different module icon.
        var old=((Grid)resources.FindName("DnsGlyph"));var path=old.Children.OfType<Path>().First();var copy=new Path{Data=path.Data.Clone(),Width=18,Height=18,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};ThemeManager.Bind(copy,Path.FillProperty,"SecondaryText");return copy;
    }
    internal static FrameworkElement Dots(){var grid=new Grid{Width=18,Height=20};foreach(int x in new[]{5,11})foreach(int y in new[]{3,9,15}){var dot=new Ellipse{Width=2,Height=2,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(x,y,0,0)};ThemeManager.Bind(dot,Shape.FillProperty,"Muted");grid.Children.Add(dot);}return grid;}
    void Begin(){if(moving||dragging==null)return;moving=true;layer=AdornerLayer.GetAdornerLayer(panel);image=new DragImage(panel,rows[dragging],grab);rows[dragging].Opacity=.25;layer?.Add(image);}
    void DragTo(double y){if(dragging==null)return;image?.Follow(y);var current=Order.ToList();int from=current.IndexOf(dragging),to=Math.Clamp((int)(Math.Max(0,y)/Math.Max(1,rows[dragging].ActualHeight)),0,current.Count-1);if(from!=to)Reorder(dragging,to);}
    void End(bool cancel){if(dragging==null)return;string id=dragging;double? final=image?.Top;if(image!=null)layer?.Remove(image);image=null;layer=null;rows[id].Opacity=1;dragging=null;moving=false;if(cancel){panel.Children.Clear();foreach(string item in start)panel.Children.Add(rows[item]);}else if(final.HasValue){panel.UpdateLayout();Animate(rows[id],final.Value-rows[id].TranslatePoint(new Point(),panel).Y);}Changed?.Invoke();if(!cancel)Committed?.Invoke();}
    void Reorder(string id,int to){var positions=rows.ToDictionary(p=>p.Key,p=>p.Value.TranslatePoint(new Point(),panel).Y);var row=rows[id];panel.Children.Remove(row);panel.Children.Insert(to,row);foreach(var item in rows.Values)item.RenderTransform=Transform.Identity;panel.UpdateLayout();foreach(var p in rows.Where(p=>p.Key!=dragging))Animate(p.Value,positions[p.Key]-p.Value.TranslatePoint(new Point(),panel).Y);Changed?.Invoke();}
    void Animate(Border row,double from){row.RenderTransform=new TranslateTransform();if(!SystemParameters.ClientAreaAnimation||Math.Abs(from)<.1)return;((TranslateTransform)row.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(from,0,TimeSpan.FromMilliseconds(140)){DecelerationRatio=.8});}
    internal void Move(string id,int delta){var ids=Order;int from=Array.IndexOf(ids,id),to=from+delta;if(from>=0&&to>=0&&to<ids.Length){Reorder(id,to);Committed?.Invoke();}}
#if OPENNET_TESTING
    internal void DragForTests(string id,double y,bool cancel=false){dragging=id;grab=20;start=Order;Begin();DragTo(y);End(cancel);}
    internal async System.Threading.Tasks.Task Demonstrate()
    {
        if(dragging!=null)return;IsEnabled=false;dragging=Order[0];grab=20;start=Order;Begin();
        try{int steps=SystemParameters.ClientAreaAnimation?24:1;for(int i=1;i<=steps;i++){DragTo(20+160d*i/steps);if(steps>1)await System.Threading.Tasks.Task.Delay(30);}End(false);}finally{if(dragging!=null)End(true);IsEnabled=true;}
    }
#endif
    sealed class DragImage : Adorner
    {
        readonly ImageBrush brush;readonly Border row;readonly double grab;internal double Top {get;private set;}
        internal DragImage(UIElement host,Border row,double grab):base(host){this.row=row;this.grab=grab;var dpi=VisualTreeHelper.GetDpi(row);var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(row.ActualWidth*dpi.DpiScaleX)),Math.Max(1,(int)Math.Ceiling(row.ActualHeight*dpi.DpiScaleY)),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);bitmap.Render(row);brush=new ImageBrush(bitmap);IsHitTestVisible=false;}
        internal void Follow(double y){Top=y-grab;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc){double width=AdornedElement.RenderSize.Width,height=row.ActualHeight;dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(22,0,0,0)),null,new Rect(2,Top+3,width-4,height),6,6);dc.DrawRoundedRectangle(ThemeManager.Brush("PopupSurface"),new Pen(ThemeManager.Brush("InputBorder"),1),new Rect(0,Top,width,height),6,6);dc.PushOpacity(1);dc.DrawRectangle(brush,null,new Rect(0,Top,width,height));dc.Pop();}
    }
}
