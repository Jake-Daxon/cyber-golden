package com.jakedaxon.cybergolden;

import android.content.Context;
import java.io.*;

final class AudioGain {
    static int u16(byte[] d,int p){return (d[p]&255)|((d[p+1]&255)<<8);}
    static int i32(byte[] d,int p){return u16(d,p)|(u16(d,p+2)<<16);}
    static File prepare(Context c,String key,double gain)throws IOException{
        File dir=new File(c.getCacheDir(),"audio-gain");dir.mkdirs();
        File out=new File(dir,key+"-"+Math.round(gain*1000)+".wav");if(out.exists())return out;
        ByteArrayOutputStream bytes=new ByteArrayOutputStream();try(InputStream in=c.getAssets().open("audio/"+key+".wav")){byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1)bytes.write(b,0,n);}
        byte[] d=bytes.toByteArray();int format=0,bits=0;
        for(int offset=12;offset+8<=d.length;){
            int length=i32(d,offset+4);if(length<0||length>d.length-offset-8)break;
            String tag=new String(d,offset,4,"US-ASCII");
            if(tag.equals("fmt ")&&length>=16){format=u16(d,offset+8);bits=u16(d,offset+22);}
            if(tag.equals("data")&&format==1&&bits==16){for(int p=offset+8;p+1<offset+8+length;p+=2){
                double sample=(short)u16(d,p)/32768.0*gain,m=Math.abs(sample);
                if(m>.9)sample=Math.signum(sample)*(.9+.09*(1-Math.exp(-(m-.9)/.09)));
                int value=(int)Math.max(-32768,Math.min(32767,Math.round(sample*32767)));
                d[p]=(byte)value;d[p+1]=(byte)(value>>8);
            }}offset+=8+length+(length&1);
        }
        try(OutputStream stream=new FileOutputStream(out)){stream.write(d);}return out;
    }
}
