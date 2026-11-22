using UnityEngine;

namespace OutpostZero
{
    /// <summary>Shared primitive build of Rook, used by the match and the character-select preview.</summary>
    public class RookAvatar
    {
        public Transform Root;
        public Transform Chest;
        public Vector3 ChestBase;
        public Transform ArmL;
        public Transform ArmR;
        public Transform Cape;
        public WeaponVisual Weapon;

        /// <summary>Swap point for the upcoming rigged Meshy Rook: HeroRook and CharacterStage only ever call
        /// this factory and read Root/Chest/ArmL/ArmR/Cape/Weapon plus Tick(), so a follow-up body (e.g. a
        /// branch here that loads a rigged FBX via Resources.Load and drives it with an Animator) can replace
        /// BuildPrimitive() below without touching either caller, as long as it keeps populating those same fields.</summary>
        public static RookAvatar Build(Transform parent, bool silhouette)
        {
            return BuildPrimitive(parent, silhouette);
        }

        static RookAvatar BuildPrimitive(Transform parent, bool silhouette)
        {
            var avatar = new RookAvatar();
            var go = new GameObject("Rook");
            go.transform.SetParent(parent, false);
            avatar.Root = go.transform;
            var t = avatar.Root;

            var teal = Matte(Palette.Teal);
            var tealDark = Matte(Palette.TealDark);
            var gun = Matte(new Color(0.16f, 0.17f, 0.19f));
            var orange = Matte(Palette.Orange);
            var boot = Matte(new Color(0.07f, 0.08f, 0.09f));
            var under = Matte(new Color(0.1f, 0.11f, 0.12f));

            // Stocky legs and chunky boots. Front of the avatar is +Z.
            Box(t, new Vector3(-0.12f, 0.07f, 0.03f), new Vector3(0.22f, 0.14f, 0.32f), boot);
            Box(t, new Vector3(0.12f, 0.07f, 0.03f), new Vector3(0.22f, 0.14f, 0.32f), boot);
            Box(t, new Vector3(-0.11f, 0.32f, 0f), new Vector3(0.16f, 0.36f, 0.18f), gun);
            Box(t, new Vector3(0.11f, 0.32f, 0f), new Vector3(0.16f, 0.36f, 0.18f), gun);
            Box(t, new Vector3(-0.11f, 0.4f, 0.08f), new Vector3(0.14f, 0.22f, 0.06f), teal);
            Box(t, new Vector3(0.11f, 0.4f, 0.08f), new Vector3(0.14f, 0.22f, 0.06f), teal);
            Box(t, new Vector3(-0.11f, 0.2f, 0.08f), new Vector3(0.16f, 0.1f, 0.08f), tealDark);
            Box(t, new Vector3(0.11f, 0.2f, 0.08f), new Vector3(0.16f, 0.1f, 0.08f), tealDark);

            Box(t, new Vector3(0f, 0.52f, 0f), new Vector3(0.5f, 0.1f, 0.24f), gun);
            Box(t, new Vector3(0f, 0.54f, 0.1f), new Vector3(0.1f, 0.08f, 0.04f), orange);
            Box(t, new Vector3(-0.2f, 0.5f, 0.08f), new Vector3(0.1f, 0.08f, 0.08f), gun);
            Box(t, new Vector3(0.2f, 0.5f, 0.08f), new Vector3(0.1f, 0.08f, 0.08f), gun);

            Box(t, new Vector3(0f, 0.78f, 0f), new Vector3(0.4f, 0.42f, 0.24f), under);
            var chestGo = Box(t, new Vector3(0f, 0.9f, 0.08f), new Vector3(0.36f, 0.2f, 0.08f), teal);
            avatar.Chest = chestGo.transform;
            avatar.ChestBase = avatar.Chest.localScale;
            Box(t, new Vector3(0f, 0.72f, 0.08f), new Vector3(0.3f, 0.12f, 0.07f), teal);
            Box(t, new Vector3(0f, 0.6f, 0.07f), new Vector3(0.26f, 0.1f, 0.06f), tealDark);

            var padL = new GameObject("PadL").transform;
            padL.SetParent(t, false);
            padL.localPosition = new Vector3(-0.34f, 0.98f, 0.02f);
            padL.localRotation = Quaternion.Euler(0f, 0f, 16f);
            Box(padL, Vector3.zero, new Vector3(0.26f, 0.16f, 0.3f), orange);
            var padR = new GameObject("PadR").transform;
            padR.SetParent(t, false);
            padR.localPosition = new Vector3(0.34f, 0.98f, 0.02f);
            padR.localRotation = Quaternion.Euler(0f, 0f, -16f);
            Box(padR, Vector3.zero, new Vector3(0.26f, 0.16f, 0.3f), orange);

            Box(t, new Vector3(0f, 0.78f, -0.16f), new Vector3(0.28f, 0.32f, 0.16f), gun);
            Box(t, new Vector3(-0.08f, 0.86f, -0.24f), new Vector3(0.08f, 0.08f, 0.04f), tealDark);
            Box(t, new Vector3(0.08f, 0.7f, -0.24f), new Vector3(0.1f, 0.1f, 0.04f), under);
            var mast = Prims.Part(PrimitiveType.Cylinder, t, new Vector3(0.16f, 1.12f, -0.16f), new Vector3(0.025f, 0.16f, 0.025f), gun);
            mast.transform.localRotation = Quaternion.identity;
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0.16f, 1.3f, -0.16f), Vector3.one * 0.05f, Prims.Unlit(new Color(0.9f, 0.22f, 0.12f)));

