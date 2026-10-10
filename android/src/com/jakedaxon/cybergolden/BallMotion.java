package com.jakedaxon.cybergolden;

final class BallMotion {
    float x,y,vx,vy,radius=12;boolean held,thrown,settled;
    private float left,right,top,bottom,gravity;private long lastSample;
    void reset(float px,float py,float l,float r,float t,float b){x=px;y=py;left=l+radius;right=Math.max(left,r-radius);top=t+radius;bottom=Math.max(top,b-radius);gravity=980;vx=vy=0;held=thrown=settled=false;clamp();}
    private void clamp(){x=(float)PetState.clamp(x,left,right);y=(float)PetState.clamp(y,top,bottom);}
    void grab(float px,float py){held=true;settled=false;vx=vy=0;x=px;y=py;clamp();lastSample=System.nanoTime();}
    void hold(float px,float py){if(!held||Math.abs(px-x)+Math.abs(py-y)<.001)return;long now=System.nanoTime();double dt=PetState.clamp((now-lastSample)/1e9,.008,.15);vx=(float)(.4*vx+.6*PetState.clamp((px-x)/dt,-1568,1568));vy=(float)(.4*vy+.6*PetState.clamp((py-y)/dt,-1568,1568));x=px;y=py;clamp();lastSample=now;}
    void release(){if(!held)return;if((System.nanoTime()-lastSample)/1e9>.12)vx=vy=0;held=false;thrown=true;settled=false;}
    void tick(double elapsed){if(held||settled)return;double remaining=PetState.clamp(elapsed,0,.25);while(remaining>0){double dt=Math.min(remaining,1.0/120);remaining-=dt;vy+=gravity*dt;x+=vx*dt;y+=vy*dt;
        if(x<left){x=left;vx=Math.abs(vx)*.72f;}else if(x>right){x=right;vx=-Math.abs(vx)*.72f;}
        if(y<top){y=top;vy=Math.abs(vy)*.45f;}
        if(y>=bottom){y=bottom;if(Math.abs(vy)>gravity*.045){vy=-Math.abs(vy)*.62f;vx*=.82f;}else{vy=0;vx*=Math.exp(-5*dt);}}
        vx*=Math.exp(-.12*dt);
        if(y>=bottom-.1&&Math.abs(vy)<gravity*.025&&Math.abs(vx)<gravity*.008){y=bottom;vx=vy=0;settled=true;break;}
    }}
}
