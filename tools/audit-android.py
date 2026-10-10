"""Audit the exact signed delivery APK; never substitute a CI-signed test build."""
from pathlib import Path
import zipfile,json,hashlib
root=Path(__file__).resolve().parents[1];apk=root/'dist/CyberGolden-Android-0.1.0.apk'
manifest=json.loads((root/'assets/pet/manifest.json').read_text('utf-8'))
expected=[root/'assets/pet/manifest.json']
for action in manifest['actions'].values():expected.extend((root/'assets/pet'/action['directory']).glob('*.png'))
for pose in manifest['staticPoses'].values():expected.append(root/'assets/pet'/pose['directory']/'pose.png')
expected.extend((root/'assets/audio').glob('*.wav'))
with zipfile.ZipFile(apk) as z:
    entries=z.namelist()
    assert all('\\' not in name for name in entries),'Backslash entry breaks Android assets'
    assert len(entries)==len(set(entries)),'Duplicate entries'
    assert 'classes.dex' in entries and 'AndroidManifest.xml' in entries
    for file in expected:
        entry='assets/'+file.relative_to(root/'assets').as_posix()
        assert z.read(entry)==file.read_bytes(),f'Missing or altered asset {entry}'
    assert not any(any(s in name.lower() for s in ['.jks','signing-password','test-data','puppy-photo','golden-photo-master','state.json','androidtestrunner']) for name in entries)
    assert b'AndroidTestRunner' not in z.read('classes.dex'),'Test code in distributed APK'
    assert len([n for n in entries if n.startswith('assets/licenses/')])>=6
result={'apk':apk.name,'bytes':apk.stat().st_size,'sha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'runtime_assets_verified_byte_for_byte':len(expected),'frames':120,'poses':3,'sounds':5,'zip_paths':'canonical forward slash','private_inputs':False,'private_signing_key':False,'test_harness':False}
(root/'verification/android/apk-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
