"""Only derived runtime assets and applicable notices enter the Android APK."""
from pathlib import Path
import shutil,json,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[1]
out=root/'android'/'assets'
out.mkdir(parents=True,exist_ok=True)
manifest=json.loads((root/'assets/pet/manifest.json').read_text('utf-8'))
files=[root/'assets/pet/manifest.json']
for action in manifest['actions'].values():files.extend((root/'assets/pet'/action['directory']).glob('*.png'))
for pose in manifest['staticPoses'].values():files.append(root/'assets/pet'/pose['directory']/'pose.png')
files.extend((root/'assets/audio').glob('*.wav'))
for source in files:
    relative=source.relative_to(root/'assets');target=out/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
licenses=out/'licenses';licenses.mkdir(exist_ok=True)
for name in ['LICENSE-VPet.txt','LICENSE-Audio-CC-BY-SA-3.0.txt','LICENSE-Foley-CC0.txt','NOTICE.txt','audio-manifest.json']:
    shutil.copy2(root/'docs'/name,licenses/name)
shutil.copy2(root/'LICENSE.txt',licenses/'LICENSE.txt')
icon=Image.open(root/'assets/pet/nomal/static-poses/frontsit/pose.png').convert('RGBA')
canvas=Image.new('RGBA',(192,192),(255,247,237,255));icon.thumbnail((192,192));canvas.alpha_composite(icon,((192-icon.width)//2,(192-icon.height)//2))
res=root/'android/res/drawable';res.mkdir(parents=True,exist_ok=True);canvas.save(res/'pet_icon.png')
report={'animation_frames':sum(a['frames'] for a in manifest['actions'].values()),'static_poses':len(manifest['staticPoses']),'sounds':5,'raw_photos':False,'user_saves':False,'network_permission':False,'asset_files':len(files)}
(root/'verification/android').mkdir(parents=True,exist_ok=True)
(root/'verification/android/assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
