"""Standalone Android source kit, with derived assets and no private signing data."""
from pathlib import Path
import zipfile
root=Path(__file__).resolve().parents[1]
files=[]
for p in (root/'android').rglob('*'):
    rel=p.relative_to(root).as_posix()
    if p.is_file() and not rel.startswith(('android/build/','android/assets/','android/res/')):files.append(p)
for folder in ['assets/pet','assets/audio']:
    files.extend(p for p in (root/folder).rglob('*') if p.is_file() and p.suffix in ('.json','.png','.wav'))
for name in ['LICENSE.txt','docs/安卓安装教程.md','docs/安卓验证报告.md','docs/NOTICE.txt','docs/LICENSE-VPet.txt','docs/LICENSE-Audio-CC-BY-SA-3.0.txt','docs/LICENSE-Foley-CC0.txt','docs/audio-manifest.json','tools/bootstrap-android.ps1','tools/build-android.ps1','tools/build-android-tests.ps1','tools/build-android-linux.sh','tools/prepare-android.py','tools/finalize-android-zip.py','tools/audit-android.py','tools/make-android-source.py']:
    files.append(root/name)
destination=root/'dist/CyberGolden-Android-Source-0.2.1-preview.zip'
with zipfile.ZipFile(destination,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(set(files)):
        assert all(part not in ('.android-private','test-data','golden-photo-assets','cyber-golden-kit-v2','build') for part in p.relative_to(root).parts)
        z.write(p,'CyberGolden-Android-Source-0.2.1-preview/'+p.relative_to(root).as_posix())
print(f'Android source kit: {len(set(files))} files, {destination.stat().st_size} bytes')
