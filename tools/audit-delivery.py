from pathlib import Path
import json,hashlib,zipfile,wave
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];publish=ROOT/'dist/CyberGolden-0.3.0-win-x64'
manifest=json.loads((publish/'assets/pet/manifest.json').read_text('utf-8'))
all_frames=list((publish/'assets/pet').rglob('*.png'))
frames=[p for p in all_frames if 'static-poses' not in p.parts]
checks={}
checks['twenty_actions']=len(manifest['actions'])==20
checks['120_RGBA_frames']=len(frames)==120
checks['three_non_looping_static_poses']=len(manifest['staticPoses'])==3 and all(not p['loop'] for p in manifest['staticPoses'].values())
checks['all_frames_512_and_transparent']=all((lambda im:im.mode=='RGBA' and im.size==(512,512) and im.getchannel('A').getextrema()[0]==0)(Image.open(p)) for p in frames)
checks['transparent_canvas_margins']=all((lambda im:im.getchannel('A').getbbox()[0]>0 and im.getchannel('A').getbbox()[1]>0 and im.getchannel('A').getbbox()[2]<512 and im.getchannel('A').getbbox()[3]<512)(Image.open(p)) for p in frames)
checks['120_distinct_animation_frame_files']=len({hashlib.sha256(p.read_bytes()).hexdigest() for p in frames})==120
checks['five_valid_local_PCM_audio']=all((lambda w:w.getnchannels()==1 and w.getsampwidth()==2 and w.getnframes()>0)(wave.open(str(publish/'assets/audio'/f'{n}.wav'))) for n in ['click','happy','eat','drink','breath'])
files=[str(p.relative_to(publish)).replace('\\','/') for p in publish.rglob('*') if p.is_file()]
checks['no_photos_reference_atlases_save_or_steam']=not any(any(token in f.lower() for token in ['puppy-photo','golden-photo','-atlas','state.json','steam','workshop','credential','api_key']) for f in files)
checks['no_developer_debug_symbols']=not any(f.endswith('.pdb') for f in files)
checks['runtime_and_native_libraries']=all((publish/name).exists() for name in ['CyberGolden.exe','coreclr.dll','hostfxr.dll','libSkiaSharp.dll','PresentationFramework.dll'])
checks['apphost_MZ']= (publish/'CyberGolden.exe').read_bytes()[:2]==b'MZ'
checks['installer_MZ']=(ROOT/'dist/CyberGolden-Setup-0.3.0-x64.exe').read_bytes()[:2]==b'MZ'
archive=ROOT/'dist/CyberGolden-Portable-0.3.0-x64.zip'
with zipfile.ZipFile(archive) as z:
    checks['portable_contains_same_entity_files']=set(z.namelist()) >= set(files)
    checks['portable_entity_hashes_match_publish']=all(hashlib.sha256(z.read(f)).digest()==hashlib.sha256((publish/f).read_bytes()).digest() for f in files)
result={'checks':checks,'passed':all(checks.values()),'pngs':len(frames),'publication_files':len(files),'total_uncompressed_bytes':sum((publish/f).stat().st_size for f in files)}
(ROOT/'verification/delivery-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
if not result['passed']:raise SystemExit(1)
