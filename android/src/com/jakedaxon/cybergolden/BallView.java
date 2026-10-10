package com.jakedaxon.cybergolden;

import android.content.Context;
import android.graphics.*;
import android.view.*;

final class BallView extends View {
    interface Touch{void grab(float x,float y);void move(float x,float y);void release();}
    private final PetState state;private final Paint paint=new Paint(3);Touch touch;private boolean holding;
    BallView(Context c,PetState s){super(c);state=s;setContentDescription("网球：按住拾取，滑动松手抛出");}
    @Override protected void onDraw(Canvas c){
        float r=Math.min(getWidth(),getHeight())*.28f,cx=getWidth()/2f,cy=getHeight()/2f;
        paint.setStyle(Paint.Style.FILL);paint.setColor(state.toy.equals("ball")?Color.rgb(188,214,59):Color.rgb(194,136,98));
        c.drawCircle(cx,cy,r,paint);
        if(state.toy.equals("ball")){paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(Math.max(1,r*.12f));paint.setColor(Color.WHITE);RectF oval=new RectF(cx-r*.7f,cy-r,cx+r*.7f,cy+r);c.drawArc(oval,65,230,false,paint);}
    }
    @Override public boolean onTouchEvent(MotionEvent e){if(!state.toy.equals("ball")||touch==null)return false;switch(e.getActionMasked()){
        case MotionEvent.ACTION_DOWN:holding=true;touch.grab(e.getRawX(),e.getRawY());return true;
        case MotionEvent.ACTION_MOVE:if(holding)touch.move(e.getRawX(),e.getRawY());return true;
        case MotionEvent.ACTION_UP:case MotionEvent.ACTION_CANCEL:if(holding){holding=false;touch.release();}return true;
    }return true;}
}
