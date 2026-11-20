// Outpost Zero - WebAudio manager. All sounds are original, synthesized in audio-src/synth.py
import { AUDIO_DATA } from './audio-data.js';

const LOOP_LEN = { music_battle_loop: 30.0, sfx_flamethrower_loop: 1.75 };

export class AudioManager {
  constructor() {
    this.ctx = null; this.buffers = {}; this.loops = {}; this.last = {};
    this.muted = false; this.ready = false;
  }
  async unlock() {
    if (this.ctx) { if (this.ctx.state === 'suspended') await this.ctx.resume(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    this.ctx = new AC();
    this.master = this.ctx.createGain(); this.master.gain.value = 0.9; this.master.connect(this.ctx.destination);
    this.sfxBus = this.ctx.createGain(); this.sfxBus.gain.value = 0.8; this.sfxBus.connect(this.master);
    this.musicBus = this.ctx.createGain(); this.musicBus.gain.value = 0.45; this.musicBus.connect(this.master);
    // iOS: play a silent buffer inside the gesture
    const b = this.ctx.createBuffer(1, 1, 22050); const s = this.ctx.createBufferSource(); s.buffer = b; s.connect(this.ctx.destination); s.start(0);
    await Promise.all(Object.entries(AUDIO_DATA).map(async ([name, b64]) => {
      try {
        const bin = atob(b64); const arr = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
        this.buffers[name] = await new Promise((res, rej) => this.ctx.decodeAudioData(arr.buffer, res, rej));
      } catch (e) { console.warn('audio decode failed', name, e); }
    }));
    this.ready = true;
  }
  play(name, { vol = 1, rate = 1, throttle = 0, detune = 0 } = {}) {
    if (!this.ready || this.muted) return;
    const buf = this.buffers[name]; if (!buf) return;
    const now = this.ctx.currentTime;
    if (throttle && this.last[name] && now - this.last[name] < throttle) return;
    this.last[name] = now;
    const src = this.ctx.createBufferSource(); src.buffer = buf;
    src.playbackRate.value = rate * (1 + (Math.random() - 0.5) * detune);
    const g = this.ctx.createGain(); g.gain.value = vol;
    src.connect(g); g.connect(this.sfxBus); src.start();
  }
  // Seamless loop: trims any mp3 encoder padding by finding the first non-silent sample
  startLoop(name, bus = 'sfx', vol = 1) {
    if (!this.ready || this.loops[name]) return;
    const buf = this.buffers[name]; if (!buf) return;
    const d = buf.getChannelData(0); let lead = 0;
    while (lead < d.length && Math.abs(d[lead]) < 0.002) lead++;
    const src = this.ctx.createBufferSource(); src.buffer = buf; src.loop = true;
    const L = LOOP_LEN[name] || buf.duration;
    src.loopStart = lead / buf.sampleRate; src.loopEnd = Math.min(buf.duration, src.loopStart + L);
    const g = this.ctx.createGain(); g.gain.value = 0;
    g.gain.linearRampToValueAtTime(this.muted ? 0 : vol, this.ctx.currentTime + 0.08);
    src.connect(g); g.connect(bus === 'music' ? this.musicBus : this.sfxBus);
    src.start(0, src.loopStart);
    this.loops[name] = { src, g, vol };
  }
  stopLoop(name, fade = 0.15) {
    const l = this.loops[name]; if (!l) return; delete this.loops[name];
    const t = this.ctx.currentTime; l.g.gain.cancelScheduledValues(t); l.g.gain.setValueAtTime(l.g.gain.value, t);
    l.g.gain.linearRampToValueAtTime(0, t + fade); l.src.stop(t + fade + 0.02);
  }
  setMuted(m) { this.muted = m; if (this.master) this.master.gain.value = m ? 0 : 0.9; }
}
