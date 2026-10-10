package com.jakedaxon.cybergolden;

import android.content.Context;
import android.util.AtomicFile;
import org.json.JSONObject;
import java.io.*;

final class Store {
    private final AtomicFile file;
    Store(Context c){file=new AtomicFile(new File(c.getFilesDir(),"pet-save.json"));}
    PetState load(){
        PetState s=new PetState();
        try(InputStream in=file.openRead()){
            ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] b=new byte[4096];int n;
            while((n=in.read(b))!=-1)bytes.write(b,0,n);
            JSONObject j=new JSONObject(bytes.toString("UTF-8"));
            s.name=j.optString("name",s.name);s.pose=j.optString("pose",s.pose);s.lastFeed=j.optString("lastFeed","");
            s.food=j.optDouble("food",80);s.water=j.optDouble("water",80);s.energy=j.optDouble("energy",80);s.mood=j.optDouble("mood",80);s.affection=j.optDouble("affection",0);
            s.hungry=j.optBoolean("hungry");s.centerAttempted=j.optBoolean("centerAttempted");s.hidden=j.optBoolean("hidden");s.paused=j.optBoolean("paused");s.sleeping=j.optBoolean("sleeping");
            s.sound=j.optBoolean("sound",true);s.hungerSound=j.optBoolean("hungerSound",true);s.autoMove=j.optBoolean("autoMove",true);s.staticIdle=j.optBoolean("staticIdle",true);
            s.volume=j.optInt("volume",20);s.size=j.optInt("size",144);s.x=(float)j.optDouble("x",-1);s.y=(float)j.optDouble("y",-1);
            s.savedAt=j.optLong("savedAt");s.nextHungerSound=j.optLong("nextHungerSound");s.lastSound=j.optLong("lastSound");
        }catch(Exception ignored){/* First run or damaged save: safe defaults. */}
        s.restore(System.currentTimeMillis());return s;
    }
    synchronized boolean save(PetState s){
        FileOutputStream out=null;
        try{
            JSONObject j=new JSONObject();
            j.put("version",1).put("name",s.name).put("pose",s.pose).put("lastFeed",s.lastFeed);
            j.put("food",s.food).put("water",s.water).put("energy",s.energy).put("mood",s.mood).put("affection",s.affection);
            j.put("hungry",s.hungry).put("centerAttempted",s.centerAttempted).put("hidden",s.hidden).put("paused",s.paused).put("sleeping",s.sleeping);
            j.put("sound",s.sound).put("hungerSound",s.hungerSound).put("autoMove",s.autoMove).put("staticIdle",s.staticIdle);
            j.put("volume",s.volume).put("size",s.size).put("x",s.x).put("y",s.y).put("savedAt",System.currentTimeMillis());
            j.put("nextHungerSound",s.nextHungerSound).put("lastSound",s.lastSound);
            out=file.startWrite();out.write(j.toString().getBytes("UTF-8"));file.finishWrite(out);return true;
        }catch(Exception e){if(out!=null)file.failWrite(out);return false;}
    }
}
