from pathlib import Path
import zipfile,hashlib
ROOT=Path(__file__).resolve().parents[1]
paths=[]
for folder in ['src','docs','previews','assets/pet','assets/audio','assets/audio-source','upstream/VPet-main/VPet-Simulator.Core']:
    for p in (ROOT/folder).rglob('*'):
        if p.is_file() and not any(part in ('obj','bin','__pycache__','.git') for part in p.relative_to(ROOT).parts):paths.append(p)
for pattern in ['assets/*-atlas.png','tools/*.py','tools/*.ps1','tools/*.iss','tools/*.isl']:
    paths.extend(ROOT.glob(pattern))
for p in (ROOT/'verification').rglob('*'):
    if p.is_file() and ('installed-app' not in p.parts) and (p.name in ['results.json','upstream-smoke.json','install-results.json','restart-results.json','delivery-audit.json','cadence.json'] or p.suffix=='.png'):
        paths.append(p)
for name in ['README.md','LICENSE.txt','global.json','NuGet.Config','assets/pet.ico','upstream/VPet-main/LICENSE','upstream/VPet-main/.editorconfig']:
    if (ROOT/name).is_file():paths.append(ROOT/name)
# Do not ship developer output logs (machine paths), test saves or reference photos.
paths=[p for p in set(paths) if p.suffix!='.log' and p.is_file()]
archive=ROOT/'dist/CyberGolden-Source-0.3.0.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=7) as z:
    for p in sorted(paths):z.write(p,p.relative_to(ROOT).as_posix())
hashes=[]
for name in ['CyberGolden-Setup-0.3.0-x64.exe','CyberGolden-Portable-0.3.0-x64.zip','CyberGolden-Source-0.3.0.zip']:
    p=ROOT/'dist'/name;hashes.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+name)
(ROOT/'dist/SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n',encoding='utf-8')
print(f'{archive.name}: {len(paths)} files, {archive.stat().st_size} bytes')
