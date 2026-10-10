using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VPet_Simulator.Core;
namespace CyberGolden;
public static class HungerVerification
{
    public static async Task Run(DesktopPetWindow window,string output,Action<string,bool,string> check)
    {
        var now=DateTimeOffset.UtcNow;
        var settings=new Settings{AutoMove=false};var pet=new GameSave("饥饿测试"){StrengthFood=80,Strength=80};
        var engine=new BehaviorEngine(settings,pet,2,()=>now){X=100,Y=100,WorkLeft=0,WorkRight=1000,WorkTop=0,WorkBottom=800};
        void Step(double seconds,double idle=0){for(int i=0;i<(int)(seconds*4);i++){now=now.AddMilliseconds(250);engine.Tick(.25,idle);}}
        engine.Start(0);engine.Command(Behavior.Shake);
        check("ordinary interaction spends exactly four food points",Math.Abs(pet.StrengthFood-76)<.001,"");
        Step(3);var before=pet.StrengthFood;Step(3600);
        check("ordinary runtime food decay is two points per hour",Math.Abs(before-pet.StrengthFood-2)<.01,"");
        var gain=pet.StrengthFood;engine.Feed("folder-first");engine.Feed("menu-next");
        check("distinct feeds have no cooldown even during eating",Math.Abs(pet.StrengthFood-Math.Min(100,gain+30))<.001&&engine.Current==Behavior.Eat,"");
        var epoch=engine.Epoch;var value=pet.StrengthFood;var duplicate=engine.Feed("menu-next");
        check("same feed event is not credited or restarted twice",!duplicate&&pet.StrengthFood==value&&engine.Epoch==epoch,"");
        pet.StrengthFood=100;check("full stomach still accepts feeding and animation",engine.Feed("full-feed")&&pet.StrengthFood==100&&engine.Current==Behavior.Eat,"");
        engine.CancelCommand();pet.StrengthFood=14;Step(.25);
        check("hungry below fifteen begins one real gait path",settings.Hungry&&settings.HungerCenterAttempted&&engine.Current==Behavior.HungerMove&&engine.AnimationName=="trot","");
        var startX=engine.X;Step(.25);check("central reminder moves gradually inside current work area",engine.X>startX&&engine.X<engine.TargetX&&engine.Y>=8&&engine.Y<=632,"");
        var voices=0;engine.SoundRequested+=s=>{if(s=="click")voices++;};Step(10);
        check("arrival rests at work-area center and chirps once",engine.Current==Behavior.Idle&&Math.Abs(engine.X-420)<2&&Math.Abs(engine.Y-320)<2&&voices==1,"");
        Step(60);check("continuing hunger does not repeat the center path",engine.Current==Behavior.Idle&&engine.X==420&&voices==1,"");
        Step(1800);check("later hunger sound waits at least thirty minutes",voices==2,"");
        engine.Feed("hunger-feed-one");check("hunger hysteresis stays active below thirty",settings.Hungry&&pet.StrengthFood<30,"");
        engine.Feed("hunger-feed-two");check("feeding to thirty clears hunger immediately",!settings.Hungry&&pet.StrengthFood>=30,"");
        engine.CancelCommand();pet.StrengthFood=14;Step(.25);engine.Command(Behavior.Shake);Step(3);
        check("manual command interrupts hunger without retrying central move",settings.HungerCenterAttempted&&engine.Current==Behavior.Idle,"");
        var oldX=engine.X;var oldVoices=voices;engine.ToggleHidden();Step(2000);engine.ToggleHidden();Step(1);
        check("hidden hunger creates no motion voice or replay queue",engine.X==oldX&&voices==oldVoices&&engine.Current==Behavior.Idle,"");
        engine.TogglePause();Step(2000);check("paused hunger does not move or chirp",voices==oldVoices&&engine.X==oldX,"");engine.TogglePause();
        engine.Command(Behavior.Sleep);Step(2000);check("explicit sleep suppresses hunger attention",engine.Current==Behavior.Sleep&&voices==oldVoices,"");engine.Command(Behavior.Idle);
        Step(2000,9999);check("AFK quiet companionship suppresses hunger attention",settings.Companion&&voices==oldVoices,"");
        settings.Sound=false;settings.Companion=false;engine.CancelCommand();Step(1);settings.HungerNextSoundUtc=now.AddSeconds(-1);Step(.25);
        check("global mute suppresses hunger voices",voices==oldVoices,"");
        settings.Sound=true;settings.HungerSound=false;settings.HungerLastSoundUtc=DateTimeOffset.MinValue;settings.HungerNextSoundUtc=now.AddSeconds(-1);Step(.25);
        check("hunger-only sound switch suppresses hunger voices",voices==oldVoices,"");
        var store=new StateStore(Path.Combine(output,"hunger-save"));store.Save(settings,pet);var loaded=store.Load();
        check("hunger episode and transaction record persist",loaded.Settings.Hungry&&loaded.Settings.HungerCenterAttempted&&loaded.Settings.LastFeedRequest==settings.LastFeedRequest,"");
        var restart=new BehaviorEngine(loaded.Settings,loaded.Pet,3,()=>now){X=600,Y=200};restart.Start(0);restart.Tick(.25,0);
        check("restart does not repeat this hunger episode's central move",restart.Current==Behavior.Idle&&restart.X==600,"");
        var lowSettings=new Settings{AutoMove=false};var lowPet=new GameSave("低精力测试"){Strength=10,StrengthFood=14};var low=new BehaviorEngine(lowSettings,lowPet,1){X=100};low.Start(0);low.Tick(.25,0);
        check("low-energy hungry path uses walking gait",low.Current==Behavior.HungerMove&&low.AnimationName=="walk","");
        var roamSettings=new Settings{RoamSeconds=60};var roam=new BehaviorEngine(roamSettings,new GameSave("漫游测试"),4){X=500,WorkRight=1500};roam.Start(0);for(int i=0;i<232;i++)roam.Tick(.25,0);
        check("static idle respects autonomous roaming cooldown",roam.Current==Behavior.Idle,"");var roamed=false;
        roam.Changed+=()=>{if(roam.Current is Behavior.Walk or Behavior.Trot)roamed=true;};for(int i=0;i<1200;i++)roam.Tick(.25,0);
        check("static idle permits occasional gait-based roaming",roamed,"");
        var empty=Path.Combine(output,"reusable-empty-bowl");Directory.CreateDirectory(empty);var occupied=Path.Combine(output,"nonempty-bowl");Directory.CreateDirectory(occupied);File.WriteAllText(Path.Combine(occupied,"kept.txt"),"keep");
        var info=new DirectoryInfo(empty);var modified=info.LastWriteTimeUtc;var created=info.CreationTimeUtc;var attributes=info.Attributes;
        check("only ordinary local empty folders qualify",FolderFeeder.IsOrdinaryLocalEmptyFolder(empty)&&!FolderFeeder.IsOrdinaryLocalEmptyFolder(occupied)&&!FolderFeeder.IsOrdinaryLocalEmptyFolder(Path.Combine(occupied,"kept.txt"))&&!FolderFeeder.IsOrdinaryLocalEmptyFolder(@"\\server\share"),"");
        window.Settings.Hidden=false;window.Settings.Paused=false;window.Settings.Sleeping=false;window.Settings.AutoMove=false;window.Settings.Hungry=false;window.Engine.Command(Behavior.Idle);window.Pet.StrengthFood=70;
        check("creating a folder alone does not feed",window.Pet.StrengthFood==70,"");
        window.AcceptFolder(empty,"drop-proof");var food=window.Pet.StrengthFood;var acceptedAgain=window.AcceptFolder(empty,"drop-proof");
        check("folder and menu use the same once-per-event accounting",Math.Abs(food-85)<.01&&!acceptedAgain&&Math.Abs(window.Pet.StrengthFood-85)<.01,"");
        window.Engine.Feed("menu-proof");check("right-menu feed follows folder immediately without cooldown",window.Pet.StrengthFood==100,"");
        check("empty folder is unchanged and reusable",Directory.Exists(empty)&&!Directory.EnumerateFileSystemEntries(empty).Any()&&new DirectoryInfo(empty).LastWriteTimeUtc==modified&&new DirectoryInfo(empty).CreationTimeUtc==created&&File.GetAttributes(empty)==attributes&&window.AcceptFolder(empty,"new-drop-proof"),"");
        window.Engine.CancelCommand();window.Pet.StrengthFood=14;window.Engine.Tick(.25,0);await Task.Delay(60);
        check("actual hunger bubble is continuously visible",window.HungerText.IsVisible,"");
        Native.GetWindowRect(window.HungerText.Handle,out var rect);
        check("actual hunger bubble routes clicks to underlying window",Native.WindowFromPoint(new Native.PointI{X=(rect.Left+rect.Right)/2,Y=(rect.Top+rect.Bottom)/2})!=window.HungerText.Handle,"");
        window.Engine.ToggleHidden();check("hide synchronously hides hunger bubble",!window.HungerText.IsVisible,"");window.Engine.ToggleHidden();
        window.Pet.StrengthFood=30;window.Engine.Tick(0,0);check("feeding threshold removes actual persistent bubble",!window.Settings.Hungry&&!window.HungerText.IsVisible,"");
        window.Pet.StrengthFood=100;window.Engine.CancelCommand();
    }
}
