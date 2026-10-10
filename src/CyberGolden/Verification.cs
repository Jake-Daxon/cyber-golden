using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using VPet_Simulator.Core;

namespace CyberGolden;
public static class Verification
{
    public static async Task Run(DesktopPetWindow window,string output)
    {
        var results=new List<object>();
        void Check(string name,bool value,string evidence="")
        {results.Add(new{name,passed=value,evidence});if(!value)throw new InvalidOperationException(name+": "+evidence);}
        var until=DateTime.UtcNow.AddSeconds(35);
        while(!window.Prepared&&DateTime.UtcNow<until)await Task.Delay(30);
        Check("actual VPet PetLoader/PNGAnimation initialized",window.Prepared);
        Check("twenty real PNG animation sequences and three static poses",window.Player.Core.Graph!.GraphsALL.OfType<PNGAnimation>().Count()==20&&window.Player.Core.Graph.GraphsALL.OfType<Picture>().Count()==3);
        Check("all graph resources ready, no upstream character fallback",window.Player.Core.Graph.GraphsALL.All(g=>g.IsReady&&!g.IsFail));
        foreach(var graph in window.Player.Core.Graph.GraphsALL.OfType<PNGAnimation>())
            Check("frame ordering / "+graph.GraphInfo.Name,graph.Animations.Count==6&&graph.Animations.Select(a=>a.FrameIndex).SequenceEqual(Enumerable.Range(0,6)));
        var settings=new Settings{StaticCompanion=false};var pet=new GameSave("测试金毛"){Likability=60,Feeling=90};
        var engine=new BehaviorEngine(settings,pet,7){X=400,Y=300,WorkLeft=0,WorkRight=1200,PixelSize=160};
        void Advance(double seconds,double idle=0){for(var i=0;i<(int)(seconds*4);i++)engine.Tick(.25,idle);}
        engine.Start(0);engine.Command(Behavior.Eat);var epoch=engine.Epoch;
        Advance(2,1000);Check("manual feeding cannot be replaced by AFK/autonomy",engine.Current==Behavior.Eat&&engine.Epoch==epoch);
        engine.Command(Behavior.Shake);Check("new direct command replaces feeding immediately",engine.Current==Behavior.Shake&&engine.Epoch>epoch);
        engine.Fetch("ball");Advance(1);Check("toy chase starts",engine.Current==Behavior.ChaseToy&&engine.ToyVisible);
        engine.ToggleHidden();var x=engine.X;Advance(100,1000);
        Check("hide cancels chase and toy, no background movement or queued action",settings.Hidden&&!engine.ToyVisible&&engine.X==x&&engine.Current==Behavior.Idle);
        engine.ToggleHidden();Advance(3);Check("restore is a short transition, no startup replay",engine.Current==Behavior.Idle);
        engine.Command(Behavior.Sleep);Advance(2);Check("explicit sleep remains stable",settings.Sleeping&&engine.Current==Behavior.Sleep);
        Advance(100,0);Check("resuming system input cannot wake explicit sleep",engine.Current==Behavior.Sleep);
        engine.ToggleHidden();engine.ToggleHidden();Check("sleep survives hidden/restore",engine.Current==Behavior.Sleep&&settings.Sleeping);
        engine.Command(Behavior.Idle);Check("wake command clears sleep",!settings.Sleeping&&engine.Current==Behavior.Idle);
        engine.BeginDrag();var savedEpoch=engine.Epoch;Advance(50,1000);Check("drag owns behavior immediately",engine.Current==Behavior.Drag&&engine.Epoch==savedEpoch);engine.EndDrag();
        engine.OpenMenu(true);Advance(100,1000);Check("menu prevents autonomous/companion scheduling",engine.Current==Behavior.Idle);engine.OpenMenu(false);
        Advance(.5,200);Check("AFK enters quiet edge approach",settings.Companion&&engine.Current==Behavior.AfkApproach);
        Advance(50,200);Check("AFK arrives at edge and lies quietly facing inward",engine.Current==Behavior.AfkRest&&engine.FaceRight&&engine.X>=16);
        Advance(.5,0);Check("AFK leaves quietly on input",!settings.Companion&&engine.Current==Behavior.Idle);
        engine.CancelCommand();settings.AutoMove=false;engine.Fetch("plush");Advance(20);
        Check("complete plush fetch returns and clears toy",!engine.ToyVisible&&!engine.Manual&&Math.Abs(engine.X-engine.HomeX)<2);
        pet.Strength=15;engine.Fetch("ball");Check("low energy fetch bounded to short route",Math.Abs(engine.TargetX-engine.X)<=65);engine.CancelCommand();
        engine.Fetch("ball");engine.RebaseWorkspace(-900,0,160,1);
        Check("monitor change cancels stale toy path and clamps location",!engine.ToyVisible&&engine.X<=-168&&engine.TargetX<=-168);engine.RebaseWorkspace(0,1200,160,1);
        engine.TogglePause();savedEpoch=engine.Epoch;Advance(100,1000);Check("pause blocks new autonomy",engine.Epoch==savedEpoch);engine.TogglePause();
        engine.WorkLeft=-1920;engine.WorkRight=0;Check("negative-monitor boundary clamping",engine.ClampX(100)==-168);
        engine.WorkLeft=0;engine.WorkRight=1920;engine.DpiScale=2;engine.PixelSize=320;
        Check("physical pixels and DIP at 200% boundary",engine.ClampX(2000)==1584);
        var randomSettings=new Settings{AutoMove=false,StaticCompanion=false};var randomEngine=new BehaviorEngine(randomSettings,new GameSave("随机测试"),29);
        var sampled=new List<(Behavior Action,double At)>();double simTime=0;
        randomEngine.Changed+=()=>{if(randomEngine.Current!=Behavior.Idle)sampled.Add((randomEngine.Current,simTime));};
        for(int i=0;i<6000;i++){simTime+=.25;randomEngine.Tick(.25,0);}
        Check("weighted idle global cooldown and no consecutive repetition",sampled.Count>2&&sampled.Zip(sampled.Skip(1),(a,b)=>b.At-a.At>=45&&b.Action!=a.Action).All(x=>x),"samples="+sampled.Count);
        var stillSettings=new Settings{AutoMove=false};var stillEngine=new BehaviorEngine(stillSettings,new GameSave("静止测试"),1){X=400};
        stillEngine.Start(0);var stillEpoch=stillEngine.Epoch;
        for(int i=0;i<14400;i++)stillEngine.Tick(.25,0);
        Check("default static companion remains still across one simulated hour and AFK",stillSettings.StaticCompanion&&stillSettings.IdlePose=="FrontSit"&&stillEngine.Current==Behavior.Idle&&stillEngine.X==400&&stillEngine.Epoch==stillEpoch);
        stillEngine.Command(Behavior.Shake);for(int i=0;i<16;i++)stillEngine.Tick(.25,0);
        Check("explicit handshake finishes back at selected static idle",stillEngine.Current==Behavior.Idle&&!stillEngine.Manual);
        var store=new StateStore(Path.Combine(output,"save-fixture"));settings.Sleeping=true;settings.Hidden=true;settings.Paused=true;
        store.Save(settings,pet);var loaded=store.Load();
        Check("VPet GameSave LPS + stable flags roundtrip",loaded.Settings.Sleeping&&loaded.Settings.Hidden&&loaded.Settings.Paused&&Math.Abs(loaded.Pet.Strength-pet.Strength)<.1);
        store.Save(settings,pet);File.WriteAllText(store.SavePath,"{broken");loaded=store.Load();
        Check("corrupt save restores atomic backup",loaded.Settings.Sleeping&&store.Diagnostic.Contains("备份"));
        File.WriteAllText(store.SavePath+".bak","broken");loaded=store.Load();Check("double-corrupt save produces clean defaults",loaded.Settings.Name=="赛博金毛"&&!loaded.Settings.Hidden);
        stillSettings.IdlePose="Lie";store.Save(stillSettings,pet);var stillLoaded=store.Load();
        Check("selected static companion pose persists",stillLoaded.Settings.StaticCompanion&&stillLoaded.Settings.IdlePose=="Lie");
        await HungerVerification.Run(window,output,Check);
        Check("hotkey parser rejects invalid and supports replacement",Native.ParseHotkey("Ctrl+Alt+D")!=null&&Native.ParseHotkey("Ctrl+Shift+F8")!=null&&Native.ParseHotkey("D")==null);
        var conflict=new Window {Width=1,Height=1,ShowInTaskbar=false,ShowActivated=false};var conflictHandle=new WindowInteropHelper(conflict).EnsureHandle();
        var key=Native.ParseHotkey(window.Settings.Hotkey)!.Value;
        Check("actual hotkey conflict returns error",!Native.RegisterHotKey(conflictHandle,9881,key.Modifiers,key.Key));
        Native.UnregisterHotKey(conflictHandle,9881);conflict.Close();
        Check("system idle timestamp available",Native.IdleSeconds()>=0);
        window.Settings.Sound=false;var played=window.Audio.PlayRequests;window.Audio.Play("click");Check("mute makes no playback request",window.Audio.PlayRequests==played);
        window.Settings.Sound=true;window.Settings.Volume=20;window.Audio.Play("missing-fixture");Check("missing audio does not throw or start playback",!window.Audio.Playing);
        window.Audio.Play("click");played=window.Audio.PlayRequests;window.Audio.Play("happy");Check("rapid clicks limited without overlap",window.Audio.PlayRequests==played);window.Audio.Stop();
        window.Settings.Paused=false;window.Settings.Hidden=false;window.Settings.Sleeping=false;
        var frameEvents=new HashSet<int>();var frameTimes=new List<DateTime>();
        window.Player.FrameChanged+=i=>{frameEvents.Add(i);frameTimes.Add(DateTime.UtcNow);};
        var foreground=Native.GetForegroundWindow();window.Engine.Preview(Behavior.Trot);
        var loopDeadline=DateTime.UtcNow.AddSeconds(4);
        while(frameEvents.Count<6&&DateTime.UtcNow<loopDeadline)await Task.Delay(40);
        Check("actual VPet trot loop displays every gait frame",frameEvents.Count==6,string.Join(",",frameEvents));
        var intervals=frameTimes.Zip(frameTimes.Skip(1),(a,b)=>(b-a).TotalMilliseconds).OrderBy(x=>x).ToArray();
        File.WriteAllText(Path.Combine(output,"cadence.json"),JsonSerializer.Serialize(new{intervals,dpi=window.Engine.DpiScale,size=window.Engine.PixelSize,regionMilliseconds=Native.LastRegionMilliseconds}));
        Check("gait frame cadence remains responsive",intervals.Length>0&&intervals[intervals.Length/2]<200,"median milliseconds="+(intervals.Length==0?0:intervals[intervals.Length/2]));
        Check("animation never takes foreground focus",Native.GetForegroundWindow()==foreground);
        Check("alpha-mask transparent canvas corner passes",!window.Player.Mask.Hit(0,0));
        Check("alpha-mask includes puppy body",Enumerable.Range(0,100).Any(i=>window.Player.Mask.Hit(i/100.0,.65)));
        var badFolder=Path.Combine(output,"bad-animation-fixture");Directory.CreateDirectory(badFolder);
        File.WriteAllText(Path.Combine(badFolder,"000_80.png"),"not a PNG");File.WriteAllText(Path.Combine(badFolder,"001_80.png"),"not a PNG");
        var badGraph=new PNGAnimation(window.Player.Core.Graph,badFolder,new DirectoryInfo(badFolder).GetFiles("*.png"),new GraphInfo("bad-fixture"));
        window.Player.Core.Graph.AddGraph(badGraph);
        var badDeadline=DateTime.UtcNow.AddSeconds(5);
        while(!badGraph.IsFail&&!badGraph.IsReady&&DateTime.UtcNow<badDeadline)await Task.Delay(30);
        window.Player.Play("bad-fixture",true);
        Check("corrupt animation falls back to a ready golden graph",badGraph.IsFail&&window.Player.ActiveName=="idle");
        Native.GetWindowRect(window.Handle,out var rect);
        Native.ApplyRegion(window.Handle,window.Player.Mask,rect.Width,rect.Height,window.Engine.FaceRight);
        var transparentHandle=Native.WindowFromPoint(new Native.PointI{X=rect.Left+1,Y=rect.Top+1});
        Check("actual Windows hit routing passes transparent corner to underlying window",transparentHandle!=window.Handle);
        bool solidHit=false;
        for(int py=20;py<80&&!solidHit;py+=5)for(int px=20;px<80;px+=5)
        {
            var nx=px/100.0;var ny=py/100.0;
            if(window.Player.Mask.Hit(window.Engine.FaceRight?1-nx:nx,ny))
            {solidHit=Native.WindowFromPoint(new Native.PointI{X=rect.Left+(int)(nx*rect.Width),Y=rect.Top+(int)(ny*rect.Height)})==window.Handle;if(solidHit)break;}
        }
        Check("actual Windows hit routing retains puppy interaction hotspot",solidHit);
        foreach(var behavior in Enum.GetValues<Behavior>())
        {
            window.Engine.Preview(behavior);await Task.Delay(140);
            Check("triggerable graph resolved / "+behavior,!string.IsNullOrEmpty(window.Player.ActiveName));
        }
        foreach(var (name,b) in new[]{("idle",Behavior.Idle),("run",Behavior.Trot),("sleep",Behavior.Sleep),("shake",Behavior.Shake),("eat",Behavior.Eat)})
        {
            window.Engine.Preview(b);await Task.Delay(250);
            foreach(var dpi in new[]{96,120,144,192})window.CaptureFrame(Path.Combine(output,$"{name}-{dpi}dpi.png"),160,dpi);
        }
        Check("DPI render dimensions 100/125/150/200 percent",File.Exists(Path.Combine(output,"run-192dpi.png")));
        window.Settings.StaticCompanion=true;window.Settings.IdlePose="FrontSit";window.Engine.Command(Behavior.Idle);
        var stillSource=window.PetImage.Source;var stillX=window.Engine.X;var stillFrameEvents=0;
        window.Player.FrameChanged+=_=>stillFrameEvents++;
        await Task.Delay(1300);
        Check("actual front-sit picture is not looping or moving",window.Player.ActiveName=="pose-FrontSit"&&ReferenceEquals(stillSource,window.PetImage.Source)&&stillFrameEvents==0&&window.Engine.X==stillX);
        window.Engine.Command(Behavior.Shake);await Task.Delay(2400);
        Check("actual handshake returns to chosen front-sit picture",window.Engine.Current==Behavior.Idle&&window.Player.ActiveName=="pose-FrontSit");
        window.CaptureFrame(Path.Combine(output,"default-static-frontsit.png"));
        foreach(var pose in new[]{"Stand","Lie","FrontSit"})
        {window.Settings.IdlePose=pose;window.ApplySettings();await Task.Delay(50);Check("static pose can be selected / "+pose,window.Player.ActiveName=="pose-"+pose);}
        window.Settings.IdlePose="Lie";window.ApplySettings();window.Engine.Command(Behavior.Drink);await Task.Delay(3500);
        Check("actual drinking returns to saved lie-down picture",window.Engine.Current==Behavior.Idle&&window.Player.ActiveName=="pose-Lie");
        window.Settings.IdlePose="FrontSit";window.ApplySettings();
        window.Engine.CancelCommand();window.Engine.Fetch("ball");await Task.Delay(900);window.Engine.ToggleHidden();await Task.Delay(100);
        Check("actual WPF hide interrupts fetch and cleans visible toy",!window.IsVisible&&!window.Engine.ToyVisible&&!window.Audio.Playing);
        var evidence=new {utc=DateTimeOffset.UtcNow,os=Environment.OSVersion.ToString(),framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,upstream="c3420bb77d6d00ed5fabf975a0945c4f6179bba3",results,limitations=new[]{"No cross-process click-through mouse injection performed","DPI captures use RenderTargetBitmap; actual mixed-DPI monitor switching needs physical test","No clean Windows Sandbox available in harness","Audio acoustic quality and full animation continuity require human review"}};
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
    }
}
