package com.jakedaxon.cybergolden;

import android.content.Context;
import android.graphics.*;
import android.view.*;

final class PetView extends View {
    interface Touch {void down(float x,float y);void move(float x,float y);void up(boolean moved);void menu();}
    private final PetState state;private final PetAssets assets;
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    Touch touch;private float downX,downY;private boolean moved,longPress;
    private long epoch=-1,started,downEpoch;private final Runnable longClick=this::openLongMenu;
    private void openLongMenu(){if(state.epoch!=downEpoch)return;longPress=true;if(touch!=null)touch.menu();}
    PetView(Context c,PetState s,PetAssets a){super(c);state=s;assets=a;setContentDescription("赛博金毛：轻点摸头，拖动改变位置，长按打开互动菜单");setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);setFocusable(true);}
    void changed(){if(epoch!=state.epoch){epoch=state.epoch;started=android.os.SystemClock.uptimeMillis();}invalidate();}
    @Override protected void onDraw(Canvas canvas){
        Bitmap image=assets.frame(state,android.os.SystemClock.uptimeMillis()-started);
        if(image!=null){canvas.save();if(state.faceRight&&state.moving())canvas.scale(-1,1,getWidth()/2f,getHeight()/2f);canvas.drawBitmap(image,null,new RectF(0,0,getWidth(),getHeight()),paint);canvas.restore();}
        if(state.toyCarried){paint.setColor(state.toy.equals("ball")?Color.rgb(152,185,65):Color.rgb(194,136,98));canvas.drawCircle(getWidth()*.24f,getHeight()*.42f,getWidth()*.06f,paint);}
    }
    @Override public boolean performClick(){super.performClick();if(touch!=null)touch.up(false);return true;}
    @Override public boolean onTouchEvent(MotionEvent event){
        if(touch==null)return false;
        switch(event.getActionMasked()){
            case MotionEvent.ACTION_DOWN:
                // Ignore transparent pixels within this compact window. Android still
                // owns the rectangular touch region; do not claim desktop alpha passthrough.
                Bitmap b=assets.frame(state,0);if(b!=null){int px=(int)(event.getX()/getWidth()*b.getWidth()),py=(int)(event.getY()/getHeight()*b.getHeight());if(px>=0&&py>=0&&px<b.getWidth()&&py<b.getHeight()&&Color.alpha(b.getPixel(px,py))<24)return false;}
                downX=event.getRawX();downY=event.getRawY();moved=false;longPress=false;touch.down(downX,downY);downEpoch=state.epoch;postDelayed(longClick,550);return true;
            case MotionEvent.ACTION_MOVE:
                if(Math.hypot(event.getRawX()-downX,event.getRawY()-downY)>8*getResources().getDisplayMetrics().density){moved=true;removeCallbacks(longClick);touch.move(event.getRawX(),event.getRawY());}return true;
            case MotionEvent.ACTION_UP:
                removeCallbacks(longClick);if(!longPress){if(moved)touch.up(true);else performClick();}return true;
            case MotionEvent.ACTION_CANCEL:removeCallbacks(longClick);touch.up(true);return true;
        }return true;
    }
}
