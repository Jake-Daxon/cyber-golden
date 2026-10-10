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
    PetAssets(Context context){
        try{
            String json;
            try(InputStream in=context.getAssets().open("pet/manifest.json")){ByteArrayOutputStream b=new ByteArrayOutputStream();byte[] buf=new byte[4096];int n;while((n=in.read(buf))!=-1)b.write(buf,0,n);json=b.toString("UTF-8");}
            JSONObject manifest=new JSONObject(json),actions=manifest.getJSONObject("actions");
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
            pool.setOnLoadCompleteListener((p,id,status)->{if(status==0)ready.add(id);});
            for(String key:new String[]{"click","eat","drink","happy","breath"}){try(android.content.res.AssetFileDescriptor fd=context.getAssets().openFd("audio/"+key+".wav")){sounds.put(key,pool.load(fd,1));}catch(Exception ignored){}}
        }catch(Exception ignored){}
    }
    private Bitmap load(Context c,String file)throws IOException{
        try(InputStream in=c.getAssets().open(file)){
            BitmapFactory.Options options=new BitmapFactory.Options();options.inSampleSize=2;
            Bitmap b=BitmapFactory.decodeStream(in,null,options);if(b==null)throw new IOException(file);return b;
        }
    }
    Bitmap frame(PetState state,long elapsed){
        if(state.action.equals("idle"))return poses.getOrDefault(state.pose,poses.get("FrontSit"));
        String key=state.visualAction();Bitmap[] list=frames.get(key);
        if(list==null||list.length==0)return poses.get("FrontSit");
        int index=(int)((Math.max(0,elapsed)/timing.getOrDefault(key,180))%list.length);
        if(state.action.equals("drop"))index=list.length-1-index;
        return list[index];
    }
    void play(String key,int volume){stop();try{Integer id=sounds.get(key);if(pool!=null&&id!=null&&ready.contains(id)){float v=volume/100f;stream=pool.play(id,v,v,1,0,1);}}catch(Exception ignored){}}
    void stop(){try{if(pool!=null&&stream!=0)pool.stop(stream);}catch(Exception ignored){}stream=0;}
    void close(){stop();if(pool!=null)pool.release();for(Bitmap[] list:frames.values())for(Bitmap b:list)b.recycle();for(Bitmap b:poses.values())b.recycle();}
}
