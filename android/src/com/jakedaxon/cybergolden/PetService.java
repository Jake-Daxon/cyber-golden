package com.jakedaxon.cybergolden;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.graphics.*;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import java.util.UUID;

public final class PetService extends Service {
    public static PetService instance;
    PetState state;Store store;PetAssets assets;
    private WindowManager windows;private WindowManager.LayoutParams params,bubbleParams,toyParams;
    private PetView pet;private TextView bubble;private View toy;private PopupWindow menu;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private float density=1,maxX,maxY,downX,downY,originX,originY;
    private long lastTick,lastSave;private boolean screenOn=true,added,bubbleAdded,toyAdded;
    private Rect bounds=new Rect();private boolean oldHungry,oldAttempt;
    private static final int NOTICE=100;
    private final BroadcastReceiver screen=new BroadcastReceiver(){@Override public void onReceive(Context c,Intent i){
        screenOn=!Intent.ACTION_SCREEN_OFF.equals(i.getAction());state.cancel();state.stable();assets.stop();state.quiet=!screenOn;state.lastInput=System.currentTimeMillis();persist();render();
    }};
    @Override public void onCreate(){
        super.onCreate();instance=this;store=new Store(this);state=store.load();assets=new PetAssets(this);
        windows=(WindowManager)getSystemService(WINDOW_SERVICE);density=getResources().getDisplayMetrics().density;
        NotificationManager nm=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);
        nm.createNotificationChannel(new NotificationChannel("companion","桌宠运行与控制",NotificationManager.IMPORTANCE_LOW));
        try{if(Build.VERSION.SDK_INT>=34)startForeground(NOTICE,notification(),ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);else startForeground(NOTICE,notification());}
        catch(Exception e){android.util.Log.e("CyberGolden","Foreground service failed",e);stopSelf();return;}
        pet=new PetView(this,state,assets);bubble=new TextView(this);bubble.setText("我饿了");bubble.setTextSize(14);bubble.setTextColor(Color.rgb(71,50,36));bubble.setGravity(Gravity.CENTER);
        android.graphics.drawable.GradientDrawable bg=new android.graphics.drawable.GradientDrawable();bg.setColor(Color.rgb(255,247,237));bg.setCornerRadius(16*density);bg.setStroke((int)density,Color.rgb(216,180,142));bubble.setBackground(bg);
        toy=new View(this){private final Paint p=new Paint(3);@Override protected void onDraw(Canvas c){p.setColor(state.toy.equals("ball")?Color.rgb(152,185,65):Color.rgb(194,136,98));if(state.toy.equals("ball"))c.drawCircle(getWidth()/2f,getHeight()/2f,getWidth()*.44f,p);else c.drawRoundRect(0,0,getWidth(),getHeight(),getWidth()*.3f,getHeight()*.3f,p);}};
        int type=WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY;
        int flags=WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL;
        params=new WindowManager.LayoutParams((int)(state.size*density),(int)(state.size*density),type,flags,PixelFormat.TRANSLUCENT);params.gravity=Gravity.TOP|Gravity.LEFT;
        bubbleParams=new WindowManager.LayoutParams((int)(86*density),(int)(36*density),type,flags|WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,PixelFormat.TRANSLUCENT);bubbleParams.gravity=params.gravity;
        toyParams=new WindowManager.LayoutParams((int)(24*density),(int)(24*density),type,flags|WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,PixelFormat.TRANSLUCENT);toyParams.gravity=params.gravity;
        // Android 12+ blocks touches behind untrusted opaque overlays, including
        // NOT_TOUCHABLE windows. Keep combined companion opacity below 0.8.
        bubbleParams.alpha=.70f;toyParams.alpha=.25f;
        pet.setOnApplyWindowInsetsListener((v,insets)->{handler.post(this::workspace);return insets;});
        pet.touch=new PetView.Touch(){
            public void down(float x,float y){downX=x;downY=y;originX=state.x;originY=state.y;state.cancel();state.stable();assets.stop();}
            public void move(float x,float y){if(!state.dragging)state.dragStart(System.currentTimeMillis());state.x=originX+(x-downX)/density;state.y=originY+(y-downY)/density;clamp();render();}
            public void up(boolean moved){if(moved){state.dragEnd(System.currentTimeMillis());persist();render();}else command("pet");}
            public void menu(){showMenu();}
        };
        workspace();
        IntentFilter filter=new IntentFilter();filter.addAction(Intent.ACTION_SCREEN_OFF);filter.addAction(Intent.ACTION_SCREEN_ON);
        if(Build.VERSION.SDK_INT>=33)registerReceiver(screen,filter,Context.RECEIVER_NOT_EXPORTED);else registerReceiver(screen,filter);
        lastTick=SystemClock.elapsedRealtime();handler.post(ticker);
    }
    private final Runnable ticker=new Runnable(){public void run(){
        if(instance!=PetService.this||pet==null)return;
        long monotonic=SystemClock.elapsedRealtime(),now=System.currentTimeMillis();double dt=(monotonic-lastTick)/1000.0;lastTick=monotonic;
        oldHungry=state.hungry;oldAttempt=state.centerAttempted;String before=state.action;
        if(!screenOn)state.quiet=true;
        state.tick(now,dt,maxX,maxY);
        // Record this hunger attempt BEFORE rendering the first moving pixel.
        if(oldHungry!=state.hungry||oldAttempt!=state.centerAttempted||now-lastSave>=30000)persist();
        if(state.hidden||!screenOn||!state.sound)assets.stop();
        if(state.pendingSound!=null){String sound=state.pendingSound;state.pendingSound=null;if(!state.hidden&&screenOn)assets.play(sound,state.volume);persist();}
        if(!before.equals(state.action)||state.moving()||!state.action.equals("idle"))render();else updateCompanions();
        handler.postDelayed(this,(!state.hidden&&screenOn&&!state.action.equals("idle")&&!state.paused)?33:1000);
    }};
    private Notification notification(){
        PendingIntent open=PendingIntent.getActivity(this,1,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
        return new Notification.Builder(this,"companion").setSmallIcon(android.R.drawable.ic_menu_myplaces).setContentTitle(state==null?"赛博金毛":state.name)
            .setContentText(state!=null&&state.hidden?"已隐藏 · 点击恢复或调整":"正在静静陪伴 · 点击进入互动与设置").setContentIntent(open).setOngoing(true)
            .addAction(new Notification.Action.Builder(null,state!=null&&state.hidden?"显示":"隐藏",serviceIntent("toggle",2)).build())
            .addAction(new Notification.Action.Builder(null,"关闭桌宠",serviceIntent("stop",3)).build()).build();
    }
    private PendingIntent serviceIntent(String action,int id){return PendingIntent.getService(this,id,new Intent(this,PetService.class).setAction(action),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);}
    @Override public int onStartCommand(Intent intent,int flags,int startId){
        if(pet==null)return START_NOT_STICKY;
        if(intent!=null){String a=intent.getAction();if("stop".equals(a)){stopSelf();return START_NOT_STICKY;}if("toggle".equals(a))hide(!state.hidden);if("show".equals(a))hide(false);}
        if(!Settings.canDrawOverlays(this)){Toast.makeText(this,"请先允许赛博金毛显示悬浮窗",Toast.LENGTH_LONG).show();stopSelf();return START_NOT_STICKY;}
        render();return START_NOT_STICKY;
    }
    void persist(){if(!store.save(state)){android.util.Log.e("CyberGolden","Save failed; autonomy paused");state.paused=true;}lastSave=System.currentTimeMillis();}
    public void command(String command){
        dismissMenu();assets.stop();
        if(command.equals("trot")&&state.energy<30)command="walk";
        state.interact(command,UUID.randomUUID().toString(),System.currentTimeMillis());
        if(command.equals("walk")||command.equals("trot")){
            state.targetX=(float)PetState.clamp(state.x+(state.x<maxX/2?80:-80),0,maxX);
            state.targetY=state.y;
        }
        persist();render();
    }
    public void fetch(String kind){dismissMenu();assets.stop();state.startToy(kind,maxX,maxY,System.currentTimeMillis());persist();render();}
    public void hide(boolean value){dismissMenu();state.setHidden(value,System.currentTimeMillis());assets.stop();persist();render();((NotificationManager)getSystemService(NOTIFICATION_SERVICE)).notify(NOTICE,notification());}
    public void refreshSettings(){state.normalize();state.cancel();state.stable();assets.stop();workspace();persist();render();}
    private void clamp(){state.x=(float)PetState.clamp(state.x,0,maxX);state.y=(float)PetState.clamp(state.y,0,maxY);state.targetX=(float)PetState.clamp(state.targetX,0,maxX);state.targetY=(float)PetState.clamp(state.targetY,0,maxY);}
    private void workspace(){
        if(params==null)return;
        Rect newBounds;int l=0,t=0,r=0,b=0;
        if(Build.VERSION.SDK_INT>=30){WindowMetrics wm=windows.getCurrentWindowMetrics();newBounds=wm.getBounds();Insets in=wm.getWindowInsets().getInsetsIgnoringVisibility(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());l=in.left;t=in.top;r=in.right;b=in.bottom;}
        else {android.util.DisplayMetrics dm=new android.util.DisplayMetrics();windows.getDefaultDisplay().getRealMetrics(dm);newBounds=new Rect(0,0,dm.widthPixels,dm.heightPixels);int id=getResources().getIdentifier("status_bar_height","dimen","android");t=id>0?getResources().getDimensionPixelSize(id):0;id=getResources().getIdentifier("navigation_bar_height","dimen","android");b=id>0?getResources().getDimensionPixelSize(id):0;}
        Rect safe=new Rect(newBounds.left+l,newBounds.top+t,newBounds.right-r,newBounds.bottom-b);
        if(!safe.equals(bounds)&&!bounds.isEmpty()){state.cancel();state.stable();}
        bounds=safe;params.width=params.height=(int)(state.size*density);
        maxX=Math.max(0,bounds.width()/density-state.size);maxY=Math.max(0,bounds.height()/density-state.size);
        if(state.x<0||state.y<0){state.x=maxX*.7f;state.y=maxY*.72f;}clamp();render();
    }
    private void render(){
        if(pet==null)return;
        boolean visible=!state.hidden&&screenOn&&Settings.canDrawOverlays(this);
        try{
            if(visible){params.x=(int)(state.x*density);params.y=(int)(state.y*density);if(!added){windows.addView(pet,params);added=true;}else windows.updateViewLayout(pet,params);pet.changed();}
            else if(added){windows.removeView(pet);added=false;}
            updateCompanions();
        }catch(Exception e){android.util.Log.e("CyberGolden","Overlay permission or window lost",e);stopSelf();}
    }
    private void updateCompanions(){
        if(pet==null)return;
        try{
            if(added&&state.hungry){
                bubbleParams.x=(int)PetState.clamp(params.x+params.width*.45,0,Math.max(0,bounds.width()-bubbleParams.width));
                bubbleParams.y=(int)PetState.clamp(params.y-36*density,0,Math.max(0,bounds.height()-bubbleParams.height));
                if(!bubbleAdded){windows.addView(bubble,bubbleParams);bubbleAdded=true;}else windows.updateViewLayout(bubble,bubbleParams);
            }else if(bubbleAdded){windows.removeView(bubble);bubbleAdded=false;}
            if(added&&state.toyVisible&&!state.toyCarried){toyParams.x=(int)((state.toyX+state.size*.7)*density);toyParams.y=(int)((state.toyY+state.size*.84)*density);if(!toyAdded){windows.addView(toy,toyParams);toyAdded=true;}else windows.updateViewLayout(toy,toyParams);toy.invalidate();}
            else if(toyAdded){windows.removeView(toy);toyAdded=false;}
        }catch(Exception e){android.util.Log.w("CyberGolden","Companion overlay unavailable",e);}
    }
    static final String[][] MENU_ACTIONS={
        {"喂食 +15","eat"},{"喝水","drink"},{"摸摸头","pet"},{"握手","shake"},
        {"摇尾巴","wag"},{"开心蹦跳","jump"},{"歪头","tilt"},{"舔鼻子","lick"},
        {"打哈欠","yawn"},{"伸懒腰","stretch"},{"期待撒娇","expect"},
        {"坐下观察","sitdown"},{"趴下","liedown"},{"趴下休息","companion"},
        {"睡觉","sleep"},{"叫醒","wake"},{"玩网球","ball"},{"玩玩偶","plush"},
        {"散步","walk"},{"小跑","trot"},{"回到选定姿势","stable"},
        {"隐藏狗狗","hide"},{"关闭菜单","close"}
    };
    private void showMenu(){
        if(!added)return;state.cancel();state.menu=true;state.stable();assets.stop();
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setVerticalScrollBarEnabled(true);scroll.setScrollbarFadingEnabled(false);scroll.setContentDescription("全部互动动作，上下滑动选择");
        LinearLayout buttons=new LinearLayout(this);buttons.setOrientation(LinearLayout.VERTICAL);buttons.setPadding((int)(10*density),(int)(8*density),(int)(10*density),(int)(8*density));
        TextView title=new TextView(this);title.setText("全部互动 · 上下滑动");title.setTextSize(14);title.setTextColor(Color.rgb(43,89,148));title.setGravity(Gravity.CENTER);title.setPadding(0,(int)(8*density),0,(int)(8*density));buttons.addView(title);
        for(String[] entry:MENU_ACTIONS){Button button=new Button(this);button.setText(entry[0]);button.setTextSize(15);button.setAllCaps(false);button.setMinHeight((int)(48*density));buttons.addView(button,new LinearLayout.LayoutParams(-1,-2));button.setOnClickListener(v->{String a=entry[1];dismissMenu();if(a.equals("hide"))hide(true);else if(a.equals("ball")||a.equals("plush"))fetch(a);else if(a.equals("stable"))command("wake");else if(!a.equals("close"))command(a);});}
        scroll.addView(buttons);
        int margin=(int)(8*density),width=Math.max(1,Math.min((int)(220*density),bounds.width()-2*margin)),height=Math.max(1,Math.min((int)(420*density),bounds.height()-2*margin));
        android.graphics.drawable.GradientDrawable background=new android.graphics.drawable.GradientDrawable();background.setColor(Color.rgb(255,247,237));background.setCornerRadius(14*density);
        menu=new PopupWindow(scroll,width,height,false);menu.setBackgroundDrawable(background);menu.setElevation(8*density);menu.setOutsideTouchable(true);menu.setOnDismissListener(()->{state.menu=false;state.lastInput=System.currentTimeMillis();persist();});
        int x=(int)PetState.clamp(params.x+params.width,margin,Math.max(margin,bounds.width()-margin-width));
        int y=(int)PetState.clamp(params.y+params.height/2-height/2,margin,Math.max(margin,bounds.height()-margin-height));
        try{menu.showAtLocation(pet,Gravity.TOP|Gravity.LEFT,x,y);}catch(Exception e){state.menu=false;menu=null;}
    }
    private void dismissMenu(){if(menu!=null){menu.dismiss();menu=null;}state.menu=false;}
    @Override public void onConfigurationChanged(android.content.res.Configuration config){super.onConfigurationChanged(config);workspace();persist();}
    @Override public IBinder onBind(Intent intent){return null;}
    @Override public void onDestroy(){
        handler.removeCallbacksAndMessages(null);if(state!=null){dismissMenu();state.cancel();state.stable();persist();}
        try{unregisterReceiver(screen);}catch(Exception ignored){}
        try{if(added)windows.removeView(pet);if(bubbleAdded)windows.removeView(bubble);if(toyAdded)windows.removeView(toy);}catch(Exception ignored){}
        if(assets!=null)assets.close();if(instance==this)instance=null;super.onDestroy();
    }
}
