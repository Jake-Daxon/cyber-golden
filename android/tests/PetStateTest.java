package com.jakedaxon.cybergolden;

public final class PetStateTest {
    static int checks;static void check(boolean value,String name){checks++;if(!value)throw new AssertionError(name);}
    static PetState state(){PetState s=new PetState();s.restore(1000000);s.autoMove=false;return s;}
    public static void main(String[] args){
        PetState s=state();check(s.pose.equals("FrontSit"),"default front sit");check(s.action.equals("idle"),"static default");
        s.food=50;s.interact("eat","feed-1",1000000);check(s.food==65,"feed +15");long epoch=s.epoch;s.interact("eat","feed-1",1000001);check(s.food==65&&s.epoch==epoch,"duplicate not credited or replayed");
        s.interact("eat","feed-2",1000002);check(s.food==80,"no feed cooldown");s.interact("eat","feed-3",1000003);s.interact("eat","feed-4",1000004);check(s.food==100,"food cap");epoch=s.epoch;s.interact("eat","feed-5",1000005);check(s.food==100&&s.epoch>epoch,"full feeding plays again");
        s.interact("pet",null,1000010);check(s.food==96,"active interaction costs 4");s.tick(1004010,4,300,600);check(s.action.equals("idle"),"interaction returns static");
        s=state();s.food=50;for(int i=1;i<=60;i++)s.tick(1000000+i*60000,60,300,600);check(Math.abs(s.food-48)<.0001,"runtime 2 per hour");
        s=state();s.food=50;s.savedAt=1;s.restore(24*3600000L);check(s.food==42,"offline cap 4 hours");s.food=6;s.savedAt=1;s.restore(24*3600000L);check(s.food==5,"offline floor");
        s=state();s.food=14;s.updateHunger(1000000);check(s.hungry,"hungry below15");s.food=29;s.updateHunger(1000001);check(s.hungry,"hysteresis keeps hunger");s.food=30;s.updateHunger(1000002);check(!s.hungry,"clear at30");
        s=state();s.food=14;s.x=10;s.y=10;s.tick(1000001,.01,300,600);check(s.centerAttempted&&s.action.equals("hunger"),"one hunger center attempt");check(s.visualAction().equals("trot"),"energetic real trot frames");
        for(int i=1;i<400;i++)s.tick(1000001+i*100,.1,300,600);check(s.action.equals("idle")&&Math.abs(s.x-150)<1&&Math.abs(s.y-300)<1,"center arrival and static");
        s.savedAt=1040001;s.restore(1040002);check(s.centerAttempted&&s.action.equals("idle"),"hunger episode survives restart");s.tick(1040003,.01,300,600);check(s.action.equals("idle"),"restart no repeated movement");
        s=state();s.food=14;s.energy=20;s.tick(1000001,.01,300,600);check(s.visualAction().equals("walk"),"low energy walking");s.interact("shake",null,1000002);check(s.action.equals("shake"),"manual interrupts hunger");s.tick(1003002,3,300,600);s.tick(1003003,.01,300,600);check(s.action.equals("idle"),"interrupted hunger not resumed");
        for(String mode:new String[]{"hidden","paused","sleeping","quiet","dragging","menu"}){s=state();s.food=14;if(mode.equals("hidden"))s.hidden=true;if(mode.equals("paused"))s.paused=true;if(mode.equals("sleeping"))s.sleeping=true;if(mode.equals("quiet"))s.quiet=true;if(mode.equals("dragging"))s.dragging=true;if(mode.equals("menu"))s.menu=true;s.tick(1000001,.01,300,600);check(!s.centerAttempted&&s.pendingSound==null,"blocked hunger "+mode);check(s.hungry,"hungry persists "+mode);}
        s=state();s.food=14;s.centerAttempted=true;s.hungry=true;s.nextHungerSound=1000000;s.tick(1000001,.01,300,600);check(s.pendingSound!=null,"hunger chirp");s.pendingSound=null;s.tick(1001001,1,300,600);check(s.pendingSound==null,"no chirp stacking");check(s.nextHungerSound==2800001,"30 minute interval");
        s=state();s.sound=false;s.food=14;s.hungry=true;s.centerAttempted=true;s.tick(1000001,.01,300,600);check(s.pendingSound==null,"global mute");s.sound=true;s.hungerSound=false;s.nextHungerSound=0;s.tick(1004001,3,300,600);check(s.pendingSound==null,"separate hunger mute");
        s=state();s.interact("eat","feed",1000000);s.setHidden(true,1000001);check(s.pendingSound==null&&!s.manual&&!s.toyVisible,"hide cancels transient");s.setHidden(false,1000002);check(s.action.equals("idle"),"show no stale feeding");
        s=state();s.interact("sleep",null,1000000);s.tick(1001200,1.2,300,600);check(s.sleeping&&s.action.equals("sleep"),"sleep transition");s.savedAt=1001200;s.restore(1001300);check(s.sleeping&&s.action.equals("sleep"),"restore sleep");s.interact("wake",null,1001301);check(!s.sleeping&&s.action.equals("idle"),"wake");
        s=state();s.x=10;s.y=20;s.startToy("ball",300,600,1000000);for(int i=1;i<200;i++)s.tick(1000000+i*100,.1,300,600);check(!s.toyVisible&&s.action.equals("idle")&&Math.abs(s.x-10)<1,"fetch returns home and idle");
        s=state();s.startToy("plush",300,600,1000000);s.interact("eat","interrupt",1000001);check(!s.toyVisible&&!s.toyCarried,"feeding interrupts toy");
        s=state();s.autoMove=true;s.x=0;s.y=0;s.nextRoam=1000001;s.tick(1000002,.01,300,600);check(s.moving(),"autonomous roam retained");
        s=state();s.food=14;s.tick(1600001,.01,300,600);check(s.quiet&&!s.centerAttempted,"10min quiet suppresses hunger");
        s=state();s.pose="Lie";s.interact("pet",null,1000000);s.tick(1003000,3,300,600);check(s.pose.equals("Lie")&&s.action.equals("idle"),"chosen pose retained");
        System.out.println("PASS: "+checks+" Android behavior assertions");
    }
}
