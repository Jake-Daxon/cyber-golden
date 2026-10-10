using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace CyberGolden;
public sealed class StatusWindow : Window
{
    private readonly DesktopPetWindow pet;
    private readonly TextBlock description=new() {FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,14)};
    private readonly StackPanel values=new();
    public StatusWindow(DesktopPetWindow owner)
    {
        pet=owner;Title="金毛状态";Width=310;Height=370;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;FontFamily=new FontFamily("Microsoft YaHei UI");
        var root=new StackPanel {Margin=new Thickness(24)};
        root.Children.Add(new TextBlock {Text="陪在你身边",FontSize=22,FontWeight=FontWeights.SemiBold});
        root.Children.Add(description);root.Children.Add(values);
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,18,0,0)};
        var setting=new Button {Content="设置",MinWidth=100,Padding=new Thickness(12,6,12,6)};setting.Click+=(_,_)=>{Close();pet.ShowSettings();};
        var close=new Button {Content="关闭",MinWidth=100,Margin=new Thickness(12,0,0,0),Padding=new Thickness(12,6,12,6)};close.Click+=(_,_)=>Close();
        buttons.Children.Add(setting);buttons.Children.Add(close);root.Children.Add(buttons);Content=root;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.NoActivate(h);HwndSource.FromHwnd(h).AddHook((IntPtr hw,int m,IntPtr w,IntPtr l,ref bool done)=>{if(m==0x21){done=true;return new IntPtr(3);}return IntPtr.Zero;});};
        Refresh();
    }
    public void Refresh()
    {
        description.Text=pet.Settings.Name+" · "+pet.Engine.StatusText;
        values.Children.Clear();
        foreach(var (name,value) in new[]{("饱食度",pet.Pet.StrengthFood),("水分",pet.Pet.StrengthDrink),("精力",pet.Pet.Strength),("心情",pet.Pet.Feeling),("亲密度",pet.Pet.Likability)})
        {
            var row=new Grid {Margin=new Thickness(0,0,0,12)};
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(72)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});
            var label=new TextBlock {Text=name,VerticalAlignment=VerticalAlignment.Center};var bar=new ProgressBar {Minimum=0,Maximum=100,Value=value,Height=7,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(93,133,170))};
            var number=new TextBlock {Text=Math.Round(value).ToString(),HorizontalAlignment=HorizontalAlignment.Right};
            Grid.SetColumn(bar,1);Grid.SetColumn(number,2);row.Children.Add(label);row.Children.Add(bar);row.Children.Add(number);values.Children.Add(row);
        }
    }
}
public sealed class SettingsWindow : Window
{
    private readonly DesktopPetWindow pet;
    private readonly TextBlock shortcutStatus=new() {TextWrapping=TextWrapping.Wrap,FontSize=12};
    public SettingsWindow(DesktopPetWindow owner)
    {
        pet=owner;Title="赛博金毛 · 设置（0.4.1 体验版）";Width=420;Height=630;MinWidth=380;MinHeight=480;
        FontFamily=new FontFamily("Microsoft YaHei UI");ShowInTaskbar=false;ShowActivated=false;
        // Explicitly opened settings can accept text focus on click; animation/menu never activates.
        var root=new StackPanel {Margin=new Thickness(24)};
        root.Children.Add(new TextBlock {Text="你的金毛，你的节奏",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
        root.Children.Add(new TextBlock {Text="名字",Margin=new Thickness(0,0,0,4)});
        var name=new TextBox {Text=pet.Settings.Name,MaxLength=24,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)};
        name.TextChanged+=(_,_)=>{pet.Settings.Name=name.Text;pet.Dirty();};root.Children.Add(name);
        root.Children.Add(new TextBlock {Text="待机姿势",Margin=new Thickness(0,2,0,4)});
        var pose=new ComboBox {Margin=new Thickness(0,0,0,6),Padding=new Thickness(6)};
        pose.Items.Add(new ComboBoxItem {Content="正面静坐 · 双爪落地（握手形象）",Tag="FrontSit"});
        pose.Items.Add(new ComboBoxItem {Content="自然四足站立",Tag="Stand"});
        pose.Items.Add(new ComboBoxItem {Content="安静趴下（默认）",Tag="Lie"});
        pose.SelectedIndex=pet.Settings.IdlePose=="Stand"?1:pet.Settings.IdlePose=="Lie"?2:0;
        pose.SelectionChanged+=(_,_)=>{if(pose.SelectedItem is ComboBoxItem item){pet.Settings.IdlePose=(string)item.Tag;pet.ApplySettings();}};root.Children.Add(pose);
        root.Children.Add(new TextBlock {Text="待机不循环；漫游按独立 CD 开启。互动后回所选姿势，饥饿提醒除外。",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        root.Children.Add(new TextBlock {Text="显示大小（96～240 DIP）"});
        var size=new Slider {Minimum=96,Maximum=240,Value=pet.Settings.Size,TickFrequency=8,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,0,10)};
        size.ValueChanged+=(_,_)=>{pet.Settings.Size=size.Value;pet.ApplySettings();};root.Children.Add(size);
        void Check(string text,bool initial,Action<bool> change)
        {
            var box=new CheckBox {Content=text,IsChecked=initial,Margin=new Thickness(0,5,0,5)};
            box.Checked+=(_,_)=>{change(true);pet.ApplySettings();};box.Unchecked+=(_,_)=>{change(false);pet.ApplySettings();};root.Children.Add(box);
        }
        Check("置顶显示",pet.Settings.Topmost,v=>pet.Settings.Topmost=v);
        Check("允许自主散步",pet.Settings.AutoMove,v=>pet.Settings.AutoMove=v);
        Check("安静模式",pet.Settings.Quiet,v=>pet.Settings.Quiet=v);
        root.Children.Add(new TextBlock {Text="安静模式小狗不会叫，不会自由漫步",FontSize=12,Foreground=Brushes.Gray,Margin=new Thickness(0,0,0,8)});
        Check("播放互动声音",pet.Settings.Sound,v=>pet.Settings.Sound=v);
        root.Children.Add(new TextBlock {Text="音量（不修改系统音量）",Margin=new Thickness(0,8,0,0)});
        var volume=new Slider {Minimum=0,Maximum=100,Value=pet.Settings.Volume,Margin=new Thickness(0,6,0,10)};
        volume.ValueChanged+=(_,_)=>{pet.Settings.Volume=volume.Value;pet.Audio.Update();pet.Dirty();};root.Children.Add(volume);
        Check("睡眠轻呼吸声",pet.Settings.SleepSound,v=>pet.Settings.SleepSound=v);
        Check("饥饿提醒（每轮轻叫两声，可关闭）",pet.Settings.HungerSound,v=>pet.Settings.HungerSound=v);
        root.Children.Add(new TextBlock {Text="自主漫游 CD（秒，5～1000）",Margin=new Thickness(0,10,0,4)});
        var roam=new TextBox {Text=pet.Settings.RoamSeconds.ToString(),Padding=new Thickness(6),MaxLength=4};
        roam.LostFocus+=(_,_)=>{if(int.TryParse(roam.Text,out var value)){pet.Settings.RoamSeconds=value;pet.ApplySettings();}roam.Text=pet.Settings.RoamSeconds.ToString();};root.Children.Add(roam);
        Check("登录 Windows 时启动（默认关闭）",pet.Settings.Startup,pet.SetStartup);
        root.Children.Add(new TextBlock {Text="隐藏 / 恢复快捷键",Margin=new Thickness(0,12,0,4)});
        var hotkey=new TextBox {Text=pet.Settings.Hotkey,Padding=new Thickness(6),MaxLength=32};root.Children.Add(hotkey);
        var register=new Button {Content="应用快捷键",Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,6,0,6)};
        register.Click+=(_,_)=>{pet.Settings.Hotkey=hotkey.Text;pet.RegisterShortcut();shortcutStatus.Text=pet.HotkeyStatus;};root.Children.Add(register);
        shortcutStatus.Text=pet.HotkeyStatus;root.Children.Add(shortcutStatus);
        root.Children.Add(new TextBlock {Text="所有数据保存在当前用户应用数据目录。\nVPet Core · Apache-2.0。声音署名及修改见 NOTICE。",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,14)});
        var close=new Button {Content="保存并关闭",Padding=new Thickness(12,8,12,8)};
        close.Click+=(_,_)=>{pet.ApplySettings();pet.Save();Close();};root.Children.Add(close);
        root.Children.Add(new TextBlock {Text="联系创作者可定制萌宠",FontSize=11,Foreground=Brushes.Gray,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,16,0,0)});
        Content=new ScrollViewer {Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        Closing+=(_,_)=>{pet.ApplySettings();pet.Save();};
    }
}
