"""Outpost Zero - original procedural audio. Every sound is synthesized from
oscillators, noise and envelopes with numpy. No samples, no third-party recordings.

The weapon shots are layered (transient click + noise body + low thump) and the
battle loop is a 30 second original arrangement. Playback code varies pitch per shot
and raises the gate-tick pitch as a counter climbs.
"""
import numpy as np, os, sys
from scipy.io import wavfile
from scipy.signal import butter, lfilter

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1337)

def t_(d): return np.arange(int(SR * d)) / SR
def noise(d): return rng.uniform(-1, 1, int(SR * d))
def env_exp(d, k): return np.exp(-t_(d) * k)
def adsr(n, a, d, s, r):
    a, d, r = int(a * SR), int(d * SR), int(r * SR); sl = max(n - a - d - r, 0)
    e = np.concatenate([np.linspace(0, 1, max(a, 1), False), np.linspace(1, s, max(d, 1), False), np.full(sl, s), np.linspace(s, 0, max(r, 1))])
    return np.pad(e, (0, max(0, n - len(e))))[:n]
def bp(x, lo, hi, o=2):
    lo = max(lo, 20); hi = min(hi, SR / 2 - 100)
    b, a = butter(o, [lo / (SR / 2), hi / (SR / 2)], 'band'); return lfilter(b, a, x)
def lp(x, f, o=2):
    b, a = butter(o, min(f, SR / 2 - 100) / (SR / 2), 'low'); return lfilter(b, a, x)
def hp(x, f, o=2):
    b, a = butter(o, min(f, SR / 2 - 100) / (SR / 2), 'high'); return lfilter(b, a, x)
def sweep(f0, f1, d, shape='sine'):
    n = max(int(SR * d), 8)
    f = np.geomspace(max(f0, 1), max(f1, 1), n); ph = 2 * np.pi * np.cumsum(f) / SR
    if shape == 'saw': return 2 * ((ph / (2 * np.pi)) % 1) - 1
    if shape == 'sq': return np.sign(np.sin(ph))
    if shape == 'tri': return 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1) - 1) - 1
    return np.sin(ph)
def osc(f, d, shape='sine'): return sweep(f, f * 1.0000001, d, shape)
def sat(x, g=2): return np.tanh(x * g) / np.tanh(g)
def norm(x, peak=0.89):
    m = np.max(np.abs(x)); return x / m * peak if m > 0 else x
def mix(*parts):
    n = max(len(p) for p in parts); o = np.zeros(n)
    for p in parts: o[:len(p)] += p
    return o
def fade(x, ms=4):
    n = max(int(SR * ms / 1000), 2); x = x.copy(); x[:n] *= np.linspace(0, 1, n); x[-n:] *= np.linspace(1, 0, n); return x
def save(name, x, peak=0.89, loop=False):
    x = norm(x, peak) if loop else fade(norm(x, peak))
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
    print(name, round(len(x) / SR, 3))

# --- weapons: click transient + noise body + low thump
def rifle():
    click = hp(noise(0.004), 5200) * env_exp(0.004, 700)
    snap = osc(3600, 0.01, 'tri') * env_exp(0.01, 320) * 0.28
    body = bp(noise(0.07), 800, 4200) * env_exp(0.07, 70) * 0.32
    thump = sweep(170, 46, 0.055) * env_exp(0.055, 48) * 0.55
    return sat(mix(click, snap, body, thump), 1.6)

def shotgun():
    click = hp(noise(0.006), 4000) * env_exp(0.006, 450)
    blast = bp(noise(0.16), 200, 2200) * env_exp(0.16, 18) * 0.4
    boom = sweep(110, 36, 0.14) * env_exp(0.14, 14) * 0.7
    crack = hp(noise(0.03), 4500) * env_exp(0.03, 80) * 0.35
    return sat(mix(click, blast, boom, crack), 1.5)

