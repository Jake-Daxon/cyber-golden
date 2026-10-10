"""Local audio: licensed dog recording derivatives plus original procedural foley.
No sounds are represented as the photographed dog's voice.
"""
from pathlib import Path
import wave,json
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly, butter, sosfilt
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'assets/audio';OUT.mkdir(exist_ok=True)
SR=22050
def write(name,a):
    a=np.clip(a,-.8,.8)
    with wave.open(str(OUT/f'{name}.wav'),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(SR);f.writeframes((a*32767).astype('<i2').tobytes())
data,rate=sf.read(ROOT/'assets/audio-source/dog.ogg',always_2d=True)
mono=data.mean(axis=1)
mono=resample_poly(mono,SR,rate)
rms=np.sqrt(np.convolve(mono**2,np.ones(221)/221,mode='same'))
start=max(0,np.argmax(rms>.11)-250)
snippet=mono[start:start+int(.65*SR)]
# A modest pitch/time lift and low-pass removes the harshness of the source bark.
snippet=resample_poly(snippet,4,5)
snippet=sosfilt(butter(3,5200,fs=SR,output='sos'),snippet)
snippet/=max(.01,np.max(np.abs(snippet)));snippet*=.45
fade=min(200,len(snippet)//4);snippet[:fade]*=np.linspace(0,1,fade);snippet[-fade:]*=np.linspace(1,0,fade)
write('click',snippet)
write('happy',np.concatenate([snippet,np.zeros(int(.16*SR)),snippet*.75]))
# Original deterministic textured crunch and water-lap foley (not electronic beeps).
rng=np.random.default_rng(719)
eat=np.zeros(int(1.6*SR));drink=np.zeros(int(1.4*SR))
for index in range(5):
    n=int(.13*SR);t=np.arange(n)/SR
    noise=rng.normal(size=n)
    crunch=sosfilt(butter(2,[500,5600],btype='bandpass',fs=SR,output='sos'),noise)*np.exp(-t*30)*.075
    at=int((.1+index*.27)*SR);eat[at:at+n]+=crunch
for index in range(6):
    n=int(.12*SR);t=np.arange(n)/SR
    lap=sosfilt(butter(2,[170,1900],btype='bandpass',fs=SR,output='sos'),rng.normal(size=n))*np.sin(np.pi*t/.12)**2*.045
    at=int((.04+index*.2)*SR);drink[at:at+n]+=lap
write('eat',eat);write('drink',drink)
n=int(2.5*SR);t=np.arange(n)/SR
breath=sosfilt(butter(2,650,fs=SR,output='sos'),rng.normal(size=n))*(np.sin(np.pi*t/2.5)**2)*.025
write('breath',breath)
report={name:{'seconds':len(a)/SR,'peak':float(np.max(np.abs(a)))} for name,a in [('click',snippet),('happy',np.concatenate([snippet,np.zeros(int(.16*SR)),snippet*.75])),('eat',eat),('drink',drink),('breath',breath)]}
(ROOT/'docs/audio-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(report)
