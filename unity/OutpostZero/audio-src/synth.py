"""Outpost Zero - original procedural audio. Every sound is synthesized from
oscillators + noise with numpy; no samples are used."""
import numpy as np, os, sys
from scipy.io import wavfile
from scipy.signal import butter, lfilter

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1337)

def t_(d): return np.arange(int(SR*d))/SR
def noise(d): return rng.uniform(-1,1,int(SR*d))
def env_exp(d, k): return np.exp(-t_(d)*k)
def adsr(n, a, d, s, r):
    a,d,r=int(a*SR),int(d*SR),int(r*SR); sl=max(n-a-d-r,0)
    e=np.concatenate([np.linspace(0,1,a,False),np.linspace(1,s,d,False),np.full(sl,s),np.linspace(s,0,r)])
    return np.pad(e,(0,max(0,n-len(e))))[:n]
def bp(x,lo,hi,o=2):
    b,a=butter(o,[lo/(SR/2),hi/(SR/2)],'band'); return lfilter(b,a,x)
def lp(x,f,o=2):
    b,a=butter(o,f/(SR/2),'low'); return lfilter(b,a,x)
def hp(x,f,o=2):
    b,a=butter(o,f/(SR/2),'high'); return lfilter(b,a,x)
def sweep(f0,f1,d,shape='sine'):
    f=np.geomspace(f0,f1,int(SR*d)); ph=2*np.pi*np.cumsum(f)/SR
    if shape=='saw': return 2*((ph/(2*np.pi))%1)-1
    if shape=='sq': return np.sign(np.sin(ph))
    if shape=='tri': return 2*np.abs(2*((ph/(2*np.pi))%1)-1)-1
    return np.sin(ph)
def osc(f,d,shape='sine'): return sweep(f,f*1.0000001,d,shape)
def sat(x,g=2): return np.tanh(x*g)/np.tanh(g)
def norm(x,peak=0.89):
    m=np.max(np.abs(x)); return x/m*peak if m>0 else x
def mix(*parts):
    n=max(len(p) for p in parts); o=np.zeros(n)
    for p in parts: o[:len(p)]+=p
    return o
def fade(x,ms=5):
    n=int(SR*ms/1000); x=x.copy(); x[:n]*=np.linspace(0,1,n); x[-n:]*=np.linspace(1,0,n); return x
def save(name,x,peak=0.89,loop=False):
    x=norm(x,peak) if loop else fade(norm(x,peak)); wavfile.write(os.path.join(OUT,name+".wav"),SR,(x*32767).astype(np.int16)); print(name,len(x)/SR)

# --- weapons
def rifle():
    d=0.16
    crack=bp(noise(d),1500,7000)*env_exp(d,45)
    body=sweep(180,55,d)*env_exp(d,30)
    thump=lp(noise(d),400)*env_exp(d,25)
    return sat(mix(crack*0.9,body*0.8,thump*1.2),3)
def shotgun():
    d=0.45
    blast=lp(noise(d),3000)*env_exp(d,14)
    boom=sweep(120,38,d)*env_exp(d,9)
    crack=hp(noise(d),3000)*env_exp(d,60)
    tail=bp(noise(d),200,900)*env_exp(d,6)*0.4
    return sat(mix(blast,boom*1.3,crack*0.6,tail),2.5)
def flame_loop():
    d=2.0; n=int(SR*d)
    x=lp(noise(d),1800)+0.5*bp(noise(d),80,300)
    # crackles
    cr=np.zeros(n)
    for i in rng.integers(0,n-800,90):
        cr[i:i+800]+=hp(noise(800/SR),2500)*env_exp(800/SR,120)*rng.uniform(.3,1)
    flick=1+0.25*np.sin(2*np.pi*7*t_(d))*np.sin(2*np.pi*0.5*t_(d))
    y=(x*flick+cr*0.6)
    # seamless loop: crossfade tail into head
    c=int(SR*0.25); y[:c]=y[:c]*np.linspace(0,1,c)+y[-c:]*np.linspace(1,0,c); y=y[:-c]
    return sat(y,1.5)
