using System;
using System.Collections.Generic;
using System.Linq;
using VPet_Simulator.Core;

namespace CyberGolden;
public enum Behavior { Idle,Walk,Trot,Tilt,Lick,Yawn,Stretch,Wag,Sit,Lie,Pet,Eat,Drink,Shake,Sleep,Expect,Jump,Welcome,AfkApproach,AfkRest,WatchToy,ChaseToy,Pickup,ReturnToy,Drop,Drag,HungerMove }
public sealed class BehaviorEngine
{
    public readonly Settings Settings;
    public readonly GameSave Pet;
    public Behavior Current { get; private set; }=Behavior.Idle;
    public long Epoch { get; private set; }
    public bool Manual { get; private set; }
    public bool MenuOpen { get; private set; }
    public bool ToyVisible { get; private set; }
    public bool ToyCarried { get; private set; }
    public string ToyKind { get; private set; }="ball";
    public double X { get; set; }
    public double Y { get; set; }
    public double TargetX { get; private set; }
    public double HomeX { get; private set; }
    public double ToyX { get; private set; }
    public double ToyY { get; private set; }
    public bool FaceRight { get; private set; }
    public double InteractionX { get; private set; }
    public double WorkLeft { get; set; }
    public double WorkRight { get; set; }=1920;
    public double WorkTop { get; set; }
    public double WorkBottom { get; set; }=1080;
    public double TargetY { get; private set; }
    public double PixelSize { get; set; }=160;
    public double DpiScale { get; set; }=1;
    public event Action? Changed;
    public event Action<string?>? SoundRequested;
    public event Action? DataChanged;
    public event Action? CriticalStateChanged;
    private readonly Random random;
    private double clock,deadline,nextTry,nextMove,nextBreath,lastInputAge;
    private double globalCooldown;
    private readonly Dictionary<Behavior,double> cooldown=new();
    private Behavior previousRandom=Behavior.Idle;
    private bool preview;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly HashSet<string> feedRequests=new();
    public BehaviorEngine(Settings settings,GameSave pet,int? seed=null,Func<DateTimeOffset>? time=null)
    { Settings=settings;Pet=pet;random=seed.HasValue?new Random(seed.Value):new Random();utcNow=time??(()=>DateTimeOffset.UtcNow);nextTry=30;nextMove=settings.RoamSeconds; }
    public bool Blocked=>Settings.Hidden||Settings.Paused||MenuOpen||Current==Behavior.Drag;
    public void Start(double idleAge)
    {
        Settings.Companion=false;
        UpdateHunger();
        if(Settings.Sleeping)Set(Behavior.Sleep,false,double.PositiveInfinity);
        else if(Settings.StaticCompanion)Set(Behavior.Idle,false,double.PositiveInfinity);
        else if(idleAge>=Settings.AfkSeconds&&!Blocked)EnterCompanion();
        else if(!Blocked){TargetX=ClampX(X-20*DpiScale);FaceRight=TargetX>X;Set(Behavior.Welcome,false,1.2);}
        else Set(Behavior.Idle,false,double.PositiveInfinity);
    }
    private void Set(Behavior behavior,bool manual,double duration)
    { Current=behavior;Manual=manual;deadline=clock+duration;Epoch++;Changed?.Invoke(); }
    private void Cancel()
    { Epoch++;ToyVisible=false;ToyCarried=false;SoundRequested?.Invoke(null);Manual=false;preview=false; }
    public void ToggleHidden()
    {
        Cancel();Settings.Hidden=!Settings.Hidden;Settings.Companion=false;
        if(Settings.Hidden)Set(Settings.Sleeping?Behavior.Sleep:Behavior.Idle,false,double.PositiveInfinity);
        else if(Settings.Sleeping)Set(Behavior.Sleep,false,double.PositiveInfinity);
        else if(Settings.Paused)Set(Behavior.Idle,false,double.PositiveInfinity);
        else if(Settings.StaticCompanion)ReturnStable();
        else Set(Behavior.Expect,false,1.2);
    }
    public void TogglePause()
    { Cancel();Settings.Paused=!Settings.Paused;Settings.Companion=false;ReturnStable(); }
    public void OpenMenu(bool open)
    { MenuOpen=open; if(open){Cancel();Settings.Companion=false;ReturnStable();} Changed?.Invoke(); }
    public void BeginDrag()
    { Cancel();Settings.Companion=false;Set(Behavior.Drag,true,double.PositiveInfinity);Pet.StrengthFood=Math.Max(0,Pet.StrengthFood-4);UpdateHunger();DataChanged?.Invoke(); }
    public void EndDrag() { ReturnStable(); }
    public void Command(Behavior command,double interactionX=double.NaN)
    {
        if(command==Behavior.Eat){Feed(null,interactionX);return;}
        CommandCore(command,interactionX);
    }
    public bool Feed(string? requestId=null,double interactionX=double.NaN)
    {
        if(Settings.Hidden)return false;
        requestId??=Guid.NewGuid().ToString("N");
        if(requestId==Settings.LastFeedRequest||!feedRequests.Add(requestId))return false;
        if(feedRequests.Count>64){feedRequests.Clear();feedRequests.Add(requestId);}
        Settings.LastFeedRequest=requestId;
        CommandCore(Behavior.Eat,interactionX);
        CriticalStateChanged?.Invoke();
        return true;
    }
    private void CommandCore(Behavior command,double interactionX)
    {
        if(Settings.Hidden)return;
        if(double.IsFinite(interactionX))InteractionX=interactionX;
        Cancel();Settings.Companion=false;
        if(command==Behavior.Sleep)
        { Settings.Sleeping=true;Set(Behavior.Lie,true,1.1);return; }
        if(command==Behavior.Idle)
        { Settings.Sleeping=false;ReturnStable();return; }
        // Direct commands are deliberate wakeups, pause only suspends autonomy.
        Settings.Sleeping=false;
        if(command==Behavior.Eat){Pet.StrengthChangeFood(15);Pet.StrengthChange(3);SoundRequested?.Invoke("eat");}
        else {Pet.StrengthFood=Math.Max(0,Pet.StrengthFood-4);}
        UpdateHunger();DataChanged?.Invoke();
        if(command==Behavior.Drink){Pet.StrengthChangeDrink(22);SoundRequested?.Invoke("drink");}
        else if(command==Behavior.Pet){Pet.FeelingChange(3);Pet.Likability+=0.6;SoundRequested?.Invoke("click");}
        else if(command==Behavior.Jump||command==Behavior.Wag){Pet.FeelingChange(2);SoundRequested?.Invoke("happy");}
        else if(command==Behavior.Shake)Pet.Likability+=0.7;
        if(command==Behavior.Expect)
        { TargetX=ClampX(X+Math.Clamp(InteractionX-X,-80*DpiScale,80*DpiScale));FaceRight=TargetX>X; }
        Set(command,true,command is Behavior.Eat or Behavior.Drink?3.2:command==Behavior.Expect?3:2);
    }
    public void Fetch(string kind)
    {
        if(Settings.Hidden)return;
        Cancel();Settings.Sleeping=false;Settings.Companion=false;
        Pet.StrengthFood=Math.Max(0,Pet.StrengthFood-4);UpdateHunger();DataChanged?.Invoke();
        ToyKind=kind=="plush"?"plush":"ball";HomeX=X;
        var distance=(Pet.Strength<25?65:140)*DpiScale;
        var direction=(WorkRight-PixelSize-X)>distance?1:-1;
        TargetX=ClampX(X+direction*distance);FaceRight=direction>0;
        ToyX=TargetX+PixelSize*(FaceRight?.78:.22);
        ToyY=Y+PixelSize*.91;
        ToyVisible=true;Set(Behavior.WatchToy,true,.65);
    }
    public void CancelCommand(){Cancel();Settings.Companion=false;ReturnStable();}
    // Verification holds an action without inheriting a production movement target.
    public void Preview(Behavior b) { Cancel();Set(b,true,4);preview=true; }
    public double ClampX(double x)=>Math.Clamp(x,WorkLeft+8*DpiScale,Math.Max(WorkLeft+8*DpiScale,WorkRight-PixelSize-8*DpiScale));
    public double ClampY(double y)=>Math.Clamp(y,WorkTop+8*DpiScale,Math.Max(WorkTop+8*DpiScale,WorkBottom-PixelSize-8*DpiScale));
    public void RebaseWorkspace(double left,double right,double size,double dpi)
    {
        var changed=WorkLeft!=left||WorkRight!=right||PixelSize!=size||DpiScale!=dpi;
        WorkLeft=left;WorkRight=right;PixelSize=size;DpiScale=dpi;
        X=ClampX(X);TargetX=ClampX(TargetX);HomeX=ClampX(HomeX);
        Y=ClampY(Y);TargetY=ClampY(TargetY);
        // A removed monitor invalidates a toy route; cancel rather than chase an unreachable point.
        if(changed&&(ToyVisible||Current==Behavior.HungerMove))CancelCommand();
    }
    private void ReturnStable()
    { Set(Settings.Sleeping?Behavior.Sleep:Settings.Companion?Behavior.AfkRest:Behavior.Idle,false,double.PositiveInfinity); }
    public void SettingsUpdated()
    {
        if(Settings.Hidden||Manual||Current==Behavior.Drag)return;
        if(Settings.StaticCompanion){Cancel();ReturnStable();}
        else if(Current==Behavior.Idle)Changed?.Invoke();
    }
    private void EnterCompanion()
    {
        Settings.Companion=true;
        if(Settings.StaticCompanion){Set(Behavior.AfkRest,false,double.PositiveInfinity);return;}
        var left=WorkLeft+16*DpiScale;var right=WorkRight-PixelSize-16*DpiScale;
        TargetX=Math.Abs(X-left)<Math.Abs(X-right)?left:right;
        FaceRight=TargetX>X;
        Set(Behavior.AfkApproach,false,double.PositiveInfinity);
    }
    private bool Move(double dt,double speed,double cursorX,double cursorY,bool avoid)
    {
        var distance=TargetX-X;
        if(Math.Abs(distance)<2*DpiScale){X=TargetX;return true;}
        // Autonomy yields near the user's cursor; fetch only uses an explicit bounded route.
        if(avoid&&Math.Abs(cursorX-(X+PixelSize*.5))<PixelSize*.8&&Math.Abs(cursorY-(Y+PixelSize*.5))<PixelSize*.8)return false;
        X=ClampX(X+Math.Sign(distance)*Math.Min(Math.Abs(distance),dt*speed*DpiScale));return false;
    }
    public void Tick(double dt,double idleAge,double cursorX=-9999,double cursorY=-9999)
    {
        dt=Math.Clamp(dt,0,.25);clock+=dt;lastInputAge=idleAge;
        if(preview){if(clock>=deadline){preview=false;ReturnStable();}return;}
        Pet.StrengthFood=Math.Max(0,Pet.StrengthFood-dt*2/3600);
        UpdateHunger();
        var now=utcNow();
        if(Settings.Hungry&&(Blocked||Settings.Sleeping||Settings.Companion||idleAge>=Settings.AfkSeconds))
        {
            if(Settings.HungerNextSoundUtc<=now){Settings.HungerNextSoundUtc=now.AddMinutes(30);DataChanged?.Invoke();}
        }
        if(Settings.Hidden)return;
        if(Settings.Paused&&!Manual)return;
        // Only the existing five VPet values influence behavior. No death or extreme loss.
        Pet.StrengthDrink=Math.Max(5,Pet.StrengthDrink-dt*.002);
        Pet.Strength=Math.Clamp(Pet.Strength+dt*(Settings.Sleeping?.055:-.001),0,100);
        Pet.Health=100;Pet.Mode=Pet.CalMode();
        if(MenuOpen||Current==Behavior.Drag)return;
        if(Settings.Sleeping&&Current==Behavior.Sleep)
        { if(Settings.SleepSound&&clock>=nextBreath){nextBreath=clock+35;SoundRequested?.Invoke("breath");}return; }
        if(!Manual&&!Settings.Sleeping&&Settings.Companion&&idleAge<2)
        { Settings.Companion=false;nextMove=clock+Settings.RoamSeconds;nextTry=clock+30;globalCooldown=Math.Max(globalCooldown,clock+45);ReturnStable();return; }
        if(!Manual&&!Settings.Sleeping&&idleAge>=Settings.AfkSeconds&&!Settings.Companion)
        {Cancel();EnterCompanion();return;}
        if(Current==Behavior.HungerMove)
        {
            if(MoveToCenter(dt,Pet.Strength>=30?70:18))
            {ReturnStable();HungerVoice();}
            return;
        }
        if(Current==Behavior.WatchToy&&clock>=deadline){Set(Behavior.ChaseToy,true,double.PositiveInfinity);return;}
        if(Current==Behavior.ChaseToy&&Move(dt,Pet.Strength<25?22:75,cursorX,cursorY,false))
        {Set(Behavior.Pickup,true,.95);return;}
        if(Current==Behavior.Pickup&&clock>=deadline)
        { ToyCarried=true;TargetX=ClampX(HomeX);FaceRight=TargetX>X;Set(Behavior.ReturnToy,true,double.PositiveInfinity);return; }
        if(Current==Behavior.ReturnToy&&Move(dt,Pet.Strength<25?22:65,cursorX,cursorY,false))
        { Set(Behavior.Drop,true,.95);return; }
        if(Current==Behavior.Drop&&clock>=deadline)
        {ToyCarried=false;ToyX=X+PixelSize*(FaceRight?.78:.22);ToyY=Y+PixelSize*.91;Set(!Settings.Quiet&&Pet.Strength>45&&Pet.Feeling>65?Behavior.Jump:Behavior.Wag,true,1.5);SoundRequested?.Invoke("happy");return;}
        if(Current==Behavior.AfkApproach&&Move(dt,12,cursorX,cursorY,false))
        {FaceRight=TargetX<WorkLeft+(WorkRight-WorkLeft)/2;Set(Behavior.Lie,false,1.1);return;}
        if(Current==Behavior.Welcome&&(Move(dt,18,cursorX,cursorY,true)||clock>=deadline))
        {Set(Behavior.Wag,false,2.2);return;}
        if(Current is Behavior.Walk or Behavior.Trot)
        { if(Move(dt,Current==Behavior.Walk?18:55,cursorX,cursorY,true)||clock>=deadline)ReturnStable();return; }
        if(Current==Behavior.Expect&&Manual)Move(dt,18,cursorX,cursorY,false);
        if(clock>=deadline)
        {
            if(Current==Behavior.Lie){ReturnStable();return;}
            if(ToyVisible){ToyVisible=false;ToyCarried=false;}
            ReturnStable();nextMove=Math.Max(nextMove,clock+20);return;
        }
        if(Blocked||Manual||Settings.Sleeping)return;
        if(Settings.Companion)return;
        if(Current!=Behavior.Idle)return;
        if(Settings.Hungry)
        {
            if(!Settings.HungerCenterAttempted)
            {
                Settings.HungerCenterAttempted=true;
                Settings.HungerNextSoundUtc=now.AddMinutes(30);DataChanged?.Invoke();
                CriticalStateChanged?.Invoke(); // Persist before the first movement pixel.
                TargetX=ClampX((WorkLeft+WorkRight-PixelSize)/2);
                TargetY=ClampY((WorkTop+WorkBottom-PixelSize)/2);FaceRight=TargetX>X;
                Set(Behavior.HungerMove,false,double.PositiveInfinity);return;
            }
            if(now>=Settings.HungerNextSoundUtc)HungerVoice();
            return;
        }
        if(!Settings.StaticCompanion&&clock>=nextTry)
        {
            nextTry=clock+random.Next(20,41);
            if(clock>=globalCooldown&&random.NextDouble()<.25)
            {
                var excitement=(.35+.65*Math.Min(100,Pet.Likability)/100);
                var choices=new Dictionary<Behavior,double>{[Behavior.Tilt]=2,[Behavior.Lick]=2,[Behavior.Yawn]=Pet.Strength<30?6:1,[Behavior.Stretch]=1,[Behavior.Wag]=(Settings.Quiet?.2:Pet.Feeling>75?4:1)*excitement,[Behavior.Sit]=2,[Behavior.Lie]=Pet.Strength<30?5:1};
                var eligible=choices.Where(x=>x.Key!=previousRandom&&(!cooldown.TryGetValue(x.Key,out var until)||clock>=until)).ToList();
                var pick=random.NextDouble()*eligible.Sum(x=>x.Value);
                foreach(var pair in eligible)
                {pick-=pair.Value;if(pick<=0){previousRandom=pair.Key;cooldown[pair.Key]=clock+random.Next(60,91);globalCooldown=clock+45;Set(pair.Key,false,pair.Key is Behavior.Sit or Behavior.Lie?6:2);return;}}
            }
        }
        if(Settings.AutoMove&&!Settings.Quiet&&clock>=nextMove)
        {
            nextMove=clock+Settings.RoamSeconds+random.Next(0,61);
            var trot=Pet.Strength>45&&random.NextDouble()<.15;
            if(Pet.Likability>45&&random.NextDouble()<.2)
                TargetX=ClampX(X+Math.Clamp(InteractionX-X,-65*DpiScale,65*DpiScale));
            else TargetX=ClampX(X+random.Next(-100,101)*DpiScale);
            if(Math.Abs(TargetX-X)>20*DpiScale)
            {FaceRight=TargetX>X;Set(trot?Behavior.Trot:Behavior.Walk,false,trot?3:6);}
        }
    }
    private void UpdateHunger()
    {
        if(!Settings.Hungry&&Pet.StrengthFood<15)
        {Settings.Hungry=true;Settings.HungerCenterAttempted=false;Settings.HungerNextSoundUtc=utcNow().AddMinutes(30);DataChanged?.Invoke();}
        else if(Settings.Hungry&&Pet.StrengthFood>=30)
        {
            Settings.Hungry=false;Settings.HungerCenterAttempted=false;Settings.HungerNextSoundUtc=DateTimeOffset.MinValue;
            DataChanged?.Invoke();
            if(Current==Behavior.HungerMove){Cancel();ReturnStable();}
        }
    }
    private bool MoveToCenter(double dt,double speed)
    {
        var dx=TargetX-X;var dy=TargetY-Y;var distance=Math.Sqrt(dx*dx+dy*dy);
        if(distance<=2*DpiScale){X=TargetX;Y=TargetY;return true;}
        var step=Math.Min(distance,dt*speed*DpiScale);X=ClampX(X+dx/distance*step);Y=ClampY(Y+dy/distance*step);return false;
    }
    private void HungerVoice()
    {
        var now=utcNow();Settings.HungerNextSoundUtc=now.AddMinutes(30);DataChanged?.Invoke();
        if(now<Settings.HungerLastSoundUtc.AddMinutes(30))return;
        Settings.HungerLastSoundUtc=now;DataChanged?.Invoke();CriticalStateChanged?.Invoke();
        if(Settings.HungerSound&&Settings.Sound&&Settings.Volume>0)SoundRequested?.Invoke("click");
    }
    public string AnimationName=>Current switch
    {
        Behavior.Walk or Behavior.AfkApproach=>"walk",Behavior.Trot or Behavior.ChaseToy=>Pet.Strength<25?"walk":"trot",
        Behavior.HungerMove=>Pet.Strength>=30?"trot":"walk",
        Behavior.ReturnToy=>Pet.Strength<25?"walk":"carry",Behavior.Pickup or Behavior.Drop=>"pickup",
        Behavior.Tilt=>"tilt",Behavior.Lick=>"lick",Behavior.Yawn=>"yawn",Behavior.Stretch=>"stretch",
        Behavior.Wag=>"wag",Behavior.Welcome=>"walk",Behavior.Sit=>"sitdown",Behavior.Lie=>"liedown",
        Behavior.Pet=>"pet",Behavior.Eat=>"eat",Behavior.Drink=>"drink",Behavior.Shake=>"shake",
        Behavior.Sleep=>"sleep",Behavior.Expect=>"expect",Behavior.Jump=>Pet.Strength<25?"wag":"jump",
        Behavior.AfkRest=>"companion",_=>"idle"
    };
    public string StatusText=>Settings.Hidden?"已隐藏":Settings.Paused?"自动行为已暂停":Current switch
    {
        Behavior.Sleep=>"正在睡觉",Behavior.AfkRest=>"安静陪伴",Behavior.AfkApproach=>"走到安全边缘",
        Behavior.Eat=>"吃饭中",Behavior.Drink=>"喝水中",Behavior.Pet=>"享受摸头",Behavior.Shake=>"握手",
        Behavior.WatchToy or Behavior.ChaseToy or Behavior.Pickup or Behavior.ReturnToy or Behavior.Drop=>"陪玩中",
        Behavior.HungerMove=>"走到屏幕中央提醒",Behavior.Walk or Behavior.Trot=>"散步中",Behavior.Drag=>"正在拖动",_=>"陪在你身边"
    };
}
