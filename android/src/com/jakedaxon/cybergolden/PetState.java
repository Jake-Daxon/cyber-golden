package com.jakedaxon.cybergolden;

/** Platform-independent adaptation of the existing Windows BehaviorEngine rules.
 * Artwork, timing and sounds are shared; no WPF assembly runs on Android. */
public final class PetState {
    public String name="赛博金毛", pose="Lie", action="idle", lastFeed="";
    public double food=80, water=80, energy=80, mood=80, affection=0;
    public boolean hungry, centerAttempted, hidden, paused, sleeping, quiet;
    public boolean quietMode;
    public boolean hungerAnnounced;
    final BallMotion ball=new BallMotion();
    private float areaWidth,areaHeight;
    public int behaviorRevision,hungerDrags,roamSeconds=180;
    private boolean resumeHunger;
    private final java.util.Random random=new java.util.Random();
    private static final String[] CLICK_POOL={"pet","shake","eat","drink","wag","jump","tilt","lick","yawn","stretch","sitdown","liedown","companion","expect","sleep","wake","walk","trot","ball","plush"};
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
        if(behaviorRevision<4){if(pose.equals("FrontSit"))pose="Lie";behaviorRevision=4;}
        staticIdle=true;roamSeconds=(int)clamp(roamSeconds,5,1000);
        if(!pose.equals("FrontSit")&&!pose.equals("Stand")&&!pose.equals("Lie"))pose="Lie";
        if(name==null||name.trim().isEmpty())name="赛博金毛";
        if(name.length()>24)name=name.substring(0,24);
    }
    public static double clamp(double v,double lo,double hi){return Double.isFinite(v)?Math.max(lo,Math.min(hi,v)):lo;}
    public void restore(long now) {
        normalize();
        if(savedAt>0){double hours=clamp((now-savedAt)/3600000.0,0,4);food=Math.max(5,food-hours*2);if(sleeping)energy=clamp(energy+hours*6,0,100);}
        updateHunger(now); cancel(); stable(); lastInput=now;nextRoam=now+roamSeconds*1000L;
        savedAt=now;
    }
    public void updateHunger(long now) {
        if(!hungry&&food<15){hungry=true;centerAttempted=false;hungerDrags=0;hungerAnnounced=false;nextHungerSound=0;}
        else if(hungry&&food>=30){hungry=false;centerAttempted=false;hungerDrags=0;hungerAnnounced=false;nextHungerSound=0;}
    }
    public void cancel(){epoch++;manual=false;dragging=false;toyVisible=false;toyCarried=false;pendingSound=null;resumeHunger=false;}
    public void stable(){action=sleeping?"sleep":"idle";deadline=Long.MAX_VALUE;manual=false;}
    public void sound(String key,long now){if(quietMode&&(key.equals("click")||key.equals("happy")||key.equals("hunger-two")||key.equals("fetch")))return;if(sound&&volume>0&&(key.equals("hunger-two")||key.equals("fetch")||now-lastSound>=3000)){pendingSound=key;if(!key.equals("hunger-two"))lastSound=now;}}
    public boolean canClick(){return !hidden&&!sleeping&&!dragging&&!menu&&!manual&&action.equals("idle");}
    public String clickChoice(float x,float y){
        if(!canClick())return null;
        boolean lie=pose.equals("Lie"),stand=pose.equals("Stand");
        double cx=lie?.30:stand?.22:.455,cy=lie?.50:stand?.37:.27,rx=lie?.28:stand?.23:.34,ry=lie?.20:stand?.23:.26;
        boolean head=Math.pow((x-cx)/rx,2)+Math.pow((y-cy)/ry,2)<=1;
        boolean paws=lie?x>=.04&&x<=.64&&y>=.82&&y<=.96:stand?x>=.12&&x<=.48&&y>=.83&&y<=.96:x>=.14&&x<=.72&&y>=.83&&y<=.96;
        return head?"pet":paws?"shake":CLICK_POOL[random.nextInt(CLICK_POOL.length)];
    }
    public void interact(String command,String request,long now) {
        if(hidden)return;
        if(command.equals("eat")&&request!=null&&request.equals(lastFeed))return;
        cancel();quiet=false;lastInput=now;nextRoam=now+roamSeconds*1000L;
        if(command.equals("sleep")){sleeping=true;action="liedown";manual=true;deadline=now+1100;return;}
        if(command.equals("wake")){sleeping=false;stable();return;}
        sleeping=false;
        if(command.equals("eat")){lastFeed=request==null?"":request;food=clamp(food+15,0,100);energy=clamp(energy+3,0,100);sound("eat",now);}
        else {food=Math.max(0,food-4);}
        if(command.equals("drink")){water=clamp(water+22,0,100);sound("drink",now);}
        if(command.equals("pet")){mood=clamp(mood+3,0,100);affection=clamp(affection+.6,0,100);sound("click",now);}
        if(command.equals("shake"))affection=clamp(affection+.7,0,100);
        if(command.equals("jump")||command.equals("wag")){mood=clamp(mood+2,0,100);sound("happy",now);}
        action=command;manual=true;deadline=now+(command.equals("eat")||command.equals("drink")?3200:command.equals("jump")||command.equals("sitdown")||command.equals("liedown")||command.equals("companion")?6000:2000);updateHunger(now);
    }
    public void dragStart(long now){boolean hunger=action.equals("hunger");if(hunger)hungerDrags++;cancel();resumeHunger=hunger&&hungerDrags<=2;sleeping=false;quiet=false;dragging=true;lastInput=now;food=Math.max(0,food-2);updateHunger(now);action="drag";}
    public void dragEnd(long now){dragging=false;if(resumeHunger&&hungry&&!hidden&&!paused&&!quietMode){resumeHunger=false;action="hunger";deadline=Long.MAX_VALUE;epoch++;}else{resumeHunger=false;stable();}lastInput=now;nextRoam=now+roamSeconds*1000L;}
    public void setHidden(boolean value,long now){cancel();hidden=value;quiet=false;stable();lastInput=now;nextRoam=now+roamSeconds*1000L;}
    public void setPaused(boolean value,long now){cancel();paused=value;quiet=false;stable();lastInput=now;nextRoam=now+roamSeconds*1000L;}
    public void startToy(String kind,float maxX,float maxY,long now){
        interact("wag",null,now);pendingSound=null;toy=kind;toyVisible=true;homeX=x;homeY=y;
        areaWidth=maxX+size;areaHeight=maxY+size;
        if(toy.equals("ball")){toyX=x+size*.75f;toyY=y+size*.82f;ball.reset(toyX,toyY,4,areaWidth-4,4,areaHeight-4);action="watchtoy";deadline=Long.MAX_VALUE;epoch++;return;}
        targetX=(float)clamp(x+(x<maxX/2?110:-110),0,maxX);targetY=y;toyX=targetX;toyY=y;
        action="watchtoy";deadline=now+650;
    }
    void grabBall(float px,float py,long now){if(!toyVisible||!toy.equals("ball")||hidden)return;pendingSound=null;toyCarried=false;ball.grab(px,py);toyX=ball.x;toyY=ball.y;manual=true;action="watchtoy";deadline=Long.MAX_VALUE;epoch++;lastInput=now;}
    void holdBall(float px,float py){if(!toyVisible||!ball.held)return;ball.hold(px,py);toyX=ball.x;toyY=ball.y;lastInput=System.currentTimeMillis();}
    void releaseBall(){if(toyVisible&&toy.equals("ball")){ball.release();lastInput=System.currentTimeMillis();}}
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
        if(hidden||dragging||menu)return;
        if(paused&&!manual)return;
        if(action.equals("watchtoy")&&toy.equals("ball")&&now-lastInput>=600000){cancel();quiet=true;stable();return;}
        if(toyVisible&&toy.equals("ball")&&!toyCarried&&(action.equals("watchtoy")||action.equals("chase")||action.equals("pickup"))){
            ball.tick(dt);toyX=ball.x;toyY=ball.y;
            if(action.equals("watchtoy")&&ball.thrown&&ball.settled&&!ball.held){targetX=(float)clamp(toyX-size*.5,0,maxX);targetY=(float)clamp(toyY-size*.9,0,maxY);action="chase";deadline=Long.MAX_VALUE;epoch++;return;}
        }
        energy=clamp(energy+dt*(sleeping?.055:-.001),0,100);
        if(manual&&now>=deadline){
            if(action.equals("watchtoy")){action="chase";deadline=Long.MAX_VALUE;}
            else if(action.equals("pickup")){toyCarried=true;action="carry";targetX=homeX;targetY=homeY;deadline=Long.MAX_VALUE;epoch++;}
            else if(action.equals("drop")){toyCarried=false;if(toy.equals("ball")){toyX=x+size*.75f;toyY=y+size*.82f;ball.reset(toyX,toyY,4,maxX+size-4,4,maxY+size-4);action="watchtoy";deadline=Long.MAX_VALUE;epoch++;}else{toyVisible=false;action="wag";deadline=now+1200;sound("happy",now);}}
            else {cancel();stable();nextRoam=now+roamSeconds*1000L;}
        }
        if(moving()){
            boolean reached=move(Math.min(dt,.1),(action.equals("walk")||energy<30)?18:70);
            if(reached){
                if(action.equals("chase")){if(toy.equals("ball"))sound("fetch",now);action="pickup";deadline=now+950;epoch++;}
                else if(action.equals("carry")){if(toy.equals("ball"))sound("fetch",now);toyCarried=false;action="drop";deadline=now+950;epoch++;}
                else if(action.equals("hunger")){stable();hungerVoice(now);}
                else {stable();nextRoam=now+roamSeconds*1000L;}
            }
            return;
        }
        if(manual||!active())return;
        if(now-lastInput>=600000){quiet=true;stable();return;}
        if(!action.equals("idle"))return;
        if(hungry&&!quietMode){
            if(!centerAttempted){centerAttempted=true;targetX=maxX/2;targetY=maxY/2;action="hunger";epoch++;}
            else hungerVoice(now);
            return;
        }
        if(autoMove&&!quietMode&&now>=nextRoam){nextRoam=now+roamSeconds*1000L;targetX=(float)clamp(x+(x<maxX/2?65:-65),0,maxX);targetY=y;action=energy>=45?"trot":"walk";}
    }
    private void hungerVoice(long now){if(!hungerAnnounced&&hungerSound&&sound&&volume>0&&!quietMode){hungerAnnounced=true;sound("hunger-two",now);}}
    public String visualAction(){
        if(action.equals("hunger")||action.equals("chase"))return energy>=30?"trot":"walk";
        if(action.equals("watchtoy"))return "expect";
        if(action.equals("drop"))return "pickup";
        return action;
    }
}
