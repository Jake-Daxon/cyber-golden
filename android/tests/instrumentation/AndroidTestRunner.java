package com.jakedaxon.cybergolden;

import android.app.*;
import android.content.*;
import android.graphics.Bitmap;
import android.os.*;
import android.provider.Settings;
import java.io.*;
import android.widget.*;

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
            verifyScrollableMenu(service);
            waitForIdleSync();SystemClock.sleep(1000);
            Bitmap screenshot=getUiAutomation().takeScreenshot();check(screenshot!=null,"real Android screen capture");
            if(screenshot!=null){try(ParcelFileDescriptor command=getUiAutomation().executeShellCommand("screencap -p /data/local/tmp/cybergolden-main.png");InputStream output=new FileInputStream(command.getFileDescriptor())){byte[] buffer=new byte[4096];while(output.read(buffer)!=-1){}}}
            runOnMainSync(()->{service.hide(true);activity.finish();});
            result.putString("stream",report.toString()+"PASS: "+checks+" Android runtime assertions\n");finish(Activity.RESULT_OK,result);
        }catch(Throwable failure){result.putString("stream",report+"FAIL: "+failure.toString()+"\n"+android.util.Log.getStackTraceString(failure));finish(Activity.RESULT_CANCELED,result);}
    }
    private PopupWindow popup(PetService s)throws Exception{java.lang.reflect.Field f=PetService.class.getDeclaredField("menu");f.setAccessible(true);return (PopupWindow)f.get(s);}
    private void openMenu(PetService s)throws Exception{java.lang.reflect.Method m=PetService.class.getDeclaredMethod("showMenu");m.setAccessible(true);runOnMainSync(()->{try{m.invoke(s);}catch(Exception e){throw new RuntimeException(e);}});waitForIdleSync();}
    private void screenshot(String name)throws Exception{try(ParcelFileDescriptor command=getUiAutomation().executeShellCommand("screencap -p /data/local/tmp/"+name);InputStream output=new FileInputStream(command.getFileDescriptor())){byte[] buffer=new byte[4096];while(output.read(buffer)!=-1){}}}
    private void verifyScrollableMenu(PetService service)throws Exception{
        openMenu(service);PopupWindow p=popup(service);
        check(p.isShowing()&&p.getContentView() instanceof ScrollView,"long press opens scrollable menu");
        ScrollView scroll=(ScrollView)p.getContentView();LinearLayout list=(LinearLayout)scroll.getChildAt(0);
        check(list.getChildCount()==PetService.MENU_ACTIONS.length+1,"all 23 menu entries present");
        check(!p.isFocusable(),"menu does not take keyboard focus");
        check(scroll.canScrollVertically(1),"actions extend beyond viewport and can scroll");
        check(p.getHeight()<service.getResources().getDisplayMetrics().heightPixels,"menu height bounded to screen");
        screenshot("cybergolden-menu-top.png");
        runOnMainSync(()->scroll.scrollTo(0,list.getHeight()));waitForIdleSync();
        check(scroll.getScrollY()>0&&!scroll.canScrollVertically(1),"scroll reaches last actions");
        screenshot("cybergolden-menu-bottom.png");
        runOnMainSync(()->{service.command("wake");service.state.pose="Lie";service.state.food=100;service.state.energy=80;service.refreshSettings();});
        for(int i=0;i<PetService.MENU_ACTIONS.length;i++){
            openMenu(service);PopupWindow current=popup(service);LinearLayout choices=(LinearLayout)((ScrollView)current.getContentView()).getChildAt(0);
            String action=PetService.MENU_ACTIONS[i][1];Button selected=(Button)choices.getChildAt(i+1);
            runOnMainSync(()->{
                selected.performClick();PetState s=service.state;
                check(!current.isShowing()&&!s.menu,"menu closes after "+action);
                if(action.equals("ball")||action.equals("plush"))check(s.toyVisible&&s.toy.equals(action),"toy menu routes "+action);
                else if(action.equals("sleep"))check(s.sleeping,"menu sleeps");
                else if(action.equals("wake")||action.equals("stable"))check(!s.sleeping&&s.action.equals("idle")&&s.pose.equals("Lie"),"menu returns selected pose "+action);
                else if(action.equals("hide")){check(s.hidden,"menu hides dog");service.hide(false);}
                else if(!action.equals("close"))check(s.action.equals(action),"menu invokes "+action);
                if(action.equals("walk")||action.equals("trot"))check(Math.abs(s.targetX-s.x)<=80.01&&s.targetY==s.y,"manual movement bounded "+action);
            });
        }
        openMenu(service);PopupWindow interrupted=popup(service);runOnMainSync(()->service.command("eat"));
        check(!interrupted.isShowing()&&!service.state.menu&&service.state.action.equals("eat"),"new active command dismisses menu");
        runOnMainSync(()->{service.state.pose="FrontSit";service.state.food=80;service.refreshSettings();});
    }
}
