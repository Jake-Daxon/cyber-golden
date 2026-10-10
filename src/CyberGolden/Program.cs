using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace CyberGolden;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string? Value(string key){var at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;}
        var test=Array.IndexOf(args,"--self-test")>=0;
        var output=Value("--output")??Path.Combine(AppContext.BaseDirectory,"verification");
        var data=Value("--data-dir")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CyberGolden");
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName+Path.GetFullPath(data))))[..16];
        using var mutex=new Mutex(true,"Local\\CyberGolden-"+hash,out var created);
        if(!created)return 0;
        StateStore? store=null;
        try
        {
            store=new StateStore(data);
            var (settings,pet)=store.Load();
            var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
            app.DispatcherUnhandledException+=(_,e)=>{store.Log("dispatcher",e.Exception);};
            var window=new DesktopPetWindow(store,settings,pet,test);
            if(Array.IndexOf(args,"--ui-test")>=0)window.ShowInTaskbar=true;
            if(Array.IndexOf(args,"--state-probe")>=0)
            {
                Directory.CreateDirectory(output);
                window.Loaded+=async(_,_)=>
                {
                    var deadline=DateTime.UtcNow.AddSeconds(35);
                    while(!window.Prepared&&DateTime.UtcNow<deadline)await Task.Delay(30);
                    await Task.Delay(200);
                    File.WriteAllText(Path.Combine(output,"state-probe.json"),JsonSerializer.Serialize(new{prepared=window.Prepared,visible=window.IsVisible,hidden=window.Settings.Hidden,paused=window.Settings.Paused,sleeping=window.Settings.Sleeping,staticCompanion=window.Settings.StaticCompanion,idlePose=window.Settings.IdlePose,hungry=window.Settings.Hungry,centerAttempted=window.Settings.HungerCenterAttempted,bubble=window.HungerText.IsVisible,graph=window.Player.ActiveName,behavior=window.Engine.Current.ToString()},new JsonSerializerOptions{WriteIndented=true}));
                    window.Close();
                };
            }
            if(test)
            {
                Directory.CreateDirectory(output);
                var priorFailure=Path.Combine(output,"FAILED.txt");
                if(File.Exists(priorFailure))File.Move(priorFailure,Path.Combine(output,"FAILED-prior-"+DateTime.UtcNow.Ticks+".txt"));
                window.Loaded+=async(_,_)=>
                {
                    try{await Verification.Run(window,output);Environment.ExitCode=0;}
                    catch(Exception e){File.WriteAllText(Path.Combine(output,"FAILED.txt"),e.ToString());Environment.ExitCode=1;}
                    window.Close();
                };
            }
            app.Run(window);return Environment.ExitCode;
        }
        catch(Exception e)
        {
            store?.Log("fatal",e);
            if(test){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"FAILED.txt"),e.ToString());}
            return 1;
        }
    }
}
