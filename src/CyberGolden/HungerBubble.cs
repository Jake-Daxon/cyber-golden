using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace CyberGolden;
public sealed class HungerBubble : Window
{
    public IntPtr Handle { get; private set; }
    private (int,int,int,int) previous;
    public HungerBubble()
    {
        Title="赛博金毛 · 我饿了";Width=90;Height=34;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Focusable=false;IsHitTestVisible=false;Topmost=true;
        Content=new Border{Background=new SolidColorBrush(Color.FromRgb(255,250,238)),BorderBrush=new SolidColorBrush(Color.FromRgb(198,164,91)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=new TextBlock{Text="我饿了",FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(84,62,30)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        SourceInitialized+=(_,_)=>
        {
            Handle=new WindowInteropHelper(this).Handle;Native.NoActivate(Handle);Native.ClickThrough(Handle);
            HwndSource.FromHwnd(Handle).AddHook((IntPtr h,int m,IntPtr w,IntPtr l,ref bool done)=>{if(m==0x84){done=true;return new IntPtr(-1);}if(m==0x21){done=true;return new IntPtr(3);}return IntPtr.Zero;});
        };
    }
    public void Follow(BehaviorEngine engine)
    {
        if(!engine.Settings.Hungry||engine.Settings.Hidden){Hide();return;}
        if(!IsVisible)Show();
        var scale=engine.DpiScale;var width=(int)(90*scale);var height=(int)(34*scale);
        var x=engine.X+engine.PixelSize-10*scale;var y=engine.Y+12*scale;
        if(x+width>engine.WorkRight-8*scale)x=engine.X-width+10*scale;
        x=Math.Clamp(x,engine.WorkLeft+8*scale,Math.Max(engine.WorkLeft+8*scale,engine.WorkRight-width-8*scale));
        y=Math.Clamp(y,engine.WorkTop+8*scale,Math.Max(engine.WorkTop+8*scale,engine.WorkBottom-height-8*scale));
        var placement=((int)x,(int)y,width,height);
        if(placement==previous)return;previous=placement;
        Native.SetWindowPos(Handle,IntPtr.Zero,placement.Item1,placement.Item2,width,height,0x0014);
    }
}
