using System;
using System.IO;
using System.Text;

namespace CyberGolden;
// PCM16 gain before playback preserves a real 20x requested gain even when the
// platform volume control itself only accepts 0..1. A soft knee bounds peaks.
internal static class AudioGain
{
    public static string Prepare(string source,string directory,double gain)
    {
        Directory.CreateDirectory(directory);
        var destination=Path.Combine(directory,Path.GetFileNameWithoutExtension(source)+"-"+Math.Round(gain*1000)+".wav");
        if(File.Exists(destination))return destination;
        var data=File.ReadAllBytes(source);int bits=0,format=0;
        for(int offset=12;offset+8<=data.Length;)
        {
            var length=BitConverter.ToInt32(data,offset+4);if(length<0||length>data.Length-offset-8)break;
            var tag=Encoding.ASCII.GetString(data,offset,4);
            if(tag=="fmt "&&length>=16){format=BitConverter.ToInt16(data,offset+8);bits=BitConverter.ToInt16(data,offset+22);}
            if(tag=="data"&&format==1&&bits==16)
            {
                for(int p=offset+8;p+1<offset+8+length;p+=2)
                {
                    var sample=BitConverter.ToInt16(data,p)/32768.0*gain;
                    var magnitude=Math.Abs(sample);
                    if(magnitude>.9)sample=Math.Sign(sample)*(.9+.09*(1-Math.Exp(-(magnitude-.9)/.09)));
                    var value=(short)Math.Clamp(Math.Round(sample*32767),short.MinValue,short.MaxValue);
                    data[p]=(byte)(value&255);data[p+1]=(byte)((value>>8)&255);
                }
            }
            offset+=8+length+(length&1);
        }
        File.WriteAllBytes(destination,data);return destination;
    }
}
