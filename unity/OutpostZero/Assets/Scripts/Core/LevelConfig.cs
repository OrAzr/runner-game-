using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Data for one battle run. Values mirror the browser prototype (web/level.js).</summary>
    [Serializable]
    public class GateDef { public float distance; public string left; public string right;
        public GateDef(float d, string l, string r) { distance = d; left = l; right = r; } }

    [Serializable]
    public class ZombieGroupDef { public float distance; public float x; public int count; public float hp; public float speed;
        public ZombieGroupDef(float d, float x, int n, float hp, float speed = 1.6f) { distance = d; this.x = x; count = n; this.hp = hp; this.speed = speed; } }

    [Serializable]
    public class CrateDef { public float distance; public float x; public WeaponType weapon; public float hp;
        public CrateDef(float d, float x, WeaponType w, float hp) { distance = d; this.x = x; weapon = w; this.hp = hp; } }

    [Serializable]
    public class LevelConfig
    {
        public float runSpeed = 6.2f;
        public float bossDistance = 220f;
        public int startCount = 1;           // the run starts with Rook alone
        public float roadHalfWidth = 5f;
        public float bossHp = 900f;
        public float bossSpeed = 2.4f;
        public float bossStartGap = 20f;
        public float pickupDuration = 10f;
        public int squadCap = 12;            // hard ceiling on Count so gate snowballing can't trivialize a level

        // "#" gates are shootable counters (start negative, bullets add 1, stronger guns add more).
        public List<GateDef> gates = new List<GateDef> {
            new GateDef(20, "+3", "+2"), new GateDef(55, "+2", "#-6"), new GateDef(95, "+4", "+2"),
            new GateDef(135, "#-8", "+3"), new GateDef(175, "+3", "x2"),
        };
        public List<ZombieGroupDef> zombies = new List<ZombieGroupDef> {
            new ZombieGroupDef(45, 0, 3, 3, 1.6f), new ZombieGroupDef(95, -2, 3, 3, 1.6f),
            new ZombieGroupDef(145, 2, 4, 4, 1.7f), new ZombieGroupDef(190, 0, 4, 5, 1.7f),
        };
        public List<CrateDef> crates = new List<CrateDef> {
            new CrateDef(70, 3.3f, WeaponType.Shotgun, 6),
        };
    }

    /// <summary>Data-driven level table: one hand-tuned entry per level for the first Designed levels
    /// (easy onboarding early, genuinely dangerous to a careless player by level 3-5), then formula-scaled
    /// beyond that so the run never runs out of levels. See docs/LEVEL_RULES.md for the full table.</summary>
    public static class Levels
    {
        public const int Designed = 10;

        // QA-only: the same "-qaShots" flag QaShotRunner checks also makes every BuildRun() load a short,
        // compressed layout instead of the real table, so an automated full-run (road, gates, zombies, boss,
        // end screen) finishes in well under a minute instead of the shipped ~35s-per-level-times-the-boss-
        // fight. Shipped builds never pass this flag, so real level lengths are never affected.
        static bool? qaShortLevel;
        static bool QaShortLevel
        {
            get
            {
                if (qaShortLevel == null)
                {
                    qaShortLevel = false;
                    var args = System.Environment.GetCommandLineArgs();
                    for (int i = 0; i < args.Length; i++) if (args[i] == "-qaShots") { qaShortLevel = true; break; }
                }
                return qaShortLevel.Value;
            }
        }

        public static LevelConfig Build(int levelNumber)
        {
            if (QaShortLevel) return QaLevel();
            int n = Mathf.Max(1, levelNumber);
            return n <= Designed ? Table(n) : ScaleBeyond(Table(Designed), n - Designed);
        }

        /// <summary>~24s of road at this speed (150 / 6.2), with 3 gates and 2 zombie groups before the
        /// boss - enough for QaShotRunner to capture a gate pass and zombie combat without waiting through
        /// a full designed level. Boss HP is cut hard (900 -> 60) so the fight itself also resolves quickly
        /// under automated capture instead of running for a minute against whatever squad size QA happens
        /// to have at that point. Both sides of every gate are plain positive ops (no "#" counters, which
        /// start negative and only climb if bullets happen to hit them): the automated runner never drags
        /// to steer, so SquadController.x sits at exactly 0 the whole run and Gate.Update's `x < 0f` side
        /// pick deterministically always resolves to the right gate - a counter gate there would reliably
        /// wipe a small squad on every single QA pass instead of being down to the luck a real, steering
        /// player would have. (startCount isn't set here - Tuning.Apply always overwrites LevelConfig.
        /// startCount from Tuning.Current.startingSquadSize regardless of this value, so QaShotRunner sets
        /// that directly before building this level.)</summary>
        static LevelConfig QaLevel() => new LevelConfig
        {
            runSpeed = 6.2f, bossDistance = 150f, bossHp = 60f, bossSpeed = 2.4f, bossStartGap = 14f, squadCap = 24,
            gates = new List<GateDef> {
                new GateDef(20, "+3", "+2"), new GateDef(60, "+2", "+3"), new GateDef(100, "+4", "+2"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(40, 0, 3, 3, 1.6f), new ZombieGroupDef(90, -2, 3, 3, 1.6f),
            },
            crates = new List<CrateDef> { new CrateDef(70, 3.3f, WeaponType.Shotgun, 6) },
        };

        static LevelConfig Table(int n)
        {
            switch (n)
            {
                case 1: return Level1();
                case 2: return Level2();
                case 3: return Level3();
                case 4: return Level4();
                case 5: return Level5();
                case 6: return Level6();
                case 7: return Level7();
                case 8: return Level8();
                case 9: return Level9();
                default: return Level10();
            }
        }

        static LevelConfig Level1() => new LevelConfig
        {
            runSpeed = 6.2f, bossDistance = 220f, bossHp = 900f, bossSpeed = 2.4f, bossStartGap = 20f, squadCap = 12,
            gates = new List<GateDef> {
                new GateDef(20, "+3", "+2"), new GateDef(55, "+2", "#-6"), new GateDef(95, "+4", "+2"),
                new GateDef(135, "#-8", "+3"), new GateDef(175, "+3", "x2"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 3, 3, 1.6f), new ZombieGroupDef(95, -2, 3, 3, 1.6f),
                new ZombieGroupDef(145, 2, 4, 4, 1.7f), new ZombieGroupDef(190, 0, 4, 5, 1.7f),
            },
            crates = new List<CrateDef> { new CrateDef(70, 3.3f, WeaponType.Shotgun, 6) },
        };

        static LevelConfig Level2() => new LevelConfig
        {
            runSpeed = 6.3f, bossDistance = 260f, bossHp = 1100f, bossSpeed = 2.6f, bossStartGap = 20f, squadCap = 16,
            gates = new List<GateDef> {
                new GateDef(20, "+4", "+3"), new GateDef(55, "#-10", "+3"), new GateDef(95, "+3", "#-10"),
                new GateDef(140, "+5", "x2"), new GateDef(185, "#-12", "+4"), new GateDef(225, "+3", "#-14"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 4, 4, 1.7f), new ZombieGroupDef(90, -2, 4, 4, 1.7f),
                new ZombieGroupDef(135, 2, 4, 5, 1.8f), new ZombieGroupDef(180, -1.8f, 5, 5, 1.8f),
                new ZombieGroupDef(220, 0, 5, 6, 1.8f),
            },
            crates = new List<CrateDef> { new CrateDef(70, 3.3f, WeaponType.Shotgun, 7), new CrateDef(170, -3.3f, WeaponType.Flamethrower, 10) },
        };

        static LevelConfig Level3() => new LevelConfig
        {
            runSpeed = 6.45f, bossDistance = 300f, bossHp = 1350f, bossSpeed = 2.9f, bossStartGap = 19f, squadCap = 20,
            gates = new List<GateDef> {
                new GateDef(20, "+3", "#-14"), new GateDef(55, "#-16", "+4"), new GateDef(95, "/2", "+4"),
                new GateDef(140, "#-18", "+3"), new GateDef(185, "+4", "#-20"), new GateDef(230, "x2", "#-16"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 4, 5, 1.8f), new ZombieGroupDef(90, -2.2f, 5, 6, 1.9f),
                new ZombieGroupDef(140, 2, 5, 6, 1.9f), new ZombieGroupDef(185, -1.8f, 5, 7, 2.0f),
                new ZombieGroupDef(235, 0, 6, 8, 2.0f),
            },
            crates = new List<CrateDef> { new CrateDef(70, 3.3f, WeaponType.Shotgun, 8), new CrateDef(190, -3.3f, WeaponType.Flamethrower, 12) },
        };

        static LevelConfig Level4() => new LevelConfig
        {
            runSpeed = 6.6f, bossDistance = 340f, bossHp = 1600f, bossSpeed = 3.1f, bossStartGap = 19f, squadCap = 24,
            gates = new List<GateDef> {
                new GateDef(20, "+3", "#-16"), new GateDef(55, "#-18", "+3"), new GateDef(95, "#-22", "+4"),
                new GateDef(140, "/2", "#-18"), new GateDef(185, "+4", "#-24"), new GateDef(230, "#-20", "x1.6"),
                new GateDef(270, "#-26", "+3"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 5, 6, 1.9f), new ZombieGroupDef(90, -2, 5, 7, 2.0f),
                new ZombieGroupDef(140, 2.2f, 6, 7, 2.0f), new ZombieGroupDef(185, -2, 6, 8, 2.1f),
                new ZombieGroupDef(230, 0, 6, 9, 2.1f),
            },
            crates = new List<CrateDef> { new CrateDef(75, 3.3f, WeaponType.Shotgun, 9), new CrateDef(195, -3.3f, WeaponType.Flamethrower, 13) },
        };

        static LevelConfig Level5() => new LevelConfig
        {
            runSpeed = 6.75f, bossDistance = 380f, bossHp = 1850f, bossSpeed = 3.3f, bossStartGap = 18f, squadCap = 28,
            gates = new List<GateDef> {
                new GateDef(20, "#-18", "+3"), new GateDef(55, "#-22", "+3"), new GateDef(95, "/2", "#-20"),
                new GateDef(140, "#-26", "+4"), new GateDef(185, "#-24", "/2"), new GateDef(230, "x1.5", "#-28"),
                new GateDef(270, "#-24", "+4"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 5, 7, 2.0f), new ZombieGroupDef(85, -2, 6, 7, 2.0f),
                new ZombieGroupDef(130, 2, 6, 8, 2.1f), new ZombieGroupDef(175, -2.2f, 6, 8, 2.1f),
                new ZombieGroupDef(220, 2, 7, 9, 2.2f), new ZombieGroupDef(265, 0, 7, 10, 2.2f),
            },
            crates = new List<CrateDef> { new CrateDef(75, 3.3f, WeaponType.Shotgun, 10), new CrateDef(200, -3.3f, WeaponType.Flamethrower, 15) },
        };

        static LevelConfig Level6() => new LevelConfig
        {
            runSpeed = 6.9f, bossDistance = 420f, bossHp = 2100f, bossSpeed = 3.5f, bossStartGap = 18f, squadCap = 32,
            gates = new List<GateDef> {
                new GateDef(20, "#-20", "+3"), new GateDef(55, "#-24", "#-18"), new GateDef(95, "/2", "+4"),
                new GateDef(135, "#-28", "/2"), new GateDef(175, "#-26", "+4"), new GateDef(215, "#-30", "x1.4"),
                new GateDef(255, "/3", "#-26"), new GateDef(295, "#-28", "+3"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 6, 8, 2.1f), new ZombieGroupDef(85, -2, 6, 8, 2.1f),
                new ZombieGroupDef(130, 2, 7, 9, 2.2f), new ZombieGroupDef(175, -2, 7, 9, 2.2f),
                new ZombieGroupDef(220, 2.2f, 7, 10, 2.3f), new ZombieGroupDef(265, 0, 8, 11, 2.3f),
            },
            crates = new List<CrateDef> { new CrateDef(80, 3.3f, WeaponType.Shotgun, 11), new CrateDef(210, -3.3f, WeaponType.Flamethrower, 16) },
        };

        static LevelConfig Level7() => new LevelConfig
        {
            runSpeed = 7.0f, bossDistance = 460f, bossHp = 2350f, bossSpeed = 3.7f, bossStartGap = 17f, squadCap = 36,
            gates = new List<GateDef> {
                new GateDef(20, "#-24", "+3"), new GateDef(60, "#-28", "/2"), new GateDef(100, "#-30", "+4"),
                new GateDef(140, "/2", "#-28"), new GateDef(180, "#-32", "+3"), new GateDef(220, "x1.3", "#-30"),
                new GateDef(260, "#-34", "/2"), new GateDef(300, "#-30", "+4"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 6, 9, 2.2f), new ZombieGroupDef(85, -2, 7, 9, 2.2f),
                new ZombieGroupDef(130, 2, 7, 10, 2.3f), new ZombieGroupDef(175, -2, 8, 10, 2.3f),
                new ZombieGroupDef(220, 2, 8, 11, 2.4f), new ZombieGroupDef(270, 0, 8, 12, 2.4f),
            },
            crates = new List<CrateDef> { new CrateDef(85, 3.3f, WeaponType.Shotgun, 12), new CrateDef(220, -3.3f, WeaponType.Flamethrower, 17) },
        };

        static LevelConfig Level8() => new LevelConfig
        {
            runSpeed = 7.1f, bossDistance = 500f, bossHp = 2600f, bossSpeed = 3.9f, bossStartGap = 17f, squadCap = 40,
            gates = new List<GateDef> {
                new GateDef(20, "#-26", "/2"), new GateDef(60, "#-30", "+3"), new GateDef(100, "/2", "#-32"),
                new GateDef(140, "#-34", "+3"), new GateDef(180, "#-36", "/2"), new GateDef(220, "#-32", "x1.3"),
                new GateDef(260, "/2", "#-34"), new GateDef(300, "#-36", "+4"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 7, 10, 2.3f), new ZombieGroupDef(85, -2, 7, 10, 2.3f),
                new ZombieGroupDef(130, 2, 8, 11, 2.4f), new ZombieGroupDef(175, -2, 8, 11, 2.4f),
                new ZombieGroupDef(220, 2, 8, 12, 2.5f), new ZombieGroupDef(270, 0, 9, 13, 2.5f),
            },
            crates = new List<CrateDef> {
                new CrateDef(85, 3.3f, WeaponType.Shotgun, 13), new CrateDef(200, -3.3f, WeaponType.Flamethrower, 18), new CrateDef(300, 3.3f, WeaponType.Shotgun, 20),
            },
        };

        static LevelConfig Level9() => new LevelConfig
        {
            runSpeed = 7.2f, bossDistance = 540f, bossHp = 2850f, bossSpeed = 4.1f, bossStartGap = 16f, squadCap = 44,
            gates = new List<GateDef> {
                new GateDef(20, "#-28", "+3"), new GateDef(60, "#-32", "/2"), new GateDef(100, "#-34", "/2"),
                new GateDef(140, "/3", "#-32"), new GateDef(180, "#-38", "+3"), new GateDef(220, "#-34", "x1.2"),
                new GateDef(260, "/2", "#-38"), new GateDef(300, "#-40", "+3"), new GateDef(340, "#-36", "/2"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 7, 11, 2.4f), new ZombieGroupDef(85, -2, 8, 11, 2.4f),
                new ZombieGroupDef(130, 2, 8, 12, 2.5f), new ZombieGroupDef(175, -2, 9, 12, 2.5f),
                new ZombieGroupDef(220, 2, 9, 13, 2.6f), new ZombieGroupDef(270, 0, 9, 14, 2.6f),
            },
            crates = new List<CrateDef> {
                new CrateDef(90, 3.3f, WeaponType.Shotgun, 14), new CrateDef(210, -3.3f, WeaponType.Flamethrower, 19), new CrateDef(320, 3.3f, WeaponType.Shotgun, 22),
            },
        };

        static LevelConfig Level10() => new LevelConfig
        {
            runSpeed = 7.3f, bossDistance = 580f, bossHp = 3100f, bossSpeed = 4.3f, bossStartGap = 16f, squadCap = 48,
            gates = new List<GateDef> {
                new GateDef(20, "#-30", "/2"), new GateDef(60, "#-34", "+3"), new GateDef(100, "/2", "#-36"),
                new GateDef(140, "#-40", "/2"), new GateDef(180, "#-38", "+3"), new GateDef(220, "/3", "#-40"),
                new GateDef(260, "#-42", "x1.2"), new GateDef(300, "#-44", "/2"), new GateDef(340, "#-40", "+3"),
            },
            zombies = new List<ZombieGroupDef> {
                new ZombieGroupDef(45, 0, 8, 12, 2.5f), new ZombieGroupDef(85, -2, 8, 12, 2.5f),
                new ZombieGroupDef(125, 2, 8, 13, 2.6f), new ZombieGroupDef(165, -2, 9, 13, 2.6f),
                new ZombieGroupDef(205, 2, 9, 14, 2.7f), new ZombieGroupDef(245, 0, 9, 15, 2.7f),
                new ZombieGroupDef(290, -1.5f, 10, 16, 2.7f),
            },
            crates = new List<CrateDef> {
                new CrateDef(90, 3.3f, WeaponType.Shotgun, 15), new CrateDef(220, -3.3f, WeaponType.Flamethrower, 20), new CrateDef(340, 3.3f, WeaponType.Shotgun, 24),
            },
        };

        /// <summary>Levels past Designed reuse level 10's layout, scaled up by how far past it we are -
        /// cycling the same shape of run while gates, waves and the boss keep getting harder, uncapped.</summary>
        static LevelConfig ScaleBeyond(LevelConfig last, int extra)
        {
            float k = 1f + extra * 0.12f;
            var cfg = new LevelConfig
            {
                runSpeed = Mathf.Min(8.5f, last.runSpeed + extra * 0.05f),
                bossDistance = last.bossDistance + extra * 40f,
                bossHp = last.bossHp * k,
                bossSpeed = Mathf.Min(6.5f, last.bossSpeed + extra * 0.12f),
                bossStartGap = last.bossStartGap,
                squadCap = Mathf.Min(70, last.squadCap + extra * 3),
                gates = ScaleGates(last.gates, k),
                zombies = ScaleZombies(last.zombies, k, extra),
                crates = last.crates,
            };
            return cfg;
        }

        static List<GateDef> ScaleGates(List<GateDef> src, float k)
        {
            var list = new List<GateDef>(src.Count);
            foreach (var g in src) list.Add(new GateDef(g.distance, ScaleOp(g.left, k), ScaleOp(g.right, k)));
            return list;
        }

        // Multiplier/divide gates keep their authored factor (making those rarer/smaller is handled by the
        // hand-tuned table itself); only the additive/counter magnitudes keep scaling past level 10.
        static string ScaleOp(string s, float k)
        {
            var op = GateOp.Parse(s);
            if (op.op == 'x' || op.op == '/') return s;
            int scaled = Mathf.RoundToInt(op.n * k);
            return op.Counter ? "#" + GateOp.Format(scaled) : GateOp.Format(scaled);
        }

        static List<ZombieGroupDef> ScaleZombies(List<ZombieGroupDef> src, float k, int extra)
        {
            var list = new List<ZombieGroupDef>(src.Count);
            foreach (var z in src)
                list.Add(new ZombieGroupDef(z.distance, z.x, Mathf.Min(16, z.count + extra / 2), z.hp * k, Mathf.Min(3.2f, z.speed + extra * 0.05f)));
            return list;
        }
    }

    public static class Palette
    {
        public static readonly Color Teal = new Color32(0x1f, 0x8a, 0x8a, 255);
        public static readonly Color TealDark = new Color32(0x12, 0x5e, 0x60, 255);
        public static readonly Color Gunmetal = new Color32(0x2b, 0x30, 0x38, 255);
        public static readonly Color Orange = new Color32(0xe8, 0x79, 0x2b, 255);
        public static readonly Color Road = new Color32(0x3a, 0x3d, 0x42, 255);
        public static readonly Color Ground = new Color32(0x5a, 0x5e, 0x4c, 255);
        public static readonly Color Sky = new Color32(0x8e, 0x96, 0x90, 255);
        public static readonly Color ZombieSkin = new Color32(0x7d, 0x9a, 0x4a, 255);
        public static readonly Color ZombieShirt = new Color32(0x6b, 0x4a, 0x3a, 255);
        public static readonly Color GateGood = new Color(0.18f, 0.55f, 1f, 0.4f);
        public static readonly Color GateBad = new Color(1f, 0.23f, 0.23f, 0.4f);
    }
}
