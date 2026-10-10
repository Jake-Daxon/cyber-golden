from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
ROOT=Path(__file__).resolve().parents[1];manifest=json.loads((ROOT/'assets/pet/manifest.json').read_text('utf-8'))
try:font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
except OSError:font=ImageFont.load_default()
names={'idle':'自然站立 / 眨眼','expect':'期待 / 坐姿','companion':'趴下陪伴','sleep':'睡眠呼吸','walk':'走路','trot':'小跑','shake':'握手','eat':'吃饭','liedown':'坐趴转换','wag':'开心摇尾'}
def sequence(name):
    info=manifest['actions'][name]
    return [Image.open(p).convert('RGBA') for p in sorted((ROOT/'assets/pet'/info['directory']).glob('*.png'))]
sheet=Image.new('RGB',(640,460),(242,246,249));d=ImageDraw.Draw(sheet)
for i,(name,label) in enumerate([('FrontSit','正面静坐（默认）'),('Stand','四足静站'),('Lie','安静趴下'),('sleep','明确睡眠')]):
    picture=sequence(name)[0] if name=='sleep' else Image.open(ROOT/'assets/pet'/manifest['staticPoses'][name]['directory']/'pose.png').convert('RGBA')
    frame=picture.resize((160,160),Image.Resampling.LANCZOS)
    for j,color in enumerate([(242,246,249,255),(32,44,59,255)]):
        tile=Image.new('RGBA',(160,160),color);tile.alpha_composite(frame);sheet.paste(tile.convert('RGB'),(i*160,40+j*200))
    d.text((i*160+8,10),label,fill=(39,57,75),font=font)
sheet.save(ROOT/'previews/character-set-light-dark.png')
tour=[];durations=[]
for name in ['idle','walk','wag','shake','eat','liedown','sleep','companion']:
    for frame in sequence(name):
        tile=Image.new('RGBA',(320,360),(239,245,249,255));tile.alpha_composite(frame.resize((320,320),Image.Resampling.LANCZOS),(0,36))
        ImageDraw.Draw(tile).text((16,10),names[name],fill=(39,57,75),font=font)
        tour.append(tile.convert('RGB'));durations.append(manifest['actions'][name]['frameMs'])
tour[0].save(ROOT/'previews/state-tour.gif',save_all=True,append_images=tour[1:],duration=durations)
pose_names={'FrontSit':'正面静坐 · 双爪落地（默认）','Stand':'自然站立','Lie':'安静趴下'}
for pose,info in manifest['staticPoses'].items():
    Image.open(ROOT/'assets/pet'/info['directory']/'pose.png').resize((160,160),Image.Resampling.LANCZOS).save(ROOT/'previews'/f'pose-{pose}-160.png')
index=['<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>赛博金毛静止姿势与动作预览</title><style>body{font-family:system-ui;background:#eff5f9;color:#27394b;margin:32px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:20px}figure{margin:0;background:white;border-radius:14px;padding:16px}img{width:160px;height:160px;object-fit:contain}h1{font-size:24px}p{max-width:700px;line-height:1.7}button{padding:8px 16px;margin-top:12px}</style><h1>赛博金毛 · 默认正面静坐</h1><p>双爪落地，普通待机不循环。保留 CD 漫游；主动互动后回到保存的姿势，饥饿提醒是明确例外。下面的卡片保持静止，点击只播放一次。</p><main>']
for pose,label in pose_names.items():index.append(f'<figure><img src="pose-{pose}-160.png" alt="{label}"><figcaption>{label}</figcaption></figure>')
index.append('</main><h2>保留的主动动作</h2><main>')
for name,info in manifest['actions'].items():
    sequence(name)[0].resize((160,160),Image.Resampling.LANCZOS).save(ROOT/'previews'/f'{name}-poster.png')
    index.append(f'<figure><img id="{name}" src="{name}-poster.png" alt="{names.get(name,name)}"><figcaption>{names.get(name,name)}</figcaption><button onclick="play(\'{name}\',{info["frameMs"]*info["frames"]})">播放一次</button></figure>')
index.append('</main><script>const timers={};function play(n,t){clearTimeout(timers[n]);const im=document.getElementById(n);im.src=n+".gif?v="+Date.now();timers[n]=setTimeout(()=>im.src=n+"-poster.png",t+60)}</script></html>');(ROOT/'previews/动作预览.html').write_text('\n'.join(index),'utf-8')
print('Character sheet, eight-state tour and 20 animated action previews prepared')
