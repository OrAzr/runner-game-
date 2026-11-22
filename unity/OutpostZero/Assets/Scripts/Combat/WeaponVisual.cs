using UnityEngine;

namespace OutpostZero
{
    /// <summary>Primitive gun models. Hero gets the full kit; soldiers get the same silhouettes, smaller.</summary>
    public class WeaponVisual : MonoBehaviour
    {
        public Transform Muzzle { get; private set; }
        GameObject rifle, shotgun, flameThrower;
        Transform pilot;
        Vector3 rest;
        float recoil;
        bool hero;

        public static WeaponVisual Create(Transform parent, bool heroKit, Vector3 localPos)
        {
            var go = new GameObject(heroKit ? "HeroWeapon" : "Weapon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (heroKit ? 1f : 0.72f);
            var v = go.AddComponent<WeaponVisual>();
            v.hero = heroKit;
            v.rest = localPos;
            v.rifle = v.BuildRifle(go.transform);
            v.shotgun = v.BuildShotgun(go.transform);
            v.flameThrower = v.BuildFlamethrower(go.transform);
            var muzzle = new GameObject("Muzzle");
            v.Muzzle = muzzle.transform;
            v.Muzzle.SetParent(go.transform, false);
            v.Muzzle.localRotation = Quaternion.identity;
            v.Show(WeaponType.AssaultRifle);
            return v;
        }

        public void Show(WeaponType type)
        {
            if (rifle != null) rifle.SetActive(type == WeaponType.AssaultRifle);
            if (shotgun != null) shotgun.SetActive(type == WeaponType.Shotgun);
            if (flameThrower != null) flameThrower.SetActive(type == WeaponType.Flamethrower);
            float z = type == WeaponType.Shotgun ? 0.52f : type == WeaponType.Flamethrower ? 0.7f : 0.58f;
            if (Muzzle != null) Muzzle.localPosition = new Vector3(0f, 0.02f, z);
        }

        public void Kick(float amount) { recoil = Mathf.Max(recoil, amount); }

        public void Tick(float dt)
        {
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 9f);
            transform.localPosition = rest + new Vector3(0f, 0f, -recoil * 0.07f);
            bool shot = shotgun != null && shotgun.activeSelf;
            float pop = shot ? 1f + recoil * 0.16f : 1f;
            transform.localScale = new Vector3(pop, pop, 1f) * (hero ? 1f : 0.72f);
            if (pilot != null && flameThrower != null && flameThrower.activeSelf)
            {
                float p = 0.05f + 0.02f * Mathf.Abs(Mathf.Sin(Time.time * 28f));
                pilot.localScale = new Vector3(p, p, p);
            }
        }

        static readonly Color Steel = new Color(0.1f, 0.11f, 0.13f);
        static Material Dark() { return Prims.Mat(Steel); }
        static Material Metal() { return Prims.Mat(Palette.Gunmetal); }

        const float RifleModelLength = 0.58f, ShotgunModelLength = 0.52f, FlamethrowerModelLength = 0.6f;

        static GameObject Barrel(Transform parent, Vector3 pos, float radius, float length, Material mat)
        {
            var go = Prims.Part(PrimitiveType.Cylinder, parent, pos, new Vector3(radius, length * 0.5f, radius), mat);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            return go;
        }

        GameObject BuildRifle(Transform root)
        {
            var model = WeaponModels.TryBuild("rifle", root, RifleModelLength, out _);
            if (model != null) return model;

            var g = new GameObject("Rifle"); g.transform.SetParent(root, false);
            var t = g.transform;
            Prims.Box(t, new Vector3(0f, 0f, -0.08f), new Vector3(0.07f, 0.08f, 0.22f), Steel);
            Barrel(t, new Vector3(0f, 0.02f, 0.28f), 0.025f, 0.46f, Dark());
            Prims.Box(t, new Vector3(0f, -0.06f, 0.02f), new Vector3(0.05f, 0.1f, 0.06f), Palette.Gunmetal);
            if (hero) Prims.Box(t, new Vector3(0f, 0.05f, 0.12f), new Vector3(0.02f, 0.04f, 0.08f), Palette.Teal);
            return g;
        }

        GameObject BuildShotgun(Transform root)
        {
            var model = WeaponModels.TryBuild("shotgun", root, ShotgunModelLength, out _);
            if (model != null) return model;

            var g = new GameObject("Shotgun"); g.transform.SetParent(root, false);
            var t = g.transform;
            Prims.Box(t, new Vector3(0f, 0f, -0.1f), new Vector3(0.08f, 0.09f, 0.2f), Palette.Orange);
            Barrel(t, new Vector3(0f, 0.03f, 0.22f), 0.04f, 0.4f, Dark());
            Barrel(t, new Vector3(0f, 0.0f, 0.2f), 0.028f, 0.36f, Metal());
            Prims.Box(t, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.07f, 0.05f, 0.1f), Palette.Orange);
            return g;
        }

        GameObject BuildFlamethrower(Transform root)
        {
            var model = WeaponModels.TryBuild("flamethrower", root, FlamethrowerModelLength, out var tip);
            if (model != null)
            {
                var glow = Prims.Part(PrimitiveType.Sphere, root, tip, Vector3.one * 0.06f, Prims.Unlit(new Color(1f, 0.72f, 0.2f)));
                pilot = glow.transform;
                return model;
            }

            var g = new GameObject("Flamethrower"); g.transform.SetParent(root, false);
            var t = g.transform;
            Prims.Box(t, new Vector3(0f, 0f, -0.02f), new Vector3(0.1f, 0.1f, 0.22f), Palette.Gunmetal);
            Barrel(t, new Vector3(0f, 0.02f, 0.32f), 0.045f, 0.5f, Dark());
            Barrel(t, new Vector3(-0.1f, -0.02f, -0.02f), 0.07f, 0.22f, Metal());
            Prims.Box(t, new Vector3(-0.06f, 0.0f, 0.08f), new Vector3(0.04f, 0.04f, 0.16f), Palette.Orange);
            var light = Prims.Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.02f, 0.56f), Vector3.one * 0.06f, Prims.Unlit(new Color(1f, 0.72f, 0.2f)));
            pilot = light.transform;
            return g;
        }
    }
}
