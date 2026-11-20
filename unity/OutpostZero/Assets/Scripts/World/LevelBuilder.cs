using UnityEngine;

namespace OutpostZero
{
    /// <summary>Builds the sparse road scene and spawns gates, zombie groups and crates from a LevelConfig.</summary>
    public static class LevelBuilder
    {
        public static void Build(LevelConfig cfg, Transform root)
        {
            float len = cfg.bossDistance + 80f, hw = cfg.roadHalfWidth;
            var env = new GameObject("Environment").transform; env.SetParent(root, false);

            var road = Prims.Box(env, new Vector3(0, -0.05f, len / 2 - 10), new Vector3(hw * 2 + 1, 0.1f, len + 60), Palette.Road);
            var ground = Prims.Box(env, new Vector3(0, -0.12f, len / 2), new Vector3(200, 0.1f, len + 120), Palette.Ground);
            var lineM = Prims.Unlit(new Color(0.85f, 0.85f, 0.82f));
            foreach (var x in new[] { -hw, hw })
                Prims.Part(PrimitiveType.Cube, env, new Vector3(x, 0.005f, len / 2 - 10), new Vector3(0.15f, 0.01f, len + 60), lineM);
            var dashM = Prims.Unlit(new Color(0.85f, 0.65f, 0.19f));
            for (float z = -10; z < len; z += 6)
                Prims.Part(PrimitiveType.Cube, env, new Vector3(0, 0.006f, z), new Vector3(0.18f, 0.01f, 2.2f), dashM);
            for (float z = -8; z < len; z += 9)
                foreach (var s in new[] { -1f, 1f })
                    Prims.Box(env, new Vector3(s * (hw + 0.9f), 0.3f, z), new Vector3(0.5f, 0.6f, 3.2f), new Color(0.55f, 0.54f, 0.51f));
            for (int i = 0; i < 22; i++) // a few muted ruins far from the road
            {
                float side = i % 2 == 1 ? 1 : -1, w = 3 + (i * 7 % 5), h = 2 + (i * 13 % 7);
                Prims.Box(env, new Vector3(side * (16 + (i * 11 % 14)), h / 2, i * 22 + 10), new Vector3(w, h, 3 + (i * 3 % 4)), new Color(0.48f, 0.48f, 0.45f));
            }

            var gates = new GameObject("Gates").transform; gates.SetParent(root, false);
            foreach (var g in cfg.gates) Gate.Create(gates, g, hw);

            var zs = new GameObject("Zombies").transform; zs.SetParent(root, false);
            foreach (var grp in cfg.zombies)
                for (int i = 0; i < grp.count; i++)
                {
                    float a = i * 2.4f, r = 0.7f * Mathf.Sqrt(i);
                    var pos = new Vector3(Mathf.Clamp(grp.x + Mathf.Cos(a) * r, -hw + 0.5f, hw - 0.5f), 0, grp.distance + Mathf.Sin(a) * r);
                    Zombie.Create(zs, pos, grp.hp);
                }

            var crates = new GameObject("Crates").transform; crates.SetParent(root, false);
            foreach (var c in cfg.crates) WeaponCrate.Create(crates, c, cfg.pickupDuration);
        }
    }
}
