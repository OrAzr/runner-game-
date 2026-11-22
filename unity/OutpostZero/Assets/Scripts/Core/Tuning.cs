using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>All owner-tunable difficulty knobs, applied on top of the per-level LevelConfig produced by
    /// Levels.Build. Every default here must equal today's behaviour (multipliers 1, extras 0) so an
    /// untouched install plays exactly as before. See docs/TUNING.md for the JSON format and how each
    /// field reaches gameplay code.</summary>
    [Serializable]
    public class TuningSettings
    {
        public int version = 1;

        // Weapons (AutoShooter)
        public float rookFireRateMult = 1f;
        public float soldierFireRateMult = 1f;
        public float damageMult = 1f;
        public float maxShotsPerSecond = 0f; // 0 = uncapped
        public float soldierShootFraction = 1f; // 0..1

        // Zombies (LevelBuilder / Zombie)
        public float zombieCountMult = 1f;
        public int extraZombieWaves = 0;
        public float zombieHpMult = 1f;
        public float zombieSpeedMult = 1f;
        public float zombieContactDamageMult = 1f;
        public float waveSquadScale = 0f; // 0..1

        // Gates
        public float positiveGateMult = 1f;
        public float multiplierGateFactorMult = 1f;
        public float negativeGateChance = 0f; // 0..1
        public float negativeGateMult = 1f;
        public float counterGateDepthMult = 1f;

        // Squad
        public float squadCapMult = 1f;
        public int startingSquadSize = 1;

        // Boss
        public float bossHpMult = 1f;
        public float bossSpeedMult = 1f;

        // Progression
        public int startingLevel = 1;

        /// <summary>Mutates a freshly-built LevelConfig (from Levels.Build) in place. Levels.Build stays the
        /// single source of truth for a level's shape; tuning only scales the numbers on top of it.</summary>
        public void Apply(LevelConfig cfg)
        {
            cfg.squadCap = Mathf.Max(1, Mathf.RoundToInt(cfg.squadCap * squadCapMult));
            cfg.startCount = Mathf.Clamp(startingSquadSize, 1, cfg.squadCap);
            cfg.bossHp *= bossHpMult;
            cfg.bossSpeed *= bossSpeedMult;
            ApplyZombies(cfg);
            ApplyGates(cfg);
        }

        void ApplyZombies(LevelConfig cfg)
        {
            for (int i = 0; i < cfg.zombies.Count; i++)
            {
                var z = cfg.zombies[i];
                z.count = Mathf.Max(0, Mathf.RoundToInt(z.count * zombieCountMult));
                z.hp *= zombieHpMult;
                z.speed *= zombieSpeedMult;
            }
            if (extraZombieWaves > 0 && cfg.zombies.Count > 0)
            {
                var baseWaves = new List<ZombieGroupDef>(cfg.zombies);
                for (int i = 0; i < extraZombieWaves; i++)
                {
                    var src = baseWaves[i % baseWaves.Count];
                    float t = (i + 1f) / (extraZombieWaves + 1f);
                    float distance = Mathf.Lerp(40f, Mathf.Max(60f, cfg.bossDistance - 30f), t);
                    cfg.zombies.Add(new ZombieGroupDef(distance, src.x, src.count, src.hp, src.speed));
                }
            }
        }

        void ApplyGates(LevelConfig cfg)
        {
            for (int i = 0; i < cfg.gates.Count; i++)
            {
                var g = cfg.gates[i];
                g.left = ApplyGateSide(g.left);
                g.right = ApplyGateSide(g.right);
            }
        }

        // Multiplier/divide gate op chars pass straight through GateOp.Parse/Format so the result always
        // parses back into the same shape Gate.cs already understands.
        string ApplyGateSide(string s)
        {
            var op = GateOp.Parse(s);
            if (op.Counter) return "#" + GateOp.Format(op.n * counterGateDepthMult);
            switch (op.op)
            {
                case '+':
                    if (negativeGateChance > 0f && UnityEngine.Random.value < negativeGateChance)
                        return GateOp.Format(-op.n * negativeGateMult);
                    return GateOp.Format(op.n * positiveGateMult);
                case '-':
                    return GateOp.Format(-op.n * negativeGateMult);
                case 'x':
                    float factor = 1f + (op.n - 1f) * multiplierGateFactorMult;
                    return "x" + factor.ToString("0.##", CultureInfo.InvariantCulture);
                default:
                    return s; // '/' gates are untouched - not in the tunable list
            }
        }
    }

    /// <summary>Owns the single active TuningSettings instance: PlayerPrefs persistence plus clipboard
    /// copy/paste for the owner's "tune on phone, paste into Slack, we bake it into defaults" workflow.</summary>
    public static class Tuning
    {
        const string PrefKey = "outpostzero.tuning";
        static TuningSettings current;

        public static TuningSettings Current => current ?? (current = Load());

        static TuningSettings Load()
        {
            string json = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(json)) return new TuningSettings();
            try { return JsonUtility.FromJson<TuningSettings>(json) ?? new TuningSettings(); }
            catch { return new TuningSettings(); }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            current = new TuningSettings();
            Save();
        }

        public static void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = JsonUtility.ToJson(Current);
        }

        /// <summary>JsonUtility.FromJson ignores JSON fields that have no matching member on TuningSettings,
        /// so unknown fields (e.g. from a future version) are silently dropped rather than rejected.</summary>
        public static bool TryPasteFromClipboard(out string error)
        {
            error = null;
            string json = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(json)) { error = "Clipboard empty"; return false; }
            try
            {
                var parsed = JsonUtility.FromJson<TuningSettings>(json);
                if (parsed == null) { error = "Invalid JSON"; return false; }
                current = parsed;
                Save();
                return true;
            }
            catch { error = "Invalid JSON"; return false; }
        }
    }
}