            avatar.Cape = new GameObject("CapePivot").transform;
            avatar.Cape.SetParent(t, false);
            avatar.Cape.localPosition = new Vector3(0f, 0.96f, -0.12f);
            Box(avatar.Cape, new Vector3(0f, -0.22f, -0.04f), new Vector3(0.34f, 0.42f, 0.04f), orange);

            // Angular teal helmet. No sphere. Visor faces +Z.
            Box(t, new Vector3(0f, 1.22f, 0.02f), new Vector3(0.36f, 0.34f, 0.36f), teal);
            Box(t, new Vector3(0f, 1.36f, 0.08f), new Vector3(0.34f, 0.08f, 0.22f), tealDark);
            var cheekL = Box(t, new Vector3(-0.14f, 1.14f, 0.1f), new Vector3(0.08f, 0.12f, 0.16f), teal);
            cheekL.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            var cheekR = Box(t, new Vector3(0.14f, 1.14f, 0.1f), new Vector3(0.08f, 0.12f, 0.16f), teal);
            cheekR.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            Box(t, new Vector3(0f, 1.06f, 0.1f), new Vector3(0.22f, 0.08f, 0.16f), gun);
            Box(t, new Vector3(-0.2f, 1.22f, 0f), new Vector3(0.06f, 0.1f, 0.12f), gun);
            Box(t, new Vector3(0.2f, 1.22f, 0f), new Vector3(0.06f, 0.1f, 0.12f), gun);
            var visor = new Color(0.35f, 1f, 0.98f);
            Prims.Part(PrimitiveType.Cube, t, new Vector3(0f, 1.2f, 0.2f), new Vector3(0.26f, 0.07f, 0.04f), Prims.Unlit(visor));
            var glowSrc = Prims.SharedAdditive();
            Material glowMat = glowSrc != null ? new Material(glowSrc) : Prims.Unlit(visor);
            if (glowMat != null)
            {
                glowMat.SetColor("_Color", new Color(0.3f, 1f, 0.95f, 0.55f));
                glowMat.SetColor("_TintColor", new Color(0.3f, 1f, 0.95f, 0.55f));
            }
            var visorGlow = Prims.Part(PrimitiveType.Quad, t, new Vector3(0f, 1.2f, 0.24f), new Vector3(0.32f, 0.12f, 1f), glowMat);
            visorGlow.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            avatar.ArmL = new GameObject("ArmL").transform;
            avatar.ArmL.SetParent(t, false);
            avatar.ArmL.localPosition = new Vector3(-0.4f, 0.92f, 0.04f);
            Box(avatar.ArmL, new Vector3(0f, -0.12f, 0.04f), new Vector3(0.12f, 0.2f, 0.12f), teal);
            Box(avatar.ArmL, new Vector3(0f, -0.24f, 0.06f), new Vector3(0.1f, 0.08f, 0.1f), gun);
            Box(avatar.ArmL, new Vector3(0.02f, -0.36f, 0.1f), new Vector3(0.12f, 0.18f, 0.12f), teal);
            Box(avatar.ArmL, new Vector3(0.02f, -0.48f, 0.14f), new Vector3(0.12f, 0.1f, 0.12f), boot);

            avatar.ArmR = new GameObject("ArmR").transform;
            avatar.ArmR.SetParent(t, false);
            avatar.ArmR.localPosition = new Vector3(0.4f, 0.92f, 0.06f);
            Box(avatar.ArmR, new Vector3(0f, -0.12f, 0.06f), new Vector3(0.12f, 0.2f, 0.12f), teal);
            Box(avatar.ArmR, new Vector3(0.02f, -0.24f, 0.1f), new Vector3(0.1f, 0.08f, 0.1f), gun);
            Box(avatar.ArmR, new Vector3(0.04f, -0.36f, 0.16f), new Vector3(0.12f, 0.18f, 0.12f), teal);
            Box(avatar.ArmR, new Vector3(0.04f, -0.48f, 0.22f), new Vector3(0.12f, 0.1f, 0.12f), boot);
            var hand = new GameObject("Hand").transform;
            hand.SetParent(avatar.ArmR, false);
            hand.localPosition = new Vector3(0.04f, -0.48f, 0.34f);
            avatar.Weapon = WeaponVisual.Create(hand, true, Vector3.zero);

            avatar.Root.localScale = Vector3.one * 1.05f;
            if (silhouette) TintDark(avatar.Root);
            return avatar;
        }

        public void Tick(float dt, bool running)
        {
            float sway = Mathf.Sin(Time.time * (running ? 11f : 1.7f)) * (running ? 16f : 4f);
            if (ArmL != null) ArmL.localRotation = Quaternion.Euler(sway, 0f, 8f);
            if (ArmR != null) ArmR.localRotation = Quaternion.Euler(-sway * 0.35f, 0f, -8f);
            if (Chest != null)
            {
                float breath = 1f + Mathf.Sin(Time.time * 1.6f) * (running ? 0.03f : 0.025f);
                Chest.localScale = new Vector3(ChestBase.x, ChestBase.y * breath, ChestBase.z);
            }
            if (Cape != null)
                Cape.localRotation = Quaternion.Euler(running ? Mathf.Sin(Time.time * 9f) * 16f : 0f, 0f, 0f);
            if (Weapon != null) Weapon.Tick(dt);
        }

        static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            return Prims.Part(PrimitiveType.Cube, parent, pos, scale, mat);
        }

        static Material Matte(Color c)
        {
            var m = Prims.Mat(c, true);
            if (m == null) return null;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        static void TintDark(Transform root)
        {
            var dark = Matte(new Color(0.07f, 0.08f, 0.1f));
            var rends = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].sharedMaterial = dark;
        }
    }
}
