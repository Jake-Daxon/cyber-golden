package com.jakedaxon.cybergolden;

/** Platform-independent adaptation of the existing Windows BehaviorEngine rules.
 * Artwork, timing and sounds are shared; no WPF assembly runs on Android. */
public final class PetState {
    public String name="赛博金毛", pose="FrontSit", action="idle", lastFeed="";
    public double food=80, water=80, energy=80, mood=80, affection=0;
    public boolean hungry, centerAttempted, hidden, paused, sleeping, quiet;
    public boolean sound=true, hungerSound=true, autoMove=true, staticIdle=true;
    public int volume=20, size=144;
    public float x=-1,y=-1,targetX,targetY,homeX,homeY,toyX,toyY;
    public boolean faceRight, manual, dragging, menu, toyVisible, toyCarried;
    public String toy="ball";
    public long savedAt, nextHungerSound, lastSound, lastInput, nextRoam, deadline, epoch;
    public String pendingSound;

    public void normalize() {
        food=clamp(food,0,100); water=clamp(water,0,100); energy=clamp(energy,0,100);
        mood=clamp(mood,0,100); affection=clamp(affection,0,100);
        size=(int)clamp(size,96,220); volume=(int)clamp(volume,0,100);
        if(!pose.equals("FrontSit")&&!pose.equals("Stand")&&!pose.equals("Lie"))pose="FrontSit";
        if(name==null||name.trim().isEmpty())name="赛博金毛";
        if(name.length()>24)name=name.substring(0,24);
    }
    public static double clamp(double v,double lo,double hi){return Double.isFinite(v)?Math.max(lo,Math.min(hi,v)):lo;}
    public void restore(long now) {
        normalize();
        if(savedAt>0){double hours=clamp((now-savedAt)/3600000.0,0,4);food=Math.max(5,food-hours*2);if(sleeping)energy=clamp(energy+hours*6,0,100);}
        updateHunger(now); cancel(); stable(); lastInput=now;nextRoam=now+180000;
        savedAt=now;
    }
    public void updateHunger(long now) {
        if(!hungry&&food<15){hungry=true;centerAttempted=false;nextHungerSound=now;}
        else if(hungry&&food>=30){hungry=false;centerAttempted=false;nextHungerSound=0;}
    }
    public void cancel(){epoch++;manual=false;dragging=false;toyVisible=false;toyCarried=false;pendingSound=null;}
    public void stable(){action=sleeping?"sleep":"idle";deadline=Long.MAX_VALUE;manual=false;}
    public void sound(String key,long now){if(sound&&volume>0&&now-lastSound>=3000){pendingSound=key;lastSound=now;}}
    public void interact(String command,String request,long now) {
        if(hidden)return;
        if(command.equals("eat")&&request!=null&&request.equals(lastFeed))return;
        cancel();quiet=false;lastInput=now;nextRoam=now+180000;
        if(command.equals("sleep")){sleeping=true;action="liedown";manual=true;deadline=now+1100;return;}
        if(command.equals("wake")){sleeping=false;stable();return;}
        sleeping=false;
        if(command.equals("eat")){lastFeed=request==null?"":request;food=clamp(food+15,0,100);energy=clamp(energy+3,0,100);sound("eat",now);}
        else {food=Math.max(0,food-4);}
        if(command.equals("drink")){water=clamp(water+22,0,100);sound("drink",now);}
        if(command.equals("pet")){mood=clamp(mood+3,0,100);affection=clamp(affection+.6,0,100);sound("click",now);}
        if(command.equals("shake"))affection=clamp(affection+.7,0,100);
        if(command.equals("jump")||command.equals("wag")){mood=clamp(mood+2,0,100);sound("happy",now);}
        action=command;manual=true;deadline=now+(command.equals("eat")||command.equals("drink")?3200:2000);updateHunger(now);
    }
    public void dragStart(long now){cancel();sleeping=false;quiet=false;dragging=true;lastInput=now;food=Math.max(0,food-4);updateHunger(now);action="idle";}
    public void dragEnd(long now){dragging=false;stable();lastInput=now;nextRoam=now+180000;}
    public void setHidden(boolean value,long now){cancel();hidden=value;quiet=false;stable();lastInput=now;nextRoam=now+180000;}
    public void setPaused(boolean value,long now){cancel();paused=value;stable();lastInput=now;nextRoam=now+180000;}
    public void startToy(String kind,float maxX,float maxY,long now){
        interact("wag",null,now);pendingSound=null;toy=kind;toyVisible=true;homeX=x;homeY=y;
        targetX=(float)clamp(x+(x<maxX/2?110:-110),0,maxX);targetY=y;toyX=targetX;toyY=y;
        action="watchtoy";deadline=now+650;
    }
    public boolean active(){return !hidden&&!paused&&!sleeping&&!quiet&&!dragging&&!menu;}
    public boolean moving(){return action.equals("walk")||action.equals("trot")||action.equals("hunger")||action.equals("chase")||action.equals("carry");}
    public boolean move(double dt,float speed){
        double dx=targetX-x,dy=targetY-y,d=Math.hypot(dx,dy),step=speed*dt;
        faceRight=dx>0;
        if(d<=step+1){x=targetX;y=targetY;return true;}
        x+=(float)(dx/d*step);y+=(float)(dy/d*step);return false;
    }
    public void tick(long now,double dt,float maxX,float maxY){
        double elapsed=clamp(dt,0,14400);
        food=Math.max(elapsed>60?5:0,food-elapsed*2/3600);
        dt=clamp(dt,0,60);updateHunger(now);
        if(hungry&&(!active()||now-lastInput>=600000)){
            if(nextHungerSound<=now)nextHungerSound=now+1800000;
        }
        if(hidden||dragging||menu)return;
        if(paused&&!manual)return;
        energy=clamp(energy+dt*(sleeping?.055:-.001),0,100);
        if(manual&&now>=deadline){
            if(action.equals("watchtoy")){action="chase";deadline=Long.MAX_VALUE;}
            else if(action.equals("pickup")){toyCarried=true;action="carry";targetX=homeX;targetY=homeY;deadline=Long.MAX_VALUE;}
            else if(action.equals("drop")){toyVisible=false;toyCarried=false;action="wag";deadline=now+1200;sound("happy",now);}
            else {cancel();stable();}
        }
        if(moving()){
            boolean reached=move(Math.min(dt,.1),(action.equals("walk")||energy<30)?18:70);
            if(reached){
                if(action.equals("chase")){action="pickup";deadline=now+950;}
                else if(action.equals("carry")){toyCarried=false;action="drop";deadline=now+950;}
                else if(action.equals("hunger")){stable();hungerVoice(now);}
                else stable();
            }
            return;
        }
        if(manual||!active())return;
        if(now-lastInput>=600000){quiet=true;stable();return;}
        if(!action.equals("idle"))return;
        if(hungry){
            if(!centerAttempted){centerAttempted=true;nextHungerSound=now+1800000;targetX=maxX/2;targetY=maxY/2;action="hunger";epoch++;}
            else if(now>=nextHungerSound)hungerVoice(now);
            return;
        }
        if(autoMove&&now>=nextRoam){nextRoam=now+210000;targetX=(float)clamp(x+(x<maxX/2?65:-65),0,maxX);targetY=y;action=energy>=45?"trot":"walk";}
    }
    private void hungerVoice(long now){nextHungerSound=now+1800000;if(hungerSound)sound("click",now);}
    public String visualAction(){
        if(action.equals("hunger")||action.equals("chase"))return energy>=30?"trot":"walk";
        if(action.equals("watchtoy"))return "expect";
        if(action.equals("drop"))return "pickup";
        return action;
    }
}
