using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CyberGolden;
public sealed class ToyWindow : Window
{
    private readonly Canvas drawing=new() {Width=32,Height=32};
    private string kind="";
    private IntPtr handle;
    public ToyWindow(Action cancel)
    {
        Width=Height=24;AllowsTransparency=true;Background=Brushes.Transparent;WindowStyle=WindowStyle.None;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Focusable=false;Topmost=true;
        Content=new Viewbox {Child=drawing};
        SourceInitialized+=(_,_)=>{handle=new WindowInteropHelper(this).Handle;Native.NoActivate(handle);HwndSource.FromHwnd(handle).AddHook((IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool done)=>{if(msg==0x21){done=true;return new IntPtr(3);}return IntPtr.Zero;});};
        MouseLeftButtonDown+=(_,e)=>{cancel();e.Handled=true;};
    }
    public void Display(string name,double x,double y,double scale)
    {
        if(name!=kind){kind=name;Draw(name);}
        if(!IsVisible)Show();
        var size=24*scale;Native.SetWindowPos(handle,IntPtr.Zero,(int)(x-size/2),(int)(y-size/2),(int)size,(int)size,0x0014);
    }
    private void Draw(string name)
    {
        drawing.Children.Clear();
        if(name=="ball")
        {
            drawing.Children.Add(new Ellipse {Width=26,Height=26,Margin=new Thickness(3),Fill=new SolidColorBrush(Color.FromRgb(198,220,66)),Stroke=new SolidColorBrush(Color.FromRgb(88,124,46)),StrokeThickness=1});
            drawing.Children.Add(new System.Windows.Shapes.Path {Stroke=Brushes.White,StrokeThickness=2,Data=Geometry.Parse("M9,4 C23,11 23,21 9,28 M25,5 C12,12 12,20 25,27")});
        }
        else
        {
            var fill=new SolidColorBrush(Color.FromRgb(177,133,98));
            foreach(var (x,y,w,h) in new[]{(7,12,18,18),(5,4,8,9),(19,4,8,9),(8,5,16,17),(2,16,10,8),(21,16,10,8)})
            {var shape=new Ellipse {Width=w,Height=h,Fill=fill};Canvas.SetLeft(shape,x);Canvas.SetTop(shape,y);drawing.Children.Add(shape);}
            foreach(var x in new[]{12,20}){var eye=new Ellipse {Width=2,Height=3,Fill=Brushes.Black};Canvas.SetLeft(eye,x);Canvas.SetTop(eye,12);drawing.Children.Add(eye);}
        }
    }
}