# --- gates / pickups / ui
def gate_good():
    out=np.zeros(int(SR*0.5))
    for i,f in enumerate([523.25,659.25,783.99,1046.5]):
        s=int(i*0.06*SR); d=0.3
        tone=(osc(f,d,'sq')*0.3+osc(f*2,d)*0.3)*env_exp(d,9)
        out[s:s+len(tone)]+=tone[:len(out)-s]
    sh=hp(noise(0.5),5000)*env_exp(0.5,8)*0.15
    return mix(out,sh)
def gate_bad():
    d=0.55
    a=sweep(330,110,d,'saw'); b=sweep(311,104,d,'saw')
    y=lp((a+b)*0.5,1800)*adsr(int(SR*d),0.005,0.1,0.6,0.25)
    buzz=osc(55,d,'sq')*env_exp(d,5)*0.4
    return sat(mix(y,buzz),2)
def crate_open():
    d=0.35
    crack=bp(noise(d),600,3500)*env_exp(d,30)
    knock=sweep(220,90,0.12)*env_exp(0.12,35)
    out=mix(crack,knock*0.9)
    chime=np.zeros(int(SR*0.6))
    for i,f in enumerate([880,1318.5,1760]):
        s=int((0.08+i*0.05)*SR); tone=osc(f,0.35)*env_exp(0.35,10)*0.5
        chime[s:s+len(tone)]+=tone
    return mix(out,chime)
def ui_click():
    d=0.06
    return osc(1800,d,'tri')*env_exp(d,90)+bp(noise(d),3000,8000)*env_exp(d,200)*0.3
# --- zombies
def groan(base=95,d=1.1,seed_mod=0):
    n=int(SR*d); tt=t_(d)
    vib=1+0.04*np.sin(2*np.pi*5.5*tt)+0.02*rng.standard_normal(n).cumsum()/np.sqrt(n)
    f=base*(1+0.25*np.sin(np.pi*tt/d))*vib
    ph=2*np.pi*np.cumsum(f)/SR
    src=(2*((ph/(2*np.pi))%1)-1)+0.3*noise(d)
    # formants (vowel "uuoh")
    v=mix(bp(src,250,450)*1.0,bp(src,600,900)*0.6,bp(src,2300,2700)*0.15)
    am=1+0.3*np.sin(2*np.pi*13*tt)
    return sat(v*am*adsr(n,0.15,0.2,0.8,0.4),2)
def zombie_hit():
    d=0.18
    squish=lp(noise(d),1200)*env_exp(d,25)
    thud=sweep(160,60,d)*env_exp(d,28)
    return sat(mix(squish,thud*1.1),2.5)
def soldier_lost():
    d=0.4
    a=sweep(700,180,d,'sq')*env_exp(d,7)*0.35
    puff=bp(noise(d),300,1500)*env_exp(d,15)
    return mix(a,puff)
def boss_roar():
    d=2.2; n=int(SR*d); tt=t_(d)
    f=60*(1+0.5*np.sin(np.pi*np.clip(tt/1.6,0,1)))
    ph=2*np.pi*np.cumsum(f*(1+0.06*np.sin(2*np.pi*30*tt)))/SR
    src=(2*((ph/(2*np.pi))%1)-1)+0.6*noise(d)
    v=mix(bp(src,150,400),bp(src,500,800)*0.8,bp(src,1100,1600)*0.4)
    rumble=lp(noise(d),120)*1.5
    g=groan(48,d)*0.5
    return sat(mix(v,rumble,g)*adsr(n,0.25,0.3,0.85,0.9),3)
# --- stings
def note(f,d,shape='saw',k=4):
    y=osc(f,d,shape)+0.5*osc(f*1.005,d,shape)
    return lp(y,3500)*adsr(int(SR*d),0.01,0.1,0.7,0.15)
def victory():
    seq=[(392,0.12),(523.25,0.12),(659.25,0.12),(783.99,0.5)]
    out=np.zeros(int(SR*1.6)); p=0
    for f,d in seq:
        s=int(p*SR); y=note(f,d+0.1,'saw')
        out[s:s+len(y)]+=y; p+=d
    s=int(p*SR); ch=sum(note(f,0.9,'sq')*0.4 for f in [523.25,659.25,783.99,1046.5])
    out[s:s+len(ch)]+=ch[:len(out)-s]
    return mix(out, lp(noise(1.6),300)*env_exp(1.6,3)*0.0)
