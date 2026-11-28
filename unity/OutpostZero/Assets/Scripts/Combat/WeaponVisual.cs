using UnityEngine;

namespace OutpostZero
{
    /// <summary>Primitive gun models. Hero gets the full kit; soldiers get the same silhouettes, smaller.</summary>
    // After HeroAnimator poses the hands (default order 0), snap the weapon itself to world-forward
    // so body yaw or a twisted hand socket cannot aim the barrel sideways.
    [DefaultExecutionOrder(1000)]
    public class WeaponVisual : MonoBehaviour
    {
        public Transform Muzzle { get; private set; }

        /// <summary>When set, LateUpdate locks the weapon to this transform's flattened forward instead of
        /// always world +Z. CharacterStage points this at the preview body root so the gun follows Rook's
        /// own facing on the spinning turntable instead of always aiming at the stage camera.</summary>
        public Transform AimReference;

        GameObject rifle, shotgun, flameThrower;
        Transform pilot;
        Vector3 rest;
        float recoil;
        bool hero;

        // Muzzle offsets in this component's local space for each real model, captured once at build time
        // from the asset's own Muzzle mount point (see WeaponModels.TryBuild). These defaults are only used
        // by the primitive fallback guns, which have no Muzzle empty to measure.
        Vector3 rifleMuzzleLocal = new Vector3(0f, 0.02f, 0.58f);
        Vector3 shotgunMuzzleLocal = new Vector3(0f, 0.02f, 0.52f);
        Vector3 flameMuzzleLocal = new Vector3(0f, 0.02f, 0.7f);

        /// <summary>worldScale is the body's own blow-up factor (RookAvatar.GameplayScale for a rigged body,
        /// 1 for the life-size primitive fallback). The rigged model's target lengths below were tuned as
        /// absolute world meters before GameplayScale existed, so without this the gun reads as a tiny sliver
        /// in the oversized rigged hand - this scales the target length up to match whichever body it rides.</summary>
        public static WeaponVisual Create(Transform parent, bool heroKit, Vector3 localPos, float worldScale = 1f)
        {
            var go = new GameObject(heroKit ? "HeroWeapon" : "Weapon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (heroKit ? 1f : 0.72f);
            var v = go.AddComponent<WeaponVisual>();
            v.hero = heroKit;
            v.rest = localPos;
            v.rifle = v.BuildRifle(go.transform, worldScale);
            v.shotgun = v.BuildShotgun(go.transform, worldScale);
            v.flameThrower = v.BuildFlamethrower(go.transform, worldScale);
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
            Vector3 local = type == WeaponType.Shotgun ? shotgunMuzzleLocal : type == WeaponType.Flamethrower ? flameMuzzleLocal : rifleMuzzleLocal;
            if (Muzzle != null) Muzzle.localPosition = local;
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

        // After the clip has posed the hand, force this weapon (hero and soldiers) to point
        // level along a fixed aim basis - world +Z by default, or AimReference's own flattened
        // forward when one is set - so body yaw or a twisted hand socket cannot aim the barrel
        // sideways. The grip stays in the hand because only rotation is overwritten; local
        // position is left alone. The child mesh is already oriented so its barrel follows
        // this transform's +Z.
        void LateUpdate()
        {
            Vector3 fwd = Vector3.forward;
            if (AimReference != null)
            {
                fwd = AimReference.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            }
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        GameObject BuildRifle(Transform root, float worldScale)
        {
            var model = WeaponModels.TryBuild(WeaponDefinition.Get(WeaponType.AssaultRifle), root, RifleModelLength * worldScale, out var mounts);
            if (model != null)
            {
                if (mounts.Muzzle != null) rifleMuzzleLocal = root.InverseTransformPoint(mounts.Muzzle.position);
                return model;
            }

            var g = new GameObject("Rifle"); g.transform.SetParent(root, false);
            var t = g.transform;
            t.localScale = Vector3.one * worldScale;
            Prims.Box(t, new Vector3(0f, 0f, -0.08f), new Vector3(0.07f, 0.08f, 0.22f), Steel);
            Barrel(t, new Vector3(0f, 0.02f, 0.28f), 0.025f, 0.46f, Dark());
            Prims.Box(t, new Vector3(0f, -0.06f, 0.02f), new Vector3(0.05f, 0.1f, 0.06f), Palette.Gunmetal);
            if (hero) Prims.Box(t, new Vector3(0f, 0.05f, 0.12f), new Vector3(0.02f, 0.04f, 0.08f), Palette.Teal);
            return g;
        }

        GameObject BuildShotgun(Transform root, float worldScale)
        {
            var model = WeaponModels.TryBuild(WeaponDefinition.Get(WeaponType.Shotgun), root, ShotgunModelLength * worldScale, out var mounts);
            if (model != null)
            {
                if (mounts.Muzzle != null) shotgunMuzzleLocal = root.InverseTransformPoint(mounts.Muzzle.position);
                return model;
            }

            var g = new GameObject("Shotgun"); g.transform.SetParent(root, false);
            var t = g.transform;
            t.localScale = Vector3.one * worldScale;
            Prims.Box(t, new Vector3(0f, 0f, -0.1f), new Vector3(0.08f, 0.09f, 0.2f), Palette.Orange);
            Barrel(t, new Vector3(0f, 0.03f, 0.22f), 0.04f, 0.4f, Dark());
            Barrel(t, new Vector3(0f, 0.0f, 0.2f), 0.028f, 0.36f, Metal());
            Prims.Box(t, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.07f, 0.05f, 0.1f), Palette.Orange);
            return g;
        }

        GameObject BuildFlamethrower(Transform root, float worldScale)
        {
            var model = WeaponModels.TryBuild(WeaponDefinition.Get(WeaponType.Flamethrower), root, FlamethrowerModelLength * worldScale, out var mounts);
            if (model != null)
            {
                if (mounts.Muzzle != null) flameMuzzleLocal = root.InverseTransformPoint(mounts.Muzzle.position);
                var glow = Prims.Part(PrimitiveType.Sphere, root, flameMuzzleLocal, Vector3.one * 0.06f, Prims.Unlit(new Color(1f, 0.72f, 0.2f)));
                pilot = glow.transform;
                return model;
            }

            var g = new GameObject("Flamethrower"); g.transform.SetParent(root, false);
            var t = g.transform;
            t.localScale = Vector3.one * worldScale;
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
