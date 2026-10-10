"""Canonical APK entry separators before signing, especially Windows aapt2 assets."""
from pathlib import Path
import zipfile,sys,copy
apk=Path(sys.argv[1]); temporary=apk.with_suffix('.canonical.apk')
with zipfile.ZipFile(apk) as src,zipfile.ZipFile(temporary,'w') as dst:
    names=set()
    for entry in src.infolist():
        target=copy.copy(entry);target.filename=entry.filename.replace('\\','/')
        if target.filename in names:raise RuntimeError('Duplicate APK entry')
        names.add(target.filename);dst.writestr(target,src.read(entry))
temporary.replace(apk)
print(f'Canonical APK entries: {len(names)}')
