using System;
using System.IO;
using System.Windows.Media;

namespace CyberGolden;
public sealed class LocalAudio : IDisposable
{
    private readonly MediaPlayer player=new();
    private readonly Settings settings;
    private readonly StateStore store;
    private DateTime nextAllowed=DateTime.MinValue;
    private string activeName="";
    public bool Playing { get; private set; }
    public int PlayRequests { get; private set; }
    public LocalAudio(Settings s,StateStore st)
    {settings=s;store=st;player.MediaFailed+=(_,e)=>{Playing=false;store.Log("audio",e.ErrorException);};player.MediaEnded+=(_,_)=>Playing=false;}
    public void Play(string? name)
    {
        if(name==null){Stop();return;}
        if(!settings.Sound||settings.Volume<=0||settings.Hidden||settings.Paused)return;
        if(settings.Quiet&&name is "click" or "happy" or "hunger-two" or "fetch")return;
        if(name=="hunger-two"&&!settings.HungerSound)return;
        if(name=="breath"&&!settings.SleepSound)return;
        if(name is not ("hunger-two" or "fetch")&&DateTime.UtcNow<nextAllowed)return;
        try
        {
            var path=Path.Combine(AppContext.BaseDirectory,"assets","audio",(name=="fetch"?"click":name)+".wav");
            if(!File.Exists(path)){store.Log("audio",new FileNotFoundException("本地声音缺失",name));return;}
            if(name!="eat")path=AudioGain.Prepare(path,Path.Combine(store.DirectoryPath,"audio-gain"),settings.Volume/100.0*20);
            player.Stop();player.Close();activeName=name;player.Volume=name=="eat"?settings.Volume/100.0:1;
            player.Open(new Uri(path));player.Play();Playing=true;PlayRequests++;
            nextAllowed=name is "hunger-two" or "fetch"?DateTime.MinValue:DateTime.UtcNow.AddSeconds(name=="breath"?20:3);
        }
        catch(Exception e){Playing=false;store.Log("audio",e);}
    }
    public void Update(){player.Volume=activeName=="eat"?settings.Volume/100.0:1;if(activeName!="eat"||!settings.Sound||settings.Volume==0||settings.Quiet&&activeName is "click" or "happy" or "hunger-two" or "fetch"||activeName=="hunger-two"&&!settings.HungerSound)Stop();}
    public void Stop(){try{player.Stop();player.Close();}catch{}Playing=false;}
    public void Dispose(){Stop();}
}
