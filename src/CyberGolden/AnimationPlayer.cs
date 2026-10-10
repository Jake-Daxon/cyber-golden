using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LinePutScript;
using VPet_Simulator.Core;
using static VPet_Simulator.Core.GraphInfo;

namespace CyberGolden;
public sealed class AnimationPlayer : IDisposable
{
    public readonly GameCore Core;
    public readonly AlphaMask Mask=new();
    private readonly Image image;
    private readonly StateStore store;
    private IGraph? active;
    private long generation;
    public event Action<int>? FrameChanged;
    public string ActiveName { get; private set; }="";
    public AnimationPlayer(Image target,GameSave pet,StateStore stateStore)
    {
        image=target;store=stateStore;
        var root=Path.Combine(AppContext.BaseDirectory,"assets","pet");
        GraphCore.CachePath=Path.Combine(store.DirectoryPath,"cache");
        var loader=new PetLoader(new LpsDocument(File.ReadAllText(Path.Combine(root,"golden.lps"))),new DirectoryInfo(root));
        var graph=loader.Graph(512,target.Dispatcher);
        Core=new GameCore { Graph=graph,Save=pet };
        foreach(var animation in graph.GraphsALL.OfType<PNGAnimation>())
            animation.FrameDisplayed+=(frame,index)=>{if(ReferenceEquals(active,animation)){Mask.Set(frame);FrameChanged?.Invoke(index);}};
    }
    public async Task Prepare()
    {
        var graphs=Core.Graph!.GraphsALL;
        if(graphs.Count==0)throw new InvalidDataException("没有金毛动画资源");
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(graphs.Any(g=>!g.IsReady&&!g.IsFail)&&DateTime.UtcNow<deadline)await Task.Delay(30);
        var failed=graphs.Where(g=>g.IsFail||!g.IsReady).ToList();
        foreach(var g in failed)store.Log("animation",new InvalidDataException(g.FailMessage));
        if(!graphs.Any(g=>g.IsReady))throw new InvalidDataException("金毛动画加载失败；诊断日志已记录");
    }
    public void Stop()
    {
        generation++;
        if(active?.Control!=null){active.Control.EndAction=null;active.Control.Type=IGraph.TaskControl.ControlType.Status_Stoped;}
    }
    public void Play(string name,bool loop,bool reverse=false)
    {
        Stop();
        var graph=Core.Graph!.FindGraph(name,AnimatType.Single,Core.Save!.Mode) as PNGAnimation;
        if(graph?.IsReady!=true)
            graph=Core.Graph.FindGraph("idle",AnimatType.Single,IGameSave.ModeType.Nomal) as PNGAnimation;
        if(graph?.IsReady!=true)
            graph=Core.Graph.GraphsALL.OfType<PNGAnimation>().FirstOrDefault(g=>g.IsReady&&!g.IsFail);
        if(graph==null)return;
        active=graph;ActiveName=graph.GraphInfo.Name;graph.IsLoop=loop;
        // Reverse a head-lowering pickup into a head-lifting drop, explicitly declared reuse.
        var expected=graph.Animations.Select(a=>a.FrameIndex).ToArray();
        var needReverse=reverse&&expected[0]<expected[^1];
        var needForward=!reverse&&expected[0]>expected[^1];
        if(needReverse||needForward)graph.Animations.Reverse();
        var epoch=generation;
        var task=graph.Run(image,()=> {if(epoch!=generation)return;});
        if(task.Status==TaskStatus.Created)task.Start();
    }
    public void ShowPose(string pose)
    {
        Stop();
        var graph=Core.Graph!.FindGraph("pose-"+pose,AnimatType.Single,IGameSave.ModeType.Nomal) as Picture;
        graph??=Core.Graph.FindGraph("pose-FrontSit",AnimatType.Single,IGameSave.ModeType.Nomal) as Picture;
        if(graph==null){Play("idle",false);return;}
        try
        {
            active=graph;ActiveName=graph.GraphInfo.Name;graph.IsLoop=false;
            var task=graph.Run(image);
            if(image.Source is BitmapSource source){Mask.Set(source);FrameChanged?.Invoke(0);}
            if(task.Status==TaskStatus.Created)task.Start(); // One 1ms completion, no timer/loop.
        }
        catch(Exception e){store.Log("static pose",e);Play("idle",false);}
    }
    public void Dispose(){Stop();Core.Graph?.Dispose();}
}
