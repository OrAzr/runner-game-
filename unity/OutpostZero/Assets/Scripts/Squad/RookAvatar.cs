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
        public WeaponVisual Weapon;

        public static RookAvatar Build(Transform parent, bool silhouette)
        {
            var avatar = new RookAvatar();
            var go = new GameObject("Rook");
            go.transform.SetParent(parent, false);
            avatar.Root = go.transform;
            var t = avatar.Root;
            var boot = new Color(0.08f, 0.09f, 0.1f);
            var cloth = new Color(0.16f, 0.2f, 0.22f);
            Prims.Box(t, new Vector3(-0.1f, 0.06f, 0.02f), new Vector3(0.16f, 0.1f, 0.28f), boot);
            Prims.Box(t, new Vector3(0.1f, 0.06f, 0.02f), new Vector3(0.16f, 0.1f, 0.28f), boot);
            Prims.Box(t, new Vector3(-0.1f, 0.28f, 0f), new Vector3(0.14f, 0.32f, 0.16f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0.1f, 0.28f, 0f), new Vector3(0.14f, 0.32f, 0.16f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.08f, 0.22f), Palette.Orange);
            var chestGo = Prims.Box(t, new Vector3(0f, 0.74f, 0f), new Vector3(0.42f, 0.42f, 0.26f), Palette.Teal);
            avatar.Chest = chestGo.transform;
            avatar.ChestBase = avatar.Chest.localScale;
            Prims.Box(t, new Vector3(0f, 0.76f, 0.12f), new Vector3(0.3f, 0.28f, 0.06f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0f, 0.78f, 0.16f), new Vector3(0.08f, 0.08f, 0.03f), Palette.Teal);
            Prims.Box(t, new Vector3(-0.28f, 0.9f, 0f), new Vector3(0.22f, 0.1f, 0.28f), Palette.Orange);
            Prims.Box(t, new Vector3(0.28f, 0.9f, 0f), new Vector3(0.22f, 0.1f, 0.28f), Palette.Orange);
            Prims.Box(t, new Vector3(0f, 0.7f, -0.18f), new Vector3(0.28f, 0.32f, 0.14f), cloth);
            Prims.Box(t, new Vector3(0.02f, 0.42f, -0.16f), new Vector3(0.16f, 0.46f, 0.03f), Palette.TealDark);
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.12f, 0f), Vector3.one * 0.32f, Prims.Mat(Palette.TealDark));
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.16f, 0.02f), new Vector3(0.38f, 0.34f, 0.38f), Prims.Mat(Palette.Gunmetal));
            Prims.Box(t, new Vector3(0f, 1.14f, 0.16f), new Vector3(0.24f, 0.08f, 0.06f), new Color(0.35f, 0.92f, 0.9f));
            Prims.Part(PrimitiveType.Cylinder, t, new Vector3(0.1f, 1.4f, -0.04f), new Vector3(0.02f, 0.12f, 0.02f), Prims.Mat(Palette.Gunmetal));
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0.1f, 1.54f, -0.04f), Vector3.one * 0.06f, Prims.Unlit(Palette.Orange));

            avatar.ArmL = new GameObject("ArmL").transform;
            avatar.ArmL.SetParent(t, false);
            avatar.ArmL.localPosition = new Vector3(-0.32f, 0.9f, 0.04f);
            Prims.Box(avatar.ArmL, new Vector3(-0.02f, -0.16f, 0.08f), new Vector3(0.12f, 0.28f, 0.12f), Palette.Teal);
            Prims.Box(avatar.ArmL, new Vector3(0.02f, -0.34f, 0.16f), new Vector3(0.1f, 0.22f, 0.1f), Palette.TealDark);

            avatar.ArmR = new GameObject("ArmR").transform;
            avatar.ArmR.SetParent(t, false);
            avatar.ArmR.localPosition = new Vector3(0.3f, 0.88f, 0.06f);
            Prims.Box(avatar.ArmR, new Vector3(0.04f, -0.14f, 0.1f), new Vector3(0.12f, 0.26f, 0.12f), Palette.Teal);
            Prims.Box(avatar.ArmR, new Vector3(0.08f, -0.3f, 0.22f), new Vector3(0.1f, 0.2f, 0.1f), Palette.TealDark);
            var hand = new GameObject("Hand").transform;
            hand.SetParent(avatar.ArmR, false);
            hand.localPosition = new Vector3(0.08f, -0.4f, 0.34f);
            avatar.Weapon = WeaponVisual.Create(hand, true, Vector3.zero);
            avatar.Root.localScale = Vector3.one * 1.15f;
            if (silhouette) TintDark(avatar.Root);
            return avatar;
        }

        static void TintDark(Transform root)
        {
            var dark = Prims.Mat(new Color(0.07f, 0.08f, 0.1f));
            var rends = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].sharedMaterial = dark;
        }
    }
}
