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
    public bool Playing { get; private set; }
    public int PlayRequests { get; private set; }
    public LocalAudio(Settings s,StateStore st)
    {settings=s;store=st;player.MediaFailed+=(_,e)=>{Playing=false;store.Log("audio",e.ErrorException);};player.MediaEnded+=(_,_)=>Playing=false;}
    public void Play(string? name)
    {
        if(name==null){Stop();return;}
        if(!settings.Sound||settings.Volume<=0||settings.Hidden||settings.Paused)return;
        if(name=="breath"&&!settings.SleepSound)return;
        if(DateTime.UtcNow<nextAllowed)return;
        try
        {
            var path=Path.Combine(AppContext.BaseDirectory,"assets","audio",name+".wav");
            if(!File.Exists(path)){store.Log("audio",new FileNotFoundException("本地声音缺失",name));return;}
            player.Stop();player.Close();player.Volume=settings.Volume/100.0;
            player.Open(new Uri(path));player.Play();Playing=true;PlayRequests++;
            nextAllowed=DateTime.UtcNow.AddSeconds(name=="breath"?20:3);
        }
        catch(Exception e){Playing=false;store.Log("audio",e);}
    }
    public void Update(){player.Volume=settings.Volume/100.0;if(!settings.Sound||settings.Volume==0)Stop();}
    public void Stop(){try{player.Stop();player.Close();}catch{}Playing=false;}
    public void Dispose(){Stop();}
}
