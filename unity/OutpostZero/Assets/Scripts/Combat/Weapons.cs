using UnityEngine;

namespace OutpostZero
{
    public enum WeaponType { AssaultRifle, Shotgun, Flamethrower }

    /// <summary>Stats for squad weapons. The rifle is Rook's signature gun; the others are timed field pickups.</summary>
    public struct WeaponDef
    {
        public string label; public float rate, damage, range, speed, spread, dps, cone; public int pellets;
        public Sfx sfx; public Color tracer;

        public static WeaponDef Get(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Shotgun:
                    return new WeaponDef { label = "SHOTGUN", rate = 0.9f, damage = 1.3f, range = 14, speed = 32, pellets = 5, spread = 0.32f, sfx = Sfx.Shotgun, tracer = new Color(1f, 0.63f, 0.25f) };
                case WeaponType.Flamethrower:
                    return new WeaponDef { label = "FLAMETHROWER", dps = 2.2f, range = 8, cone = 0.55f, sfx = Sfx.FlameLoop, tracer = new Color(1f, 0.5f, 0.1f) };
                default:
                    return new WeaponDef { label = "ASSAULT RIFLE", rate = 2.2f, damage = 1, range = 24, speed = 38, pellets = 1, spread = 0.02f, sfx = Sfx.Rifle, tracer = new Color(1f, 0.83f, 0.42f) };
            }
        }
    }
}
