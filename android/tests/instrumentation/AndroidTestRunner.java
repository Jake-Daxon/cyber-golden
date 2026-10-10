package com.jakedaxon.cybergolden;

import android.app.*;
import android.content.*;
import android.graphics.Bitmap;
import android.os.*;
import android.provider.Settings;
import java.io.*;

/** Separate signed test APK: no test commands or permissions in the distributed app. */
public final class AndroidTestRunner extends Instrumentation {
    int checks;StringBuilder report=new StringBuilder();
    void check(boolean condition,String name){checks++;if(!condition)throw new AssertionError(name);report.append("PASS ").append(name).append('\n');}
    @Override public void onCreate(Bundle args){super.onCreate(args);start();}
    @Override public void onStart(){
        Bundle result=new Bundle();
        try{
            Context c=getTargetContext();
            MainActivity activity=(MainActivity)startActivitySync(new Intent(c,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            waitForIdleSync();check(activity!=null,"Chinese MainActivity starts");
            check(Settings.canDrawOverlays(c),"overlay permission granted in emulator");
            runOnMainSync(()->c.startForegroundService(new Intent(c,PetService.class)));
            for(int i=0;i<60&&PetService.instance==null;i++)SystemClock.sleep(500);
            waitForIdleSync();check(PetService.instance!=null,"foreground overlay service starts");
            PetService service=PetService.instance;
            runOnMainSync(()->{
                check(service.assets.frames.size()==20,"20 actual animation actions loaded");
                check(service.assets.poses.size()==3,"3 poses loaded");
                PetState s=service.state;s.food=50;service.command("eat");check(s.food==65,"feeding adds15");service.command("eat");check(s.food==80,"consecutive feeding without cooldown");service.command("eat");service.command("eat");check(s.food==100,"feeding cap100");long epoch=s.epoch;service.command("eat");check(s.food==100&&s.epoch>epoch,"full feeding still animates");
                service.command("pet");check(s.food==96,"ordinary interaction deducts4");
                service.fetch("ball");check(s.toyVisible,"toy appears");service.command("shake");check(!s.toyVisible&&s.action.equals("shake"),"active command interrupts toy");
                s.food=14;s.updateHunger(System.currentTimeMillis());service.refreshSettings();check(s.hungry,"hungry text state persists");
                s.paused=true;s.tick(System.currentTimeMillis(),.1,300,600);check(!s.centerAttempted,"pause blocks hunger move");s.paused=false;
                service.hide(true);check(s.hidden&&!s.toyVisible&&s.pendingSound==null,"hide cancels animations toys sounds");PetState saved=new Store(c).load();check(saved.hidden&&saved.hungry,"hidden hungry save restores");
                service.hide(false);s.pose="Lie";service.refreshSettings();saved=new Store(c).load();check(saved.pose.equals("Lie"),"selected pose saved");
                s.sound=false;service.command("pet");check(s.pendingSound==null,"global mute");s.sound=true;s.hungerSound=false;s.centerAttempted=true;s.nextHungerSound=0;s.tick(System.currentTimeMillis(),.1,300,600);check(s.pendingSound==null,"separate hunger sound switch");
                s.food=30;s.updateHunger(System.currentTimeMillis());service.refreshSettings();check(!s.hungry,"feeding clears persistent hunger");
                service.command("sleep");saved=new Store(c).load();check(saved.sleeping&&saved.action.equals("sleep"),"sleep save restores stable state");service.command("wake");check(!s.sleeping,"wake command");
                s.pose="FrontSit";s.food=80;s.sound=false;service.refreshSettings();
            });
            waitForIdleSync();SystemClock.sleep(1000);
            Bitmap screenshot=getUiAutomation().takeScreenshot();check(screenshot!=null,"real Android screen capture");
            if(screenshot!=null){try(ParcelFileDescriptor command=getUiAutomation().executeShellCommand("screencap -p /data/local/tmp/cybergolden-main.png");InputStream output=new FileInputStream(command.getFileDescriptor())){byte[] buffer=new byte[4096];while(output.read(buffer)!=-1){}}}
            runOnMainSync(()->{service.hide(true);activity.finish();});
            result.putString("stream",report.toString()+"PASS: "+checks+" Android runtime assertions\n");finish(Activity.RESULT_OK,result);
        }catch(Throwable failure){result.putString("stream",report+"FAIL: "+failure.toString()+"\n"+android.util.Log.getStackTraceString(failure));finish(Activity.RESULT_CANCELED,result);}
    }
}
