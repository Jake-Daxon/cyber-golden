using System;
using System.Diagnostics;

namespace CyberGolden;
public sealed class BallMotion
{
    public double X,Y,Vx,Vy,Radius=12;
    public bool Held,Thrown,Settled;
    private double left,right,top,bottom,gravity,lastSample;
    private static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    public void Reset(double x,double y,double radius,double l,double r,double t,double b,double g)
    {X=x;Y=y;Radius=radius;left=l+radius;right=Math.Max(left,r-radius);top=t+radius;bottom=Math.Max(top,b-radius);gravity=g;Vx=Vy=0;Held=Thrown=Settled=false;Clamp();}
    private void Clamp(){X=Math.Clamp(X,left,right);Y=Math.Clamp(Y,top,bottom);}
    public void Grab(double x,double y){Held=true;Settled=false;Vx=Vy=0;X=x;Y=y;Clamp();lastSample=Now;}
    public void Hold(double x,double y){if(!Held||Math.Abs(x-X)+Math.Abs(y-Y)<.001)return;var now=Now;var dt=Math.Clamp(now-lastSample,.008,.15);var limit=gravity*1.6;Vx=.4*Vx+.6*Math.Clamp((x-X)/dt,-limit,limit);Vy=.4*Vy+.6*Math.Clamp((y-Y)/dt,-limit,limit);X=x;Y=y;Clamp();lastSample=now;}
    public void Release(){if(!Held)return;if(Now-lastSample>.12)Vx=Vy=0;Held=false;Thrown=true;Settled=false;}
    public void Tick(double elapsed)
    {
        if(Held||Settled)return;
        var remaining=Math.Clamp(elapsed,0,.25);
        while(remaining>0){var dt=Math.Min(remaining,1.0/120);remaining-=dt;Vy+=gravity*dt;X+=Vx*dt;Y+=Vy*dt;
            if(X<left){X=left;Vx=Math.Abs(Vx)*.72;}else if(X>right){X=right;Vx=-Math.Abs(Vx)*.72;}
            if(Y<top){Y=top;Vy=Math.Abs(Vy)*.45;}
            if(Y>=bottom){Y=bottom;if(Math.Abs(Vy)>gravity*.045){Vy=-Math.Abs(Vy)*.62;Vx*=.82;}else{Vy=0;Vx*=Math.Exp(-5*dt);}}
            Vx*=Math.Exp(-.12*dt);
            if(Y>=bottom-.1&&Math.Abs(Vy)<gravity*.025&&Math.Abs(Vx)<gravity*.008){Y=bottom;Vx=Vy=0;Settled=true;break;}
        }
    }
}
