using System;
using System.IO;
using System.Text.Json;
using LinePutScript;
using VPet_Simulator.Core;

namespace CyberGolden;

public sealed class Settings
{
    public string Name { get; set; } = "赛博金毛";
    public double Size { get; set; } = 160;
    // Coordinates are physical screen pixels, never mixed with WPF DIP.
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
    public string Monitor { get; set; } = "";
    public bool Topmost { get; set; } = true;
    public bool AutoMove { get; set; } = true;
    public bool Quiet { get; set; }
    public bool Sound { get; set; } = true;
    public double Volume { get; set; } = 20;
    public bool SleepSound { get; set; }
    public int AfkSeconds { get; set; } = 180;
    public string Hotkey { get; set; } = "Ctrl+Alt+D";
    public bool Startup { get; set; }
    public bool Hidden { get; set; }
    public bool Paused { get; set; }
    public bool Sleeping { get; set; }
    public bool Companion { get; set; }
    public bool StaticCompanion { get; set; } = true;
    public string IdlePose { get; set; } = "Lie";
    public int BehaviorRevision { get; set; }
    public int HungerDragCount { get; set; }
    public bool HungerAnnounced { get; set; }
    public bool Hungry { get; set; }
    public bool HungerCenterAttempted { get; set; }
    public bool HungerSound { get; set; } = true;
    public DateTimeOffset HungerNextSoundUtc { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset HungerLastSoundUtc { get; set; } = DateTimeOffset.MinValue;
    public string LastFeedRequest { get; set; } = "";
    public int RoamSeconds { get; set; } = 180;
    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "赛博金毛" : Name.Trim()[..Math.Min(Name.Trim().Length,24)];
        Size = double.IsFinite(Size) ? Math.Clamp(Size,96,240) : 160;
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume,0,100) : 20;
        X = double.IsFinite(X) ? X : -1; Y = double.IsFinite(Y) ? Y : -1;
        AfkSeconds = Math.Clamp(AfkSeconds,30,3600);
        Hotkey ??= "Ctrl+Alt+D";
        if(BehaviorRevision<4){if(IdlePose=="FrontSit")IdlePose="Lie";BehaviorRevision=4;}
        StaticCompanion=true;
        if(IdlePose is not ("FrontSit" or "Stand" or "Lie"))IdlePose="Lie";
        RoamSeconds=Math.Clamp(RoamSeconds,5,1000);
        HungerDragCount=Math.Max(0,HungerDragCount);
        LastFeedRequest??="";
        if(LastFeedRequest.Length>64)LastFeedRequest="";
        if(HungerNextSoundUtc>DateTimeOffset.UtcNow.AddMinutes(30))HungerNextSoundUtc=DateTimeOffset.UtcNow.AddMinutes(30);
        if(HungerLastSoundUtc>DateTimeOffset.UtcNow)HungerLastSoundUtc=DateTimeOffset.UtcNow;
    }
}

public sealed class SaveEnvelope
{
    public int Version { get; set; } = 2;
    public Settings Settings { get; set; } = new();
    public string PetLps { get; set; } = "";
    public DateTimeOffset SavedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StateStore
{
    public string DirectoryPath { get; }
    public string SavePath => Path.Combine(DirectoryPath,"state.json");
    public string Diagnostic { get; private set; } = "";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented=true };
    public StateStore(string path) { DirectoryPath=path; Directory.CreateDirectory(path); }
    public (Settings Settings,GameSave Pet) Load()
    {
        foreach (var path in new[]{SavePath,SavePath+".bak"})
        {
            if (!File.Exists(path)) continue;
            try
            {
                var save=JsonSerializer.Deserialize<SaveEnvelope>(File.ReadAllText(path)) ?? throw new InvalidDataException("空存档");
                if(save.Version is <1 or >2) throw new InvalidDataException("不支持的存档版本");
                var settings=save.Settings ?? new Settings(); settings.Normalize();
                var lps=new LpsDocument(save.PetLps);
                if (lps.Count==0) throw new InvalidDataException("缺少宠物养成状态");
                var pet=GameSave.Load(lps.First()!);
                NormalizePet(pet);pet.Name=settings.Name;
                // A capped, gentle offline update; clock rollback contributes zero.
                var hours=Math.Clamp((DateTimeOffset.UtcNow-save.SavedUtc).TotalHours,0,4);
                pet.StrengthFood=Math.Max(5,pet.StrengthFood-hours*2);
                pet.StrengthDrink=Math.Max(15,pet.StrengthDrink-hours*3);
                if(settings.Sleeping)pet.Strength+=hours*6;
                pet.Mode=pet.CalMode();
                if(path.EndsWith(".bak")) Diagnostic="主存档损坏，已恢复备份。";
                return(settings,pet);
            }
            catch(Exception e) { Diagnostic="存档恢复："+e.Message; Log("save load",e); }
        }
        return(new Settings(),new GameSave("赛博金毛") { Likability=25,Feeling=75 });
    }
    public static void NormalizePet(GameSave pet)
    {
        double Clean(double value,double fallback=70)=>double.IsFinite(value)?Math.Clamp(value,0,100):fallback;
        pet.Exp=double.IsFinite(pet.Exp)?Math.Clamp(pet.Exp,0,100000):0;
        pet.Strength=Clean(pet.Strength);pet.StrengthFood=Clean(pet.StrengthFood);
        pet.StrengthDrink=Clean(pet.StrengthDrink);pet.Feeling=Clean(pet.Feeling);
        pet.Likability=Clean(pet.Likability,25);pet.Health=100;
        pet.StoreStrength=0;pet.StoreStrengthFood=0;pet.StoreStrengthDrink=0;
    }
    public void Save(Settings settings,GameSave pet)
    {
        try
        {
            settings.Normalize();pet.Name=settings.Name;
            var envelope=new SaveEnvelope { Settings=settings,PetLps=pet.ToLine().ToString(),SavedUtc=DateTimeOffset.UtcNow };
            var temp=SavePath+".tmp";
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            { JsonSerializer.Serialize(file,envelope,JsonOptions);file.Flush(true); }
            if(File.Exists(SavePath)) File.Replace(temp,SavePath,SavePath+".bak",true);
            else File.Move(temp,SavePath);
        }
        catch(Exception e) { Diagnostic="保存失败："+e.Message;Log("save write",e); }
    }
    public void Log(string context,Exception e)
    {
        try
        {
            var path=Path.Combine(DirectoryPath,"diagnostic.log");
            if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)File.Move(path,path+".old",true);
            File.AppendAllText(path,$"{DateTimeOffset.UtcNow:O} {context}: {e.GetType().Name}: {e.Message}\n");
        } catch { }
    }
}
