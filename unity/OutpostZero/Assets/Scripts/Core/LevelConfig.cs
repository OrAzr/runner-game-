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
    public class ZombieGroupDef { public float distance; public float x; public int count; public float hp;
        public ZombieGroupDef(float d, float x, int n, float hp) { distance = d; this.x = x; count = n; this.hp = hp; } }

    [Serializable]
    public class CrateDef { public float distance; public float x; public WeaponType weapon; public float hp;
        public CrateDef(float d, float x, WeaponType w, float hp) { distance = d; this.x = x; weapon = w; this.hp = hp; } }

    [Serializable]
    public class LevelConfig
    {
        public float runSpeed = 6.5f;
        public float bossDistance = 400f;
        public int startCount = 1;           // the run starts with Rook alone
        public float roadHalfWidth = 5f;
        public float bossHp = 2200f;
        public float bossSpeed = 1.1f;
        public float bossStartGap = 30f;
        public float pickupDuration = 10f;

        public List<GateDef> gates = new List<GateDef> {
            new GateDef(28, "+5", "+3"),  new GateDef(75, "x2", "-4"),   new GateDef(130, "+8", "x2"),
            new GateDef(190, "-10", "+15"), new GateDef(255, "+10", "-5"), new GateDef(320, "/2", "+20"),
            new GateDef(365, "x2", "+12"),
        };
        public List<ZombieGroupDef> zombies = new List<ZombieGroupDef> {
            new ZombieGroupDef(55, 0, 3, 3), new ZombieGroupDef(105, -2, 5, 4), new ZombieGroupDef(160, 1.5f, 6, 5),
            new ZombieGroupDef(225, 0, 8, 6), new ZombieGroupDef(290, -1.5f, 9, 8), new ZombieGroupDef(345, 1, 10, 10),
        };
        public List<CrateDef> crates = new List<CrateDef> {
            new CrateDef(95, 2.6f, WeaponType.Shotgun, 6), new CrateDef(210, -2.6f, WeaponType.Flamethrower, 10),
            new CrateDef(300, 2.6f, WeaponType.Shotgun, 14),
        };
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