def flame_loop():
    d = 2.0; n = int(SR * d)
    roar = bp(noise(d), 180, 1600) + 0.45 * hp(noise(d), 2200)
    cr = np.zeros(n)
    for i in rng.integers(0, n - 600, 110):
        cr[i:i + 600] += hp(noise(600 / SR), 2800) * env_exp(600 / SR, 140) * rng.uniform(0.25, 1)
    flick = 0.82 + 0.18 * np.sin(2 * np.pi * 9 * t_(d))
    y = sat(roar * flick + cr * 0.55, 1.6)
    c = int(SR * 0.25); y = y.copy()
    y[:c] = y[:c] * np.linspace(0, 1, c) + y[-c:] * np.linspace(1, 0, c)
    return y[:-c]

# --- gates
def gate_tick():
    """Short bright tick. The game raises playbackRate as the counter climbs."""
    d = 0.055
    tone = (osc(1560, d) * 0.7 + osc(3120, d, 'tri') * 0.35) * env_exp(d, 70)
    click = hp(noise(0.006), 6000) * env_exp(0.006, 500) * 0.8
    return sat(mix(tone, click), 2.0)

def gate_good():
    """Whoosh into a rising chime — the positive gate-pass sting."""
    d = 0.62
    n = int(SR * d)
    who = bp(noise(d), 500, 7000) * (np.linspace(0.15, 1, n) ** 1.4) * env_exp(d, 5.5)
    rise = sweep(420, 1680, 0.28, 'saw') * env_exp(0.28, 6) * 0.28
    out = np.zeros(n)
    for i, f in enumerate([659.25, 830.61, 987.77, 1318.51]):
        s = int((0.08 + i * 0.045) * SR)
        tone = mix(osc(f, 0.42) * 0.55 * env_exp(0.42, 7), osc(f * 2, 0.3) * 0.22 * env_exp(0.3, 8))
        out[s:s + len(tone)] += tone[:max(0, n - s)]
    return sat(mix(who * 0.55, rise, out), 1.8)

def gate_bad():
    d = 0.5
    a = sweep(420, 90, d, 'saw'); b = sweep(390, 78, d, 'saw')
    y = lp((a + b) * 0.5, 1600) * adsr(int(SR * d), 0.004, 0.08, 0.55, 0.22)
    drop = sweep(180, 40, 0.35) * env_exp(0.35, 6) * 0.7
    return sat(mix(y, drop), 2)

def crate_open():
    d = 0.28
    crack = bp(noise(d), 700, 4000) * env_exp(d, 28)
    knock = sweep(240, 80, 0.1) * env_exp(0.1, 32)
    chime = np.zeros(int(SR * 0.55))
    for i, f in enumerate([988, 1480, 1976]):
        s = int((0.06 + i * 0.04) * SR); tone = osc(f, 0.32) * env_exp(0.32, 9) * 0.55
        chime[s:s + len(tone)] += tone
    return mix(crack, knock * 0.8, chime)

def ui_click():
    d = 0.045
    return osc(2100, d, 'tri') * env_exp(d, 110) + hp(noise(d), 4000) * env_exp(d, 240) * 0.35

# --- zombies / squad
def groan(base=92, d=1.05):
    n = int(SR * d); tt = t_(d)
    vib = 1 + 0.045 * np.sin(2 * np.pi * 5.2 * tt)
    f = base * (1 + 0.22 * np.sin(np.pi * tt / d)) * vib
    ph = 2 * np.pi * np.cumsum(f) / SR
    src = (2 * ((ph / (2 * np.pi)) % 1) - 1) * 0.7 + noise(d) * 0.25
    v = mix(bp(src, 220, 420), bp(src, 700, 980) * 0.55, bp(src, 2200, 2800) * 0.12)
    return sat(v * (1 + 0.25 * np.sin(2 * np.pi * 12 * tt)) * adsr(n, 0.12, 0.18, 0.75, 0.35), 2)

