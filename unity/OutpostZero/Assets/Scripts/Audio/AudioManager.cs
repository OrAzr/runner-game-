using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    public enum Sfx { Rifle, Shotgun, FlameLoop, GateGood, GateBad, GateTick, ZombieGroan, ZombieHit, SoldierLost, CrateOpen, BossRoar, Victory, Defeat, UIClick }

    /// <summary>Plays the original synthesized clips in Assets/Audio. Clips are wired by the scene builder
    /// (Outpost Zero > Build Main Scene); a small voice pool avoids allocation, with per-sound throttling.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        [System.Serializable] public class Entry { public Sfx id; public AudioClip clip; }
        public List<Entry> clips = new List<Entry>();
        public AudioClip music;
        [Range(0, 1)] public float sfxVolume = 0.7f, musicVolume = 0.55f;
        public int voices = 16;

        readonly Dictionary<Sfx, AudioClip> map = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        AudioSource[] pool; int next; AudioSource musicSrc, flameSrc;
        public bool Muted { get; private set; }

        void Awake()
        {
            I = this;
            foreach (var e in clips) if (e.clip) map[e.id] = e.clip;
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++) { pool[i] = gameObject.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            musicSrc = gameObject.AddComponent<AudioSource>(); musicSrc.loop = true; musicSrc.playOnAwake = false; musicSrc.clip = music;
            flameSrc = gameObject.AddComponent<AudioSource>(); flameSrc.loop = true; flameSrc.playOnAwake = false;
            if (map.TryGetValue(Sfx.FlameLoop, out var f)) flameSrc.clip = f;
        }

        public void Play(Sfx id, float volume = 1f, float pitch = 1f, float throttle = 0f)
        {
            if (Muted || !map.TryGetValue(id, out var clip)) return;
            float now = Time.unscaledTime;
            if (throttle > 0 && lastPlayed.TryGetValue(id, out var t) && now - t < throttle) return;
            lastPlayed[id] = now;
            var src = pool[next]; next = (next + 1) % pool.Length;
            src.pitch = pitch * Random.Range(0.97f, 1.04f);
            src.PlayOneShot(clip, volume * sfxVolume);
        }

        public void PlayMusic() { if (music && !musicSrc.isPlaying) { musicSrc.volume = Muted ? 0 : musicVolume; musicSrc.Play(); } }
        public void StopMusic() { musicSrc.Stop(); }
        public void StartFlame() { if (flameSrc.clip && !flameSrc.isPlaying) { flameSrc.volume = Muted ? 0 : 0.55f * sfxVolume; flameSrc.Play(); } }
        public void StopFlame() { if (flameSrc) flameSrc.Stop(); }

        public void SetMuted(bool m)
        {
            Muted = m; musicSrc.volume = m ? 0 : musicVolume; flameSrc.volume = m ? 0 : 0.55f * sfxVolume;
        }
    }
}
