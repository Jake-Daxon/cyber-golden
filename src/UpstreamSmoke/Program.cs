using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LinePutScript;
using VPet_Simulator.Core;
class Program
{
 [STAThread]static int Main(string[] args)
 {
  var root=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
  var app=new Application();var image=new System.Windows.Controls.Image();
  var window=new Window{Width=160,Height=160,Content=new Viewbox{Child=image},ShowActivated=false,ShowInTaskbar=false};
  GraphCore.CachePath=Path.Combine(output,"cache");
  var loader=new PetLoader(new LpsDocument(File.ReadAllText(Path.Combine(root,"golden.lps"))),new DirectoryInfo(root));
  var graph=loader.Graph(512,window.Dispatcher);var pet=new GameSave("上游运行验证");
  var hashes=new HashSet<int>();var ticks=0;IGraph? animation=null;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  timer.Tick+=(_,_)=>
  {
   if(animation==null){animation=graph.FindGraph("trot",GraphInfo.AnimatType.Single,IGameSave.ModeType.Nomal);if(animation?.IsReady!=true){animation=null;return;}animation.IsLoop=true;((Picture.IImageRun)animation).Run(image).Start();}
   if(image.Source!=null)hashes.Add(image.Source.GetHashCode());
   if(++ticks>=20)
   {
    timer.Stop();animation.Stop(true);
    var loaded=GameSave.Load(new LpsDocument(pet.ToLine().ToString()).First()!);
    var result=new{source="unmodified upstream Core",commit="c3420bb77d6d00ed5fabf975a0945c4f6179bba3",graphs=graph.GraphsALL.Count,distinctDisplayedFrames=hashes.Count,gameSaveRoundtrip=loaded.Name==pet.Name,passed=hashes.Count>=6&&loaded.Name==pet.Name};
    File.WriteAllText(Path.Combine(output,"upstream-smoke.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    Environment.ExitCode=result.passed?0:1;window.Close();
   }
  };
  window.Loaded+=(_,_)=>timer.Start();app.Run(window);graph.Dispose();return Environment.ExitCode;
 }
}
