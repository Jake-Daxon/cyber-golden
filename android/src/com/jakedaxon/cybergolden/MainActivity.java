package com.jakedaxon.cybergolden;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import java.util.UUID;

public final class MainActivity extends Activity {
    private static final int INK=Color.rgb(56,47,40),BLUE=Color.rgb(43,89,148),CREAM=Color.rgb(255,249,240),MUTED=Color.rgb(98,104,110);
    PetState state;Store store;private PetAssets assets;private PetView preview;
    private TextView status,permissionStatus,hunger;private LinearLayout root;
    private final Handler handler=new Handler(Looper.getMainLooper());private boolean pendingStart,resumed;private long lastTick;
    int dp(float n){return (int)(n*getResources().getDisplayMetrics().density+.5f);}
    @Override public void onCreate(Bundle b){super.onCreate(b);getWindow().setStatusBarColor(CREAM);getWindow().setNavigationBarColor(CREAM);getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);store=new Store(this);state=PetService.instance!=null?PetService.instance.state:store.load();assets=new PetAssets(this);build();}
    private TextView text(String value,int size,int color){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(color);t.setPadding(0,dp(6),0,dp(6));return t;}
    private GradientDrawable background(int color){GradientDrawable g=new GradientDrawable();g.setColor(color);g.setCornerRadius(dp(22));return g;}
    private LinearLayout card(){LinearLayout box=new LinearLayout(this);box.setOrientation(LinearLayout.VERTICAL);box.setPadding(dp(20),dp(16),dp(20),dp(16));box.setBackground(background(Color.WHITE));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.bottomMargin=dp(16);root.addView(box,p);return box;}
    private Button button(String label,boolean primary,Runnable run){Button b=new Button(this);b.setText(label);b.setTextSize(15);b.setAllCaps(false);b.setTextColor(primary?Color.WHITE:INK);b.setMinHeight(dp(48));b.setBackground(background(primary?BLUE:Color.rgb(241,245,249)));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,dp(50));p.topMargin=dp(8);b.setLayoutParams(p);b.setOnClickListener(v->run.run());return b;}
    private void heading(LinearLayout parent,String title){TextView t=text(title,20,INK);t.setTypeface(null,Typeface.BOLD);parent.addView(t);}
    private void build(){
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setBackgroundColor(CREAM);
        root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setPadding(dp(20),dp(28),dp(20),dp(28));scroll.addView(root);setContentView(scroll);
        root.setOnApplyWindowInsetsListener((v,i)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets in=i.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());v.setPadding(dp(20)+in.left,dp(20)+in.top,dp(20)+in.right,dp(20)+in.bottom);}return i;});
        TextView tag=text("你的口袋陪伴",13,BLUE);root.addView(tag);TextView title=text("赛博金毛",32,INK);title.setTypeface(null,Typeface.BOLD);root.addView(title);root.addView(text("安静待着，随时回应你。",16,MUTED));
        LinearLayout hero=card();preview=new PetView(this,state,assets);LinearLayout.LayoutParams image=new LinearLayout.LayoutParams(dp(220),dp(220));image.gravity=Gravity.CENTER_HORIZONTAL;hero.addView(preview,image);
        preview.touch=new PetView.Touch(){public void down(float x,float y){}public void move(float x,float y){}public void up(boolean moved){command("pet");}public void menu(){command("shake");}};
        hunger=text("",16,INK);hunger.setGravity(Gravity.CENTER);hero.addView(hunger);status=text("",14,MUTED);status.setGravity(Gravity.CENTER);hero.addView(status);
        hero.addView(button("开启手机桌宠",true,this::enableOverlay));hero.addView(button("隐藏 / 恢复桌宠",false,()->{if(PetService.instance!=null){PetService.instance.hide(!state.hidden);}else{state.setHidden(!state.hidden,System.currentTimeMillis());store.save(state);toast(state.hidden?"下次开启时保持隐藏":"下次开启时显示狗狗");}update();}));
        permissionStatus=text("",13,MUTED);hero.addView(permissionStatus);
        LinearLayout interactions=card();heading(interactions,"陪它玩一会儿");interactions.addView(text("喂食没有间隔限制，吃饱也可以继续喂。",14,MUTED));
        row(interactions,"食盆 · 喂食 +15",()->command("eat"),"喝水",()->command("drink"));
        row(interactions,"摸摸头",()->command("pet"),"握手",()->command("shake"));
        row(interactions,"开心蹦跳",()->command("jump"),"摇尾巴",()->command("wag"));
        row(interactions,"玩网球",()->fetch("ball"),"玩玩偶",()->fetch("plush"));
        row(interactions,"睡觉",()->command("sleep"),"叫醒",()->command("wake"));
        interactions.addView(button("更多动作",false,this::moreActions));
        LinearLayout settings=card();heading(settings,"安静陪伴");settings.addView(text("普通待机不循环动作；互动结束回到所选姿势。",14,MUTED));
        RadioGroup poses=new RadioGroup(this);poses.setOrientation(RadioGroup.HORIZONTAL);String[] ids={"FrontSit","Stand","Lie"},labels={"坐姿","站姿","趴姿"};
        for(int i=0;i<3;i++){RadioButton r=new RadioButton(this);r.setId(200+i);r.setText(labels[i]);r.setTextColor(INK);poses.addView(r,new RadioGroup.LayoutParams(0,dp(48),1));if(state.pose.equals(ids[i]))r.setChecked(true);}poses.setOnCheckedChangeListener((g,id)->{state.pose=ids[id-200];changed();});settings.addView(poses);
        toggle(settings,"允许偶尔短距离漫游",state.autoMove,v->{state.autoMove=v;state.lastInput=System.currentTimeMillis();state.nextRoam=state.lastInput+180000;changed();});
        toggle(settings,"暂停自主行为",state.paused,v->{state.setPaused(v,System.currentTimeMillis());changed();});
        toggle(settings,"播放互动声音",state.sound,v->{state.sound=v;changed();});
        toggle(settings,"允许饥饿轻叫提醒",state.hungerSound,v->{state.hungerSound=v;changed();});
        settings.addView(text("音量",14,INK));SeekBar volume=new SeekBar(this);volume.setMax(100);volume.setProgress(state.volume);volume.setContentDescription("互动声音音量");settings.addView(volume);volume.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onStartTrackingTouch(SeekBar b){}public void onProgressChanged(SeekBar b,int n,boolean user){if(user)state.volume=n;}public void onStopTrackingTouch(SeekBar b){changed();}});
        settings.addView(text("狗狗大小",14,INK));SeekBar size=new SeekBar(this);size.setMax(124);size.setProgress(state.size-96);size.setContentDescription("悬浮狗狗大小");settings.addView(size);size.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onStartTrackingTouch(SeekBar b){}public void onProgressChanged(SeekBar b,int n,boolean user){if(user)state.size=96+n;}public void onStopTrackingTouch(SeekBar b){changed();}});
        settings.addView(button("给狗狗改名字",false,()->{EditText name=new EditText(this);name.setSingleLine();name.setText(state.name);new AlertDialog.Builder(this).setTitle("狗狗名字").setView(name).setPositiveButton("保存",(d,w)->{state.name=name.getText().toString();changed();}).setNegativeButton("取消",null).show();}));
        LinearLayout help=card();heading(help,"第一次使用");help.addView(text("1. 点“开启手机桌宠”。\n2. 允许“显示在其他应用上层”。\n3. 返回本 App，狗狗会出现在屏幕上。\n4. 轻点摸头、拖动移动，长按打开菜单。\n5. 通知栏可隐藏、恢复或关闭桌宠。",15,INK));
        help.addView(text("锁屏时停止动画和声音。部分手机需要在系统电池设置中允许后台运行。关闭 App 页面后，已开启的桌宠可继续陪伴；若系统结束了它，重新打开本 App 并点开启即可。",14,MUTED));
        help.addView(button("关闭桌宠",false,()->{stopService(new Intent(this,PetService.class));toast("桌宠已关闭，存档保留");handler.postDelayed(()->{state=store.load();rebuildPreview();update();},300);}));
        help.addView(button("关于与许可证",false,()->{new AlertDialog.Builder(this).setTitle("赛博金毛 Android 0.1.0").setMessage("完全本地离线，无账号、无广告、无网络权限。\n\n沿用 Windows 版金毛连续帧、声音与行为规则；Android 使用原生显示和状态适配，不运行 WPF/VPet DLL。\n\nVPet：LorisYounger，Apache-2.0。\n犬声：Amada44，CC BY-SA 3.0，已剪辑。\n进食、饮水及呼吸拟音：CC0。\n完整许可证已内置于 assets/licenses。\n\nGitHub：Jake-Daxon/cyber-golden").setPositiveButton("知道了",null).show();}));
        root.addView(text("ANDROID 0.1.0 · 离线陪伴",12,MUTED));update();
    }
    private void row(LinearLayout box,String a,Runnable ar,String b,Runnable br){LinearLayout row=new LinearLayout(this);Button first=button(a,false,ar),second=button(b,false,br);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(0,dp(52),1);p.setMargins(0,dp(8),dp(5),0);row.addView(first,p);LinearLayout.LayoutParams q=new LinearLayout.LayoutParams(0,dp(52),1);q.setMargins(dp(5),dp(8),0,0);row.addView(second,q);box.addView(row);}
    interface Bool {void apply(boolean value);}
    private void toggle(LinearLayout box,String label,boolean value,Bool callback){Switch sw=new Switch(this);sw.setText(label);sw.setTextColor(INK);sw.setTextSize(14);sw.setMinHeight(dp(52));sw.setChecked(value);box.addView(sw);sw.setOnCheckedChangeListener((b,v)->callback.apply(v));}
    private void toast(String s){Toast.makeText(this,s,Toast.LENGTH_SHORT).show();}
    private void command(String action){
        if(state.hidden){toast("请先恢复狗狗，再互动");return;}
        if(PetService.instance!=null)PetService.instance.command(action);
        else {assets.stop();state.interact(action,UUID.randomUUID().toString(),System.currentTimeMillis());store.save(state);preview.changed();}update();
    }
    private void fetch(String kind){if(state.hidden){toast("请先恢复狗狗");return;}if(PetService.instance!=null)PetService.instance.fetch(kind);else{command("pickup");toast("开启悬浮桌宠后可以追逐、叼回玩具");}}
    private void changed(){state.normalize();if(PetService.instance!=null)PetService.instance.refreshSettings();else{state.cancel();state.stable();assets.stop();store.save(state);preview.changed();}update();}
    private void moreActions(){String[] labels={"歪头","舔鼻子","打哈欠","伸懒腰","期待撒娇","坐下","趴下"},actions={"tilt","lick","yawn","stretch","expect","sitdown","liedown"};new AlertDialog.Builder(this).setTitle("互动动作").setItems(labels,(d,i)->command(actions[i])).setNegativeButton("关闭",null).show();}
    private void enableOverlay(){
        if(!Settings.canDrawOverlays(this)){pendingStart=true;try{startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));}catch(Exception e){pendingStart=false;toast("请在系统设置中搜索“悬浮窗”，允许赛博金毛");}return;}
        store.save(state);startDesktop();
    }
    private void startDesktop(){
        if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},44);
        if(PetService.instance==null){try{startForegroundService(new Intent(this,PetService.class));handler.postDelayed(()->{if(PetService.instance!=null){state=PetService.instance.state;rebuildPreview();update();}},300);}catch(Exception e){toast("未能开启桌宠，请保持 App 在前台后重试");}}
        else {PetService.instance.hide(false);state=PetService.instance.state;rebuildPreview();}
    }
    private void rebuildPreview(){if(preview==null)return;LinearLayout parent=(LinearLayout)preview.getParent();int index=parent.indexOfChild(preview);android.view.ViewGroup.LayoutParams layout=preview.getLayoutParams();parent.removeView(preview);preview=new PetView(this,state,assets);preview.touch=new PetView.Touch(){public void down(float x,float y){}public void move(float x,float y){}public void up(boolean moved){command("pet");}public void menu(){command("shake");}};parent.addView(preview,index,layout);}
    private void update(){if(status==null)return;status.setText(String.format(java.util.Locale.CHINA,"饱食度 %.0f / 100   ·   精力 %.0f",state.food,state.energy));hunger.setText(state.hungry?"我饿了":state.sleeping?"正在睡觉":state.name+"正在陪着你");permissionStatus.setText((Settings.canDrawOverlays(this)?"悬浮窗权限已开启":"尚未开启悬浮窗权限")+(PetService.instance!=null?" · 桌宠运行中":" · 可先在 App 内互动"));}
    private final Runnable ticker=new Runnable(){public void run(){if(!resumed)return;long now=SystemClock.elapsedRealtime();if(PetService.instance==null){boolean paused=state.paused;state.paused=true;state.tick(System.currentTimeMillis(),(now-lastTick)/1000.0,0,0);state.paused=paused;if(state.pendingSound!=null){assets.play(state.pendingSound,state.volume);state.pendingSound=null;}}lastTick=now;preview.changed();update();handler.postDelayed(this,state.action.equals("idle")?1000:50);}};
    @Override protected void onResume(){super.onResume();resumed=true;if(PetService.instance!=null){state=PetService.instance.state;rebuildPreview();}if(pendingStart){pendingStart=false;if(Settings.canDrawOverlays(this))startDesktop();else toast("未允许悬浮窗，仍可在 App 内互动");}lastTick=SystemClock.elapsedRealtime();handler.post(ticker);}
    @Override protected void onPause(){resumed=false;handler.removeCallbacks(ticker);assets.stop();if(PetService.instance==null)store.save(state);super.onPause();}
    @Override protected void onDestroy(){handler.removeCallbacksAndMessages(null);if(assets!=null)assets.close();super.onDestroy();}
}
