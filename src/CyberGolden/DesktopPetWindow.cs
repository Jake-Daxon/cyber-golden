using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using VPet_Simulator.Core;
using Forms=System.Windows.Forms;

namespace CyberGolden;
public sealed class DesktopPetWindow : Window
{
    public readonly Settings Settings;
    public readonly GameSave Pet;
    public readonly StateStore Store;
    public readonly BehaviorEngine Engine;
    public readonly AnimationPlayer Player;
    public readonly LocalAudio Audio;
    public readonly Image PetImage=new() {Width=500,Height=500,Stretch=Stretch.Fill,IsHitTestVisible=false};
    private readonly Grid surface=new() {Width=500,Height=500,Background=Brushes.Transparent};
    private readonly DispatcherTimer timer;
    private readonly Stopwatch elapsed=Stopwatch.StartNew();
    private readonly Forms.NotifyIcon tray;
    private readonly ToyWindow toy;
    public readonly HungerBubble HungerText=new();
    private sealed class DropRequest {public readonly string Id=Guid.NewGuid().ToString("N");}
    private readonly ConditionalWeakTable<DragEventArgs,DropRequest> dropRequests=new();
    private readonly Dictionary<string,double[][]> mouths=new();
    private HwndSource? source;
    public IntPtr Handle { get; private set; }
    public string HotkeyStatus { get; private set; }="";
    public bool Prepared { get; private set; }
    private double lastTick,nextSave=30,saveDue=double.PositiveInfinity;
    private bool dragging,pressing,disposed,regionDirty=true;
    private Native.PointI down;
    private Native.RectI dragStart;
    private int frameIndex;
    private (int X,int Y,int Size) lastPlacement=(int.MinValue,int.MinValue,0);
    private StatusWindow? status;
    private SettingsWindow? settingsWindow;
    private ContextMenu? currentMenu;
    private bool DisplayFlip=>Engine.FaceRight&&Engine.Current is not (Behavior.Idle or Behavior.AfkRest);
    public DesktopPetWindow(StateStore store,Settings settings,GameSave pet,bool test=false)
    {
        Store=store;Settings=settings;Pet=pet;Title="赛博金毛";
        Width=Height=settings.Size;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
        Topmost=settings.Topmost;Focusable=false;Opacity=0;
        surface.Children.Add(PetImage);
        Content=new Viewbox {Stretch=Stretch.Fill,Child=surface};
        RenderOptions.SetBitmapScalingMode(PetImage,BitmapScalingMode.HighQuality);
        Engine=new BehaviorEngine(settings,pet);
        Player=new AnimationPlayer(PetImage,pet,store);Audio=new LocalAudio(settings,store);
        Engine.SoundRequested+=Audio.Play;
        Engine.DataChanged+=()=>{Dirty();UpdateBubble();status?.Refresh();};
        Engine.CriticalStateChanged+=Save;
        Engine.Changed+=OnBehaviorChanged;
        Player.FrameChanged+=index=>{frameIndex=index;regionDirty=true;};
        ReadMouths();
        toy=new ToyWindow(()=>Engine.CancelCommand());
        tray=new Forms.NotifyIcon {Text="赛博金毛",Icon=new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory,"pet.ico")),Visible=!test};
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{if(Settings.Hidden)Engine.ToggleHidden();else ShowStatus();});
        MakeTray();
        SourceInitialized+=(_,_)=>InitializeNative();
        Loaded+=async(_,_)=>await InitializeAnimations();
        MouseLeftButtonDown+=Down;MouseMove+=Move;MouseLeftButtonUp+=Up;
        LostMouseCapture+=(_,_)=>{if(dragging){dragging=false;pressing=false;Engine.EndDrag();}};
        MouseRightButtonUp+=(_,e)=>{OpenMenu();e.Handled=true;};
        AllowDrop=true;PreviewDragOver+=FolderOver;PreviewDrop+=FolderDrop;
        timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(33)};
        timer.Tick+=(_,_)=>Tick();
        Closing+=(_,_)=>Cleanup();
    }
    private void ReadMouths()
    {
        try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets/pet/manifest.json")));
            foreach(var entry in doc.RootElement.GetProperty("actions").EnumerateObject())
            {
                var anchors=new System.Collections.Generic.List<double[]>();
                foreach(var point in entry.Value.GetProperty("mouth").EnumerateArray())anchors.Add(new[]{point[0].GetDouble(),point[1].GetDouble()});
                mouths[entry.Name]=anchors.ToArray();
            }
        } catch(Exception e){Store.Log("mouth anchors",e);}
    }
    private void InitializeNative()
    {
        Handle=new WindowInteropHelper(this).Handle;Native.NoActivate(Handle,!ShowInTaskbar);
        source=HwndSource.FromHwnd(Handle);source.AddHook(Hook);
        var screen=Forms.Screen.PrimaryScreen!;
        if(!string.IsNullOrEmpty(Settings.Monitor))screen=Native.ScreenAt(Settings.X,Settings.Y);
        var area=screen.WorkingArea;
        Engine.X=string.IsNullOrEmpty(Settings.Monitor)?area.Right-220:Settings.X;
        Engine.Y=string.IsNullOrEmpty(Settings.Monitor)?area.Bottom-210:Settings.Y;
        Native.SetWindowPos(Handle,IntPtr.Zero,(int)Engine.X,(int)Engine.Y,0,0,0x0015);
        UpdateBounds();Place();RegisterShortcut();
    }
    private async Task InitializeAnimations()
    {
        try
        {
            await Player.Prepare();Prepared=true;
            UpdateBounds();Engine.Start(Native.IdleSeconds());
            if(Settings.Hidden)Hide();else Opacity=1;
            timer.Start();
        }
        catch(Exception e)
        {
            Store.Log("startup",e);
            HotkeyStatus="动画加载失败，请查看诊断日志";
            tray.Text="赛博金毛：资源加载失败";
            // A failed resource never invokes an upstream character fallback.
        }
    }
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x0021){handled=true;return new IntPtr(3);} // MA_NOACTIVATE
        if(message==0x0312&&wParam.ToInt32()==7001){Engine.ToggleHidden();handled=true;}
        if(message==0x0084)
        {
            var raw=lParam.ToInt64();var x=(short)(raw&0xffff);var y=(short)((raw>>16)&0xffff);
            Native.GetWindowRect(hwnd,out var rect);
            var nx=(x-rect.Left)/(double)Math.Max(1,rect.Width);var ny=(y-rect.Top)/(double)Math.Max(1,rect.Height);
            if(DisplayFlip)nx=1-nx;
            handled=true;return new IntPtr(Player.Mask.Hit(nx,ny)?1:-1);
        }
        if(message is 0x02E0 or 0x007E or 0x001A)
            Dispatcher.BeginInvoke(new Action(()=>{UpdateBounds();Place();regionDirty=true;}));
        return IntPtr.Zero;
    }
    public void RegisterShortcut()
    {
        Native.UnregisterHotKey(Handle,7001);
        var hotkey=Native.ParseHotkey(Settings.Hotkey);
        HotkeyStatus=hotkey==null?"格式无效，例如 Ctrl+Alt+D":Native.RegisterHotKey(Handle,7001,hotkey.Value.Modifiers,hotkey.Value.Key)?"快捷键已注册":"快捷键被占用；请修改，或使用托盘入口";
        Dirty();
    }
    private void UpdateBounds()
    {
        if(Handle==IntPtr.Zero)return;
        Native.GetWindowRect(Handle,out var rect);
        var screen=Native.ScreenAt(rect.Left,rect.Top,rect.Width);
        var area=screen.WorkingArea;
        var dpi=Native.DpiScale(Handle);
        Engine.WorkTop=area.Top;Engine.WorkBottom=area.Bottom;
        Engine.RebaseWorkspace(area.Left,area.Right,Settings.Size*dpi,dpi);
        Engine.Y=Math.Clamp(Engine.Y,area.Top+8*Engine.DpiScale,Math.Max(area.Top+8*Engine.DpiScale,area.Bottom-Engine.PixelSize-8*Engine.DpiScale));
        Settings.Monitor=screen.DeviceName;
        Width=Height=Settings.Size;
    }
    private void Place()
    {
        if(Handle==IntPtr.Zero)return;
        var next=((int)Math.Round(Engine.X),(int)Math.Round(Engine.Y),(int)Math.Round(Engine.PixelSize));
        if(next==lastPlacement)return;
        lastPlacement=next;
        Native.SetWindowPos(Handle,IntPtr.Zero,next.Item1,next.Item2,next.Item3,next.Item3,0x0014);
    }
    private void OnBehaviorChanged()
    {
        if(!Prepared||disposed)return;
        timer.Interval=TimeSpan.FromMilliseconds(Settings.StaticCompanion&&Engine.Current==Behavior.Idle&&!Engine.Manual?200:33);
        if(Settings.Hidden)
        {if(currentMenu!=null)currentMenu.IsOpen=false;Player.Stop();Hide();toy.Hide();HungerText.Hide();status?.Close();settingsWindow?.Close();}
        else
        {
            if(!IsVisible){Show();Opacity=1;}
            if(Engine.Current is Behavior.Idle or Behavior.AfkRest)
            {Player.ShowPose(Settings.IdlePose);}
            else if(Settings.Paused&&!Engine.Manual)
            {Player.Play(Settings.Sleeping?"sleep":"idle",false);}
            else
            {
                var loop=Engine.Current is Behavior.Sleep or Behavior.Walk or Behavior.Trot or Behavior.HungerMove or Behavior.AfkApproach or Behavior.ChaseToy or Behavior.ReturnToy or Behavior.Wag or Behavior.Welcome or Behavior.Expect or Behavior.Eat or Behavior.Drink;
                Player.Play(Engine.AnimationName,loop,Engine.Current==Behavior.Drop);
            }
            PetImage.RenderTransform=new ScaleTransform(DisplayFlip?-1:1,1,250,250);
            regionDirty=true;
        }
        UpdateToy();UpdateBubble();Dirty();status?.Refresh();MakeTray();
    }
    private void Tick()
    {
        var now=elapsed.Elapsed.TotalSeconds;var dt=now-lastTick;lastTick=now;
        var cursor=new Native.PointI {X=-9999,Y=-9999};
        if(!Settings.StaticCompanion||Engine.Manual||Engine.Current is Behavior.Walk or Behavior.Trot or Behavior.HungerMove)Native.GetCursorPos(out cursor);
        Engine.Tick(dt,Native.IdleSeconds(),cursor.X,cursor.Y);
        if(!Settings.Hidden)
        {
            Place();
            if(regionDirty&&Player.Mask.Ready)
            {Native.ApplyRegion(Handle,Player.Mask,(int)Math.Round(Engine.PixelSize),(int)Math.Round(Engine.PixelSize),DisplayFlip);regionDirty=false;}
            UpdateToy();
            UpdateBubble();
        }
        if(now>=nextSave||now>=saveDue)
        {Save();nextSave=now+30;saveDue=double.PositiveInfinity;status?.Refresh();}
    }
    private void UpdateBubble(){if(!disposed&&Prepared)HungerText.Follow(Engine);}
    public bool AcceptFolder(string path,string? requestId=null)
    {return FolderFeeder.IsOrdinaryLocalEmptyFolder(path)&&Engine.Feed(requestId);}
    private static string? DroppedPath(DragEventArgs e)
    {return e.Data.GetDataPresent(DataFormats.FileDrop)&&e.Data.GetData(DataFormats.FileDrop) is string[] files&&files.Length==1?files[0]:null;}
    private void FolderOver(object sender,DragEventArgs e)
    {
        var path=DroppedPath(e);
        var valid=Hit(e.GetPosition(this))&&FolderFeeder.IsOrdinaryLocalEmptyFolder(path);
        e.Effects=valid?((e.AllowedEffects&DragDropEffects.Link)!=0?DragDropEffects.Link:DragDropEffects.Copy):DragDropEffects.None;
        e.Handled=true;
    }
    private void FolderDrop(object sender,DragEventArgs e)
    {
        var path=DroppedPath(e);
        var accepted=path!=null&&Hit(e.GetPosition(this))&&AcceptFolder(path,dropRequests.GetValue(e,_=>new DropRequest()).Id);
        e.Effects=accepted?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;
    }
    private void UpdateToy()
    {
        if(!Engine.ToyVisible||Settings.Hidden){toy.Hide();return;}
        var x=Engine.ToyX;var y=Engine.ToyY;
        var carried=Engine.ToyCarried;
        var pickupProgress=Engine.Current==Behavior.Pickup&&frameIndex>=3;
        if(carried||pickupProgress)
        {
            var mouth=mouths.TryGetValue(Player.ActiveName,out var sequence)?sequence[Math.Clamp(frameIndex,0,sequence.Length-1)]:new[]{.25,.4};
            x=Engine.X+Engine.PixelSize*(Engine.FaceRight?1-mouth[0]:mouth[0]);
            y=Engine.Y+Engine.PixelSize*mouth[1];
        }
        toy.Display(Engine.ToyKind,x,y,Engine.DpiScale);
    }
    private bool Hit(System.Windows.Point p)
    {var x=p.X/ActualWidth;if(DisplayFlip)x=1-x;return Player.Mask.Hit(x,p.Y/ActualHeight);}
    private void Down(object sender,MouseButtonEventArgs e)
    {
        if(!Hit(e.GetPosition(this)))return;
        Engine.OpenMenu(false);Engine.CancelCommand();Native.GetCursorPos(out down);Native.GetWindowRect(Handle,out dragStart);
        pressing=true;CaptureMouse();e.Handled=true;
    }
    private void Move(object sender,MouseEventArgs e)
    {
        if(!pressing||e.LeftButton!=MouseButtonState.Pressed)return;
        Native.GetCursorPos(out var cursor);
        if(!dragging&&Math.Abs(cursor.X-down.X)+Math.Abs(cursor.Y-down.Y)>6*Engine.DpiScale){dragging=true;Engine.BeginDrag();}
        if(dragging)
        {
            Engine.X=dragStart.Left+cursor.X-down.X;Engine.Y=dragStart.Top+cursor.Y-down.Y;
            Place();UpdateBounds();Place();regionDirty=true;Dirty();
        }
    }
    private void Up(object sender,MouseButtonEventArgs e)
    {
        if(!pressing)return;pressing=false;
        if(dragging){dragging=false;Engine.EndDrag();Dirty();}
        else
        {
            Native.GetCursorPos(out var cursor);
            if(e.GetPosition(this).Y/ActualHeight<.57)Engine.Command(Behavior.Pet,cursor.X);
            else ShowStatus();
        }
        ReleaseMouseCapture();e.Handled=true;
    }
    private void OpenMenu()
    {
        Engine.OpenMenu(true);
        var menu=new ContextMenu {PlacementTarget=this};
        currentMenu=menu;
        void Add(string text,Action action){var item=new MenuItem {Header=text};item.Click+=(_,_)=>{Engine.OpenMenu(false);action();};menu.Items.Add(item);}
        Add("摸摸头",()=>Engine.Command(Behavior.Pet));Add("喂食",()=>Engine.Command(Behavior.Eat));Add("喝水",()=>Engine.Command(Behavior.Drink));
        Add("握手",()=>Engine.Command(Behavior.Shake));Add("玩网球",()=>Engine.Fetch("ball"));Add("玩小玩偶",()=>Engine.Fetch("plush"));
        Add("开心蹦跳",()=>Engine.Command(Behavior.Jump));
        Add("期待撒娇",()=>{Native.GetCursorPos(out var cursor);Engine.Command(Behavior.Expect,cursor.X);});
        var extra=new MenuItem {Header="其他小动作"};
        foreach(var (label,action) in new[]{("歪头",Behavior.Tilt),("舔鼻子",Behavior.Lick),("打哈欠",Behavior.Yawn),("伸懒腰",Behavior.Stretch),("摇尾巴",Behavior.Wag),("坐下观察",Behavior.Sit),("趴下休息",Behavior.Lie)})
        {var item=new MenuItem {Header=label};item.Click+=(_,_)=>{Engine.OpenMenu(false);Engine.Command(action);};extra.Items.Add(item);}
        menu.Items.Add(extra);
        Add(Settings.Sleeping?"叫醒":"睡觉",()=>Engine.Command(Settings.Sleeping?Behavior.Idle:Behavior.Sleep));
        Add("取消当前动作",Engine.CancelCommand);menu.Items.Add(new Separator());
        Add("状态",ShowStatus);Add("设置 / 改名 / 大小 / 声音",ShowSettings);
        Add(Settings.StaticCompanion?"关闭静止陪伴":"开启静止陪伴",()=>{Settings.StaticCompanion=!Settings.StaticCompanion;ApplySettings();});
        Add(Settings.Paused?"恢复自动行为":"暂停自动行为",Engine.TogglePause);
        Add("隐藏  "+Settings.Hotkey,Engine.ToggleHidden);Add("退出",Close);
        menu.Closed+=(_,_)=>{if(ReferenceEquals(currentMenu,menu))currentMenu=null;Engine.OpenMenu(false);};
        menu.Opened+=(_,_)=>{if(PresentationSource.FromVisual(menu) is HwndSource popup)Native.NoActivate(popup.Handle);};
        menu.IsOpen=true;
    }
    private void MakeTray()
    {
        var old=tray.ContextMenuStrip;
        var menu=new Forms.ContextMenuStrip();
        void Add(string text,Action action)=>menu.Items.Add(text,null,(_,_)=>Dispatcher.Invoke(action));
        Add(Settings.Hidden?"显示赛博金毛":"隐藏赛博金毛",Engine.ToggleHidden);
        Add(Settings.Paused?"恢复自动行为":"暂停自动行为",Engine.TogglePause);
        Add("状态",ShowStatus);Add("设置",ShowSettings);
        Add(Settings.Sleeping?"叫醒":"睡觉",()=>Engine.Command(Settings.Sleeping?Behavior.Idle:Behavior.Sleep));
        Add("退出",Close);tray.ContextMenuStrip=menu;
        // A clicked menu can still be dispatching; dispose on the following dispatcher turn.
        if(old!=null)Dispatcher.BeginInvoke(new Action(old.Dispose));
    }
    public void ShowStatus()
    {
        Engine.OpenMenu(true);
        if(status!=null){status.Close();return;}
        status=new StatusWindow(this);status.Closed+=(_,_)=>{status=null;Engine.OpenMenu(false);};status.Show();
    }
    public void ShowSettings()
    {
        Engine.OpenMenu(true);
        if(settingsWindow!=null){settingsWindow.Activate();return;}
        settingsWindow=new SettingsWindow(this);
        settingsWindow.Closed+=(_,_)=>{settingsWindow=null;Engine.OpenMenu(false);};settingsWindow.Show();
    }
    public void ApplySettings()
    {
        if(disposed)return;
        Settings.Normalize();Pet.Name=Settings.Name;Topmost=Settings.Topmost;
        UpdateBounds();Place();regionDirty=true;Audio.Update();RegisterShortcut();Dirty();
        Engine.SettingsUpdated();
    }
    public void SetStartup(bool enabled)
    {
        try
        {
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(enabled)key.SetValue("CyberGolden",'"'+Environment.ProcessPath+'"');else key.DeleteValue("CyberGolden",false);
            Settings.Startup=enabled;
        }catch(Exception e){Store.Log("startup setting",e);}
    }
    public void Dirty(){saveDue=elapsed.Elapsed.TotalSeconds+2;}
    public void Save(){Settings.X=Engine.X;Settings.Y=Engine.Y;Store.Save(Settings,Pet);}
    public void CaptureFrame(string path,double size=160,double dpi=96)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Round(size*dpi/96),(int)Math.Round(size*dpi/96),dpi,dpi,PixelFormats.Pbgra32);
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen())dc.DrawImage(PetImage.Source,new Rect(0,0,size,size));
        bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(path);encoder.Save(stream);
    }
    private void Cleanup()
    {
        if(disposed)return;disposed=true;timer.Stop();if(currentMenu!=null)currentMenu.IsOpen=false;Save();Native.UnregisterHotKey(Handle,7001);
        source?.RemoveHook(Hook);Audio.Dispose();Player.Dispose();toy.Close();HungerText.Close();status?.Close();settingsWindow?.Close();tray.Visible=false;tray.Dispose();
    }
}