def zombie_hit():
    slap = hp(noise(0.008), 1500) * env_exp(0.008, 280)
    meat = bp(noise(0.09), 160, 800) * env_exp(0.09, 32) * 0.4
    thud = sweep(150, 40, 0.08) * env_exp(0.08, 30) * 0.65
    return sat(mix(slap, meat, thud), 1.7)

def soldier_lost():
    d = 0.36
    a = sweep(880, 160, d, 'sq') * env_exp(d, 8) * 0.32
    puff = bp(noise(d), 400, 1800) * env_exp(d, 14)
    return mix(a, puff)

def boss_roar():
    d = 1.7; n = int(SR * d); tt = t_(d)
    f = 48 * (1 + 0.85 * np.sin(np.pi * np.clip(tt / 1.15, 0, 1)))
    ph = 2 * np.pi * np.cumsum(f * (1 + 0.07 * np.sin(2 * np.pi * 28 * tt))) / SR
    src = sat(np.sin(ph) * 0.6 + (2 * ((ph / (2 * np.pi)) % 1) - 1) * 0.5 + noise(d) * 0.35, 2.4)
    v = mix(bp(src, 70, 180) * 1.4, bp(src, 200, 480), bp(src, 700, 1200) * 0.45)
    sub = sweep(70, 32, d) * env_exp(d, 1.6) * 0.8
    return sat(mix(v, sub) * adsr(n, 0.08, 0.2, 0.9, 0.55), 2.3)

# --- stings
def note(f, d, shape='saw'):
    y = osc(f, d, shape) + 0.45 * osc(f * 1.005, d, shape)
    return lp(y, 4200) * adsr(int(SR * d), 0.008, 0.08, 0.65, 0.12)
def victory():
    seq = [(392, 0.1), (523.25, 0.1), (659.25, 0.1), (783.99, 0.42)]
    out = np.zeros(int(SR * 1.35)); p = 0
    for f, d in seq:
        s = int(p * SR); y = note(f, d + 0.12, 'saw'); out[s:s + len(y)] += y[:len(out) - s]; p += d
    s = int(p * SR); ch = sum(note(f, 0.7, 'sq') * 0.35 for f in [523.25, 659.25, 783.99, 1046.5])
    out[s:s + len(ch)] += ch[:len(out) - s]
    return out
def defeat():
    seq = [(392, 0.28), (369.99, 0.28), (311.13, 0.3), (233.08, 0.9)]
    out = np.zeros(int(SR * 2.0)); p = 0
    for f, d in seq:
        s = int(p * SR); y = note(f, d + 0.12, 'tri') + note(f / 2, d + 0.12, 'saw') * 0.45
        out[s:s + len(y)] += y[:len(out) - s]; p += d
    return out

