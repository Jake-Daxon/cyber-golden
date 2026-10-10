using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace CyberGolden;
public static class Native
{
    public static double LastRegionMilliseconds { get; private set; }
    [StructLayout(LayoutKind.Sequential)]public struct PointI {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]public struct RectI {public int Left,Top,Right,Bottom;public int Width=>Right-Left;public int Height=>Bottom-Top;}
    [StructLayout(LayoutKind.Sequential)]private struct LastInput {public uint Size,Tick;}
    [DllImport("user32.dll")]public static extern bool GetCursorPos(out PointI point);
    [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr hwnd,out RectI rect);
    [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")]private static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetWindowLong(IntPtr hwnd,int index,IntPtr value);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateRectRgn(int x1,int y1,int x2,int y2);
    [DllImport("gdi32.dll")]private static extern int CombineRgn(IntPtr dest,IntPtr src1,IntPtr src2,int mode);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")]private static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")]private static extern IntPtr ExtCreateRegion(IntPtr transform,uint bytes,byte[] data);
    [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(PointI point);
    public static double IdleSeconds()
    {
        var input=new LastInput {Size=(uint)Marshal.SizeOf<LastInput>()};
        return GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Tick)/1000.0:0;
    }
    public static void NoActivate(IntPtr hwnd,bool toolWindow=true)
    {
        var style=GetWindowLong(hwnd,-20).ToInt64();
        style=toolWindow?style|0x00000080L:style&~0x00000080L;
        SetWindowLong(hwnd,-20,new IntPtr(style|0x08000000L));
    }
    public static void ClickThrough(IntPtr hwnd)
    {SetWindowLong(hwnd,-20,new IntPtr(GetWindowLong(hwnd,-20).ToInt64()|0x00000020L));}
    public static Forms.Screen ScreenAt(double x,double y,double size=160)=>Forms.Screen.FromRectangle(new System.Drawing.Rectangle((int)x,(int)y,(int)size,(int)size));
    public static double DpiScale(IntPtr hwnd)=>Math.Max(96,GetDpiForWindow(hwnd))/96.0;
    // A silhouette region makes alpha click-through work across processes as well.
    // HTTRANSPARENT alone only forwards within the calling thread on Windows.
    public static void ApplyRegion(IntPtr hwnd,AlphaMask mask,int width,int height,bool flip)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var rectangles=new List<RectI>();
        for(int y=0;y<height;y++)
        {
            int start=-1;
            for(int x=0;x<=width;x++)
            {
                var inside=x<width&&mask.Hit((flip?width-1-x:x)/(double)width,y/(double)height);
                if(inside&&start<0)start=x;
                if(!inside&&start>=0)
                {rectangles.Add(new RectI{Left=start,Top=y,Right=x,Bottom=y+1});start=-1;}
            }
        }
        // One kernel call per frame, rather than hundreds of CombineRgn calls.
        using var memory=new MemoryStream(32+rectangles.Count*16);
        using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
        {
            writer.Write(32);writer.Write(1);writer.Write(rectangles.Count);writer.Write(rectangles.Count*16);
            writer.Write(0);writer.Write(0);writer.Write(width);writer.Write(height);
            foreach(var rect in rectangles){writer.Write(rect.Left);writer.Write(rect.Top);writer.Write(rect.Right);writer.Write(rect.Bottom);}
        }
        var data=memory.ToArray();var region=ExtCreateRegion(IntPtr.Zero,(uint)data.Length,data);
        if(region==IntPtr.Zero)return;
        if(SetWindowRgn(hwnd,region,true)==0)DeleteObject(region); // Windows owns on success.
        LastRegionMilliseconds=watch.Elapsed.TotalMilliseconds;
    }
    public static (uint Modifiers,uint Key)? ParseHotkey(string text)
    {
        uint mods=0x4000,key=0;
        foreach(var raw in text.Split('+'))
        {
            var token=raw.Trim().ToUpperInvariant();
            if(token=="CTRL")mods|=2;else if(token=="ALT")mods|=1;else if(token=="SHIFT")mods|=4;
            else if(token=="WIN")mods|=8;
            else if(token.Length==1&&char.IsAsciiLetterOrDigit(token[0]))key=token[0];
            else if(token.StartsWith("F")&&int.TryParse(token[1..],out var f)&&f>=1&&f<=24)key=(uint)(111+f);
            else return null;
        }
        return key!=0&&(mods&15)!=0?(mods,key):null;
    }
}

public sealed class AlphaMask
{
    private byte[] pixels=Array.Empty<byte>();
    private int width,height;
    public bool Ready=>pixels.Length>0;
    public void Set(BitmapSource frame)
    {
        BitmapSource source=frame.Format==PixelFormats.Bgra32||frame.Format==PixelFormats.Pbgra32?frame:new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
        width=source.PixelWidth;height=source.PixelHeight;
        pixels=new byte[width*height*4];source.CopyPixels(pixels,width*4,0);
    }
    public bool Hit(double x,double y)
    {
        if(x<0||y<0||x>=1||y>=1||!Ready)return false;
        return pixels[((int)(y*height)*width+(int)(x*width))*4+3]>=12;
    }
}