def defeat():
    seq=[(392,0.3),(369.99,0.3),(349.23,0.3),(293.66,1.0)]
    out=np.zeros(int(SR*2.2)); p=0
    for f,d in seq:
        s=int(p*SR); y=note(f,d+0.15,'tri')+note(f/2,d+0.15,'saw')*0.5
        out[s:s+len(y)]+=y[:len(out)-s]; p+=d
    return out

# --- music: 128 BPM, A minor, 16 bars = 30s loop
def music():
    bpm=128; beat=60/bpm; bars=16; total=bars*4*beat; n=int(SR*total)
    out=np.zeros(n+SR)
    def put(x,tsec,g=1.0):
        s=int(tsec*SR); out[s:s+len(x)]+=x*g
    kick=lambda: mix(sweep(150,45,0.25)*env_exp(0.25,14),lp(noise(0.02),2000)*env_exp(0.02,200)*0.3)
    snare=lambda: mix(bp(noise(0.2),900,6000)*env_exp(0.2,22),osc(190,0.12,'tri')*env_exp(0.12,30)*0.5)
    hat=lambda o: hp(noise(0.05 if not o else 0.18),7000)*env_exp(0.05 if not o else 0.18,60 if not o else 18)
    K,S,H=kick(),snare(),hat(False); HO=hat(True)
    # chord roots per bar (Am F C G progression x4)
    prog=[(110,[220,261.63,329.63]),(87.31,[174.61,220,261.63]),(130.81,[261.63,329.63,392]),(98,[196,246.94,293.66])]
    lead_pat=[0,2,3,2, 4,3,2,0, 0,2,3,4, 6,4,3,2]  # scale degrees per 8th (half bar), pentatonic
    amin_pent=[440,523.25,587.33,659.25,783.99,880,1046.5]
    for bar in range(bars):
        b0=bar*4*beat; root,ch=prog[bar%4]
        intro = bar<2
        for q in range(4):
            tb=b0+q*beat
            if not intro or q%2==0: put(K,tb,1.0)
            if q in (1,3) and not intro: put(S,tb,0.7)
            put(H,tb+beat/2,0.25); put(H,tb,0.12)
            if q==3 and bar%4==3: put(HO,tb+beat/2,0.25)
        # driving 8th/16th bass
        for e in range(8):
            tb=b0+e*beat/2; f=root if e%4!=3 else root*1.5
            d=beat/2*0.9
            y=lp(osc(f,d,'saw')+0.6*osc(f*0.5,d,'sq'),900)*adsr(int(SR*d),0.003,0.08,0.6,0.04)
            put(sat(y,2),tb,0.45)
        # stab chords on offbeats
        if bar>=4:
            for q in range(4):
                tb=b0+q*beat+beat/2; d=0.14
                y=sum(osc(f,d,'saw')+osc(f*1.007,d,'saw') for f in ch)
                put(lp(y,2500)*env_exp(d,18),tb,0.08)
        # lead in bars 8-15
        if bar>=8:
            for e in range(8):
                deg=lead_pat[(bar%2)*8+e]; f=amin_pent[deg]
                d=beat/2; tb=b0+e*beat/2
                y=(osc(f,d,'sq')*0.5+osc(f*2,d,'tri')*0.3)*adsr(int(SR*d),0.005,0.05,0.5,0.06)
                put(lp(y,4000),tb,0.16)
        # riser into bar 8
        if bar==7:
            r=hp(noise(4*beat),1000)*np.linspace(0,1,int(SR*4*beat))**2
            put(r,b0,0.25)
    # loop-wrap tail
    tail=out[n:]; out=out[:n]; out[:len(tail)]+=tail
    return sat(out,1.3)

save("sfx_rifle",rifle()); save("sfx_shotgun",shotgun()); save("sfx_flamethrower_loop",flame_loop(),0.7,loop=True)
save("sfx_gate_good",gate_good()); save("sfx_gate_bad",gate_bad()); save("sfx_crate_open",crate_open())
save("sfx_ui_click",ui_click(),0.7); save("sfx_zombie_groan",groan()); save("sfx_zombie_hit",zombie_hit())
save("sfx_soldier_lost",soldier_lost()); save("sfx_boss_roar",boss_roar())
save("sting_victory",victory()); save("sting_defeat",defeat()); save("music_battle_loop",music(),0.8,loop=True)