# --- music: 128 BPM, A minor, 16 bars = exactly 30s
def music():
    bpm = 128; beat = 60 / bpm; bars = 16; total = bars * 4 * beat
    n = int(round(SR * total))
    drums = np.zeros(n + SR); bass = np.zeros(n + SR); lead = np.zeros(n + SR); stab = np.zeros(n + SR)
    def put(buf, x, tsec, g=1.0):
        s = int(tsec * SR)
        if s >= len(buf): return
        e = min(len(buf), s + len(x)); buf[s:e] += x[:e - s] * g
    kick = sat(mix(sweep(180, 42, 0.18) * env_exp(0.18, 16), lp(noise(0.012), 2500) * env_exp(0.012, 260) * 0.55), 1.6)
    snare = sat(mix(bp(noise(0.16), 1200, 7000) * env_exp(0.16, 18), osc(196, 0.08, 'tri') * env_exp(0.08, 28) * 0.45), 1.8)
    clap = bp(noise(0.09), 900, 3500) * env_exp(0.09, 28)
    hat = hp(noise(0.04), 7500) * env_exp(0.04, 70)
    hatO = hp(noise(0.16), 6500) * env_exp(0.16, 16)
    # Am F C G
    prog = [(110, [220, 261.63, 329.63]), (87.31, [174.61, 220, 261.63]), (130.81, [261.63, 329.63, 392]), (98, [196, 246.94, 293.66])]
    # catchy 8th-note hook, A minor pentatonic degrees
    hook = [0, 4, 2, 3, 4, 2, 0, 3]
    pent = [440, 523.25, 587.33, 659.25, 783.99, 880, 1046.5, 1174.66]
    for bar in range(bars):
        b0 = bar * 4 * beat; root, ch = prog[bar % 4]
        for q in range(4):
            tb = b0 + q * beat
            put(drums, kick, tb, 1.05)
            if q in (1, 3):
                put(drums, kick, tb + beat * 0.5, 0.55)  # offbeat push
                put(drums, snare, tb, 0.85)
                put(drums, clap, tb, 0.28)
            put(drums, hat, tb, 0.22); put(drums, hat, tb + beat * 0.5, 0.16)
            if q == 3: put(drums, hatO, tb + beat * 0.5, 0.2)
        for e in range(8):
            tb = b0 + e * beat / 2
            f = root * (1.5 if e % 4 == 3 else 1)
            d = beat / 2 * 0.92
            sub = osc(f / 2, d) * adsr(int(SR * d), 0.002, 0.05, 0.7, 0.03)
            saw = lp(osc(f, d, 'saw'), 700) * adsr(int(SR * d), 0.003, 0.06, 0.55, 0.03)
            put(bass, sat(sub * 0.9 + saw * 0.55, 1.7), tb, 0.72)
        if bar >= 2:
            for q in (0, 2):
                tb = b0 + q * beat + beat * 0.5; d = 0.12
                y = sum(osc(f, d, 'saw') for f in ch)
                put(stab, lp(y, 3200) * env_exp(d, 22), tb, 0.11)
        if bar >= 2:
            for e in range(8):
                f = pent[hook[(bar + e) % len(hook)]]
                if bar % 4 == 3 and e >= 6: f = pent[min(6, hook[e] + 2)]
                d = beat / 2 * 0.86; tb = b0 + e * beat / 2
                y = osc(f, d, 'sq') * 0.42 + osc(f * 2, d, 'tri') * 0.22
                acc = 1.25 if e in (0, 4) else 0.85
                put(lead, lp(y, 4800) * adsr(int(SR * d), 0.004, 0.04, 0.45, 0.05), tb, 0.3 * acc)
        if bar == 7:
            r = hp(noise(4 * beat), 1400) * np.linspace(0, 1, int(SR * 4 * beat)) ** 2
            put(drums, r, b0, 0.35)
    # duck bass under kicks
    duck = np.ones(len(bass))
    win = int(0.12 * SR)
    ramp = np.linspace(0.62, 1, win)
    for bar in range(bars):
        for q in range(4):
            s = int((bar * 4 * beat + q * beat) * SR)
            duck[s:s + win] *= ramp
    bass *= duck[:len(bass)]
    out = drums * 0.92 + bass * 0.85 + lead * 1.05 + stab
    tail = out[n:]; out = out[:n]; out[:len(tail)] += tail * 0.85
    return sat(out, 1.15)

save("sfx_rifle", rifle(), 0.9)
save("sfx_shotgun", shotgun(), 0.92)
save("sfx_flamethrower_loop", flame_loop(), 0.62, loop=True)
save("sfx_gate_tick", gate_tick(), 0.72)
save("sfx_gate_good", gate_good(), 0.9)
save("sfx_gate_bad", gate_bad(), 0.86)
save("sfx_crate_open", crate_open(), 0.86)
save("sfx_ui_click", ui_click(), 0.62)
save("sfx_zombie_groan", groan(), 0.8)
save("sfx_zombie_hit", zombie_hit(), 0.88)
save("sfx_soldier_lost", soldier_lost(), 0.8)
save("sfx_boss_roar", boss_roar(), 0.95)
save("sting_victory", victory(), 0.86)
save("sting_defeat", defeat(), 0.84)
save("music_battle_loop", music(), 0.86, loop=True)
