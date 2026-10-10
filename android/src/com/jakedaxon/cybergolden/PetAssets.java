package com.jakedaxon.cybergolden;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.AudioAttributes;
import android.media.SoundPool;
import org.json.JSONObject;
import java.io.*;
import java.util.*;

final class PetAssets {
    final Map<String,Bitmap[]> frames=new HashMap<>();
    final Map<String,Integer> timing=new HashMap<>();
    final Map<String,Bitmap> poses=new HashMap<>();
    private SoundPool pool;private final Map<String,Integer> sounds=new HashMap<>();
    private final Set<Integer> ready=new HashSet<>();private int stream;
    private long playingUntil;
    private final Map<String,Long> soundMs=new HashMap<>();
    private final Context context;
    private long soundGeneration,pendingGeneration;
    private int pendingId;private String pendingFile="";
    private float pendingVolume;
    PetAssets(Context context){
        this.context=context.getApplicationContext();
        try{
            String json;
            try(InputStream in=context.getAssets().open("pet/manifest.json")){ByteArrayOutputStream b=new ByteArrayOutputStream();byte[] buf=new byte[4096];int n;while((n=in.read(buf))!=-1)b.write(buf,0,n);json=b.toString("UTF-8");}
            JSONObject manifest=new JSONObject(json),actions=manifest.getJSONObject("actions");
            JSONObject lengths=manifest.optJSONObject("audioDurationsMs");if(lengths!=null){Iterator<String> soundKeys=lengths.keys();while(soundKeys.hasNext()){String key=soundKeys.next();soundMs.put(key,lengths.optLong(key,850));}}
            Iterator<String> keys=actions.keys();
            while(keys.hasNext()){
                String key=keys.next();JSONObject action=actions.getJSONObject(key);String dir="pet/"+action.getString("directory");
                ArrayList<Bitmap> bitmaps=new ArrayList<>();String[] names=context.getAssets().list(dir);Arrays.sort(names);
                for(String name:names)if(name.endsWith(".png"))bitmaps.add(load(context,dir+"/"+name));
                frames.put(key,bitmaps.toArray(new Bitmap[0]));timing.put(key,action.optInt("frameMs",180));
            }
            JSONObject ps=manifest.getJSONObject("staticPoses");keys=ps.keys();
            while(keys.hasNext()){String key=keys.next();poses.put(key,load(context,"pet/"+ps.getJSONObject(key).getString("directory")+"/pose.png"));}
        }catch(Exception e){android.util.Log.e("CyberGolden","Animation load failed",e);}
        try{
            pool=new SoundPool.Builder().setMaxStreams(1).setAudioAttributes(new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build()).build();
            pool.setOnLoadCompleteListener((p,id,status)->{if(status==0){ready.add(id);if(id==pendingId&&pendingGeneration==soundGeneration)start(id,pendingFile,pendingVolume);}});
            for(String key:new String[]{"click","eat","drink","happy","breath","hunger-two"}){try(android.content.res.AssetFileDescriptor fd=context.getAssets().openFd("audio/"+key+".wav")){sounds.put(key,pool.load(fd,1));}catch(Exception ignored){}}
        }catch(Exception ignored){}
    }
    private Bitmap load(Context c,String file)throws IOException{
        try(InputStream in=c.getAssets().open(file)){
            BitmapFactory.Options options=new BitmapFactory.Options();options.inSampleSize=2;
            Bitmap b=BitmapFactory.decodeStream(in,null,options);if(b==null)throw new IOException(file);return b;
        }
    }
    Bitmap frame(PetState state,long elapsed){
        if(state.action.equals("idle")||state.action.equals("watchtoy"))return poses.getOrDefault(state.pose,poses.get("FrontSit"));
        String key=state.visualAction();Bitmap[] list=frames.get(key);
        if(list==null||list.length==0)return poses.get("FrontSit");
        int index=(int)((Math.max(0,elapsed)/timing.getOrDefault(key,180))%list.length);
        if(state.manual&&!state.sleeping&&key.equals("jump"))index=Math.min(list.length-1,(int)((Math.max(0,elapsed)%2000)/timing.getOrDefault(key,180)));
        if(state.manual&&!state.sleeping&&(key.equals("sitdown")||key.equals("liedown")||key.equals("companion")))index=Math.min(list.length-1,(int)(Math.max(0,elapsed)/timing.getOrDefault(key,180)));
        if(state.action.equals("drop"))index=list.length-1-index;
        return list[index];
    }
    void play(String key,int volume){
        stop();try{
            String file=key.equals("fetch")?"click":key,cache=key.equals("eat")?file:file+"-gain-"+volume;
            Integer id=sounds.get(cache);
            if(pool==null)return;
            if(id==null){File gained=AudioGain.prepare(context,file,volume/100.0*20);id=pool.load(gained.getAbsolutePath(),1);sounds.put(cache,id);}
            float v=key.equals("eat")?volume/100f:1;
            if(ready.contains(id))start(id,file,v);
            else{pendingId=id;pendingFile=file;pendingVolume=v;pendingGeneration=soundGeneration;}
        }catch(Exception ignored){}
    }
    private void start(int id,String file,float volume){try{pendingId=0;stream=pool.play(id,volume,volume,1,0,1);playingUntil=android.os.SystemClock.uptimeMillis()+soundMs.getOrDefault(file,850L);}catch(Exception ignored){}}
    void stop(){soundGeneration++;pendingId=0;try{if(pool!=null&&stream!=0)pool.stop(stream);}catch(Exception ignored){}stream=0;playingUntil=0;}
    void close(){stop();if(pool!=null)pool.release();for(Bitmap[] list:frames.values())for(Bitmap b:list)b.recycle();for(Bitmap b:poses.values())b.recycle();}
}
