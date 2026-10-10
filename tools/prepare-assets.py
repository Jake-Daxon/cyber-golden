"""Deterministic atlas extraction/anchor normalization; artwork made with built-in imagegen.
Does not synthesize a gait from static photos or swap reference angles.
"""
from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'assets/pet'
OUT.mkdir(parents=True,exist_ok=True)
SPEC={
 'motion-atlas.png':[('walk',83,'Move'),('trot',67,'Move'),('sitdown',140,'Idel'),('liedown',180,'Sleep')],
 'expression-atlas.png':[('tilt',150,'Idel'),('lick',120,'Idel'),('yawn',200,'Idel'),('shake',160,'Touch_Body')],
 'interaction-atlas.png':[('eat',180,'Common'),('drink',180,'Common'),('jump',100,'Idel'),('stretch',180,'Idel')],
 'idle-atlas.png':[('idle',600,'Default'),('pet',180,'Touch_Head'),('sleep',700,'Sleep'),('wag',100,'Idel')],
 'toy-atlas.png':[('pickup',160,'Common'),('carry',83,'Move'),('expect',300,'Idel'),('companion',800,'StateONE')]
}
manifest={"canvas":[512,512],"baseline":472,"source":"built-in imagegen; photos highest identity priority", "inferred":["side/back anatomy","tail","all movement"],"actions":{}}
frames={}
for filename, rows in SPEC.items():
    atlas=Image.open(ROOT/'assets'/filename).convert('RGBA')
    data=np.array(atlas)
    # Ignore tiny alpha generation noise; connected components retain actual furry edges.
    cells={}
    rowcuts=[0]
    yprojection=(data[:,:,3]>80).sum(axis=1)
    for row in range(1,4):
        center=row*256
        choices=np.arange(center-36,center+48)
        rowcuts.append(int(choices[np.argmin(yprojection[choices]+np.abs(choices-center)*.05)]))
    rowcuts.append(1024)
    # Generated atlas spacing is not an exact grid: locate gutters by alpha projection.
    # Some tail tips touch the following frame. Split at the thinnest local seam.
    for row in range(4):
        top=rowcuts[row];bot=rowcuts[row+1]
        projection=(data[top:bot,:,3]>80).sum(axis=0)
        cuts=[0]
        for col in range(1,6):
            center=col*256
            choices=np.arange(max(cuts[-1]+160,center-48),min(1535,center+60))
            score=projection[choices]+np.abs(choices-center)*.035
            cuts.append(int(choices[np.argmin(score)]))
        cuts.append(1536)
        for col in range(6):
            slab=data[top:bot,cuts[col]:cuts[col+1],:]
            labels,n=ndimage.label(slab[:,:,3]>24)
            objs=ndimage.find_objects(labels)
            candidates=[]
            for idx,sl in enumerate(objs,1):
                if sl is None:continue
                area=int((labels[sl]==idx).sum())
                if area<1800:continue
                y,x=sl
                candidates.append((idx,(slice(y.start+top,y.stop+top),slice(x.start+cuts[col],x.stop+cuts[col])),area))
            cells[(row,col)]=(candidates,(top,bot,cuts[col],cuts[col+1]),labels)
    for row,(name,ms,gtype) in enumerate(rows):
        directory=OUT/'nomal'/gtype.lower()/name
        directory.mkdir(parents=True,exist_ok=True)
        sequence=[]
        sizes=[]
        for col in range(6):
            candidates,bounds,celllabels=cells[(row,col)]
            if not candidates:raise RuntimeError(f'Missing puppy {filename} {row}:{col}')
            primary=max(candidates,key=lambda c:c[2])
            i,(ys,xs),_=primary
            x0=max(0,xs.start-2); x1=min(atlas.width,xs.stop+2)
            y0=max(0,ys.start-2); y1=min(atlas.height,ys.stop+2)
            # Include detached bowl in the same cell for feeding rows.
            if name in ('eat','drink'):
                top,bot,left,right=bounds
                for ci,(cy,cx),ca in candidates:
                    if ca>1800:
                        x0=min(x0,cx.start);x1=max(x1,cx.stop);y0=min(y0,cy.start);y1=max(y1,cy.stop)
            crop=atlas.crop((x0,y0,x1,y1))
            a=np.array(crop)
            top,bot,left,right=bounds
            keep=celllabels==i
            if name in ('eat','drink'):
                for ci,_,ca in candidates:
                    if ca>1800:keep|=celllabels==ci
            keep=ndimage.binary_dilation(keep,iterations=2)
            globalkeep=np.zeros(data.shape[:2],dtype=bool)
            globalkeep[top:bot,left:right]=keep
            a[~globalkeep[y0:y1,x0:x1]]=0
            # Alpha threshold is a technical noise cleanup, not repainting fur.
            a[a[:,:,3]<6]=0
            crop=Image.fromarray(a)
            box=crop.getchannel('A').point(lambda p:255 if p>24 else 0).getbbox()
            crop=crop.crop(box)
            sequence.append((crop,y0+box[3])); sizes.append(crop.size)
        # One constant scale for the complete action, no independent frame resizing.
        scale=min(430/max(w for w,h in sizes),440/max(h for w,h in sizes),1.85)
        if name in ('sleep','companion'):scale=1.72
        maxbottom=max(v for _,v in sequence)
        result=[]
        for col,(crop,bottom) in enumerate(sequence):
            w,h=round(crop.width*scale),round(crop.height*scale)
            layer=crop.resize((w,h),Image.Resampling.LANCZOS)
            canvas=Image.new('RGBA',(512,512))
            # Ground contact anchors; preserve real airborne phase in jump/trot.
            airborne=round((maxbottom-bottom)*scale) if name in ('jump','trot','carry') else 0
            canvas.alpha_composite(layer,((512-w)//2,472-h-airborne))
            path=directory/f'{col:03d}_{ms}.png'
            canvas.save(path)
            result.append(canvas)
        # Store per-frame mouth anchors inferred from dark face features, including pickup.
        mouths=[]
        for frame in result:
            rgba=np.array(frame); dark=(rgba[:,:,0]<90)&(rgba[:,:,1]<80)&(rgba[:,:,3]>180)
            dark[330:]=False
            lab,count=ndimage.label(dark)
            candidates=[]
            for idx,sl in enumerate(ndimage.find_objects(lab),1):
                if sl is None:continue
                yy,xx=sl;area=(lab[sl]==idx).sum()
                if area>35 and xx.start<330:candidates.append((area,((xx.start+xx.stop)/1024,(yy.start+yy.stop)/1024+.025)))
            mouths.append(max(candidates,key=lambda c:c[0])[1] if candidates else (.25,.42))
        if name=='pickup':
            # Visually checked jaw anchors: lowering puts the nose below the generic face ROI.
            mouths=[(.148,.445),(.26,.895),(.27,.895),(.27,.895),(.187,.62),(.165,.411)]
        frames[name]=result
        # Explicit metadata matches the actual PetLoader/GraphInfo rules.
        (directory/'info.lps').write_text(f'pnganimation#{name}:|graph#{gtype}:|mode#Nomal:|animat#Single:|loop#false:|\n',encoding='utf-8')
        manifest['actions'][name]={"directory":str(directory.relative_to(OUT)).replace('\\','/'),"frames":6,"frameMs":ms,"graph":gtype,"mouth":mouths,"quality":"generated six-keyframe sequence; visual review required"}
        # Preview composites handle alpha correctly even when the image viewer shows RGB.
        previews=[]
        for frame in result:
            bg=Image.new('RGBA',(256,256),(236,242,247,255))
            bg.alpha_composite(frame.resize((256,256),Image.Resampling.LANCZOS))
            previews.append(bg.convert('RGB'))
        previews[0].save(ROOT/'previews'/f'{name}.gif',save_all=True,append_images=previews[1:],duration=ms)
        strip=Image.new('RGB',(960,160),(236,242,247))
        for idx,frame in enumerate(result):
            tile=Image.new('RGBA',(160,160),(236,242,247,255));tile.alpha_composite(frame.resize((160,160),Image.Resampling.LANCZOS))
            strip.paste(tile.convert('RGB'),(idx*160,0))
        strip.save(ROOT/'previews'/f'{name}-160.png')
# Explicit aliases/fallbacks are honest reuse, not independent newly drawn animations.
manifest['fallbacks']={"drag":"idle","welcome":"wag","drop":"pickup (reverse)","sit":"expect","rest":"companion","happy":"jump","shutdown":"companion","say":"expect","work":"idle","switch":"idle"}
manifest['staticPoses']={}
for pose,source,index in [('FrontSit','shake',0),('Stand','idle',0),('Lie','companion',0)]:
    directory=OUT/'nomal'/'static-poses'/pose.lower()
    directory.mkdir(parents=True,exist_ok=True)
    frames[source][index].save(directory/'pose.png')
    (directory/'info.lps').write_text(f'picture#pose-{pose}:|path#pose.png:|graph#Default:|mode#Nomal:|animat#Single:|length#1:|loop#false:|\n',encoding='utf-8')
    manifest['staticPoses'][pose]={'directory':str(directory.relative_to(OUT)).replace('\\','/'),'sourceAction':source,'sourceFrame':index,'loop':False}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'golden.lps').write_text('pet#golden:|intor#照片版赛博金毛:|path#nomal:|petname#赛博金毛:|\ntouchhead:|px#55:|py#25:|sw#320:|sh#205:|\ntouchbody:|px#40:|py#230:|sw#440:|sh#240:|\n',encoding='utf-8')
thumb=frames['shake'][0]
thumb.save(ROOT/'assets/pet.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
# Contact sheet at real desktop default size.
sheet=Image.new('RGB',(800,640),(238,243,247))
d=ImageDraw.Draw(sheet)
for idx,(name,seq) in enumerate(frames.items()):
    tile=Image.new('RGBA',(160,160),(238,243,247,255)); tile.alpha_composite(seq[2].resize((160,160),Image.Resampling.LANCZOS))
    x=(idx%5)*160;y=(idx//5)*160;sheet.paste(tile.convert('RGB'),(x,y));d.text((x+8,y+6),name,fill=(38,52,68))
sheet.save(ROOT/'previews/contact-sheet-160.png')
print(f'Prepared {sum(len(v) for v in frames.values())} RGBA frames / {len(frames)} actions')
