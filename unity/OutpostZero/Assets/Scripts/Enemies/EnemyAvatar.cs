using UnityEngine;

namespace OutpostZero
{
    /// <summary>Shared rigged-model loader for Zombie and Boss, mirroring SoldierAvatar/RookAvatar's own
    /// BuildRigged pattern: one shared material per model (zombie.png / boss.png) and a normalized height
    /// measured from the instantiated model's own bind-pose renderer bounds - the Meshy exports are life-size
    /// (~1.72m / ~1.79m), not the gameplay-tuned heights Zombie/Boss actually want.</summary>
    public static class EnemyAvatar
    {
        public class Rigged
        {
            public Transform Root;
            public SkinnedMeshRenderer Skin;
            public EnemyAnimator Anim;
        }

        static Material zombieMaterial, bossMaterial;

        /// <summary>Instantiates the model at resourcePath under parent, scaled so its own bounds.size.y
        /// equals targetHeight, with a shared crowd-safe material and a fresh EnemyAnimator wired to its
        /// clips. Returns null (and cleans up) if the prefab, its Animator or its skin is missing, so the
        /// caller can fall back to the primitive body.</summary>
        public static Rigged Build(Transform parent, string resourcePath, float targetHeight, bool isBoss)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = isBoss ? "BossBody" : "ZombieBody";
            var root = go.transform;
            root.SetParent(parent, false);

            var animator = go.GetComponentInChildren<Animator>();
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator == null || skin == null) { Object.Destroy(go); return null; }
            animator.applyRootMotion = false;
            // A crowd of 16-20 doesn't need a full skin pass for bodies off-screen or behind the camera;
            // CullUpdateTransforms still advances the clip (so it doesn't desync) but skips the mesh update.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var bounds = Prims.CombinedBounds(root);
            if (bounds == null) { Object.Destroy(go); return null; }
            root.localScale = Vector3.one * (targetHeight / Mathf.Max(0.01f, bounds.Value.size.y));

            skin.sharedMaterial = isBoss ? BossMaterial() : ZombieMaterial();
            // Mobile crowd budget: no shadows, cheapest skin quality, never update a hidden skin's mesh.
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.updateWhenOffscreen = false;
            skin.quality = SkinQuality.Bone2;

            var anim = go.AddComponent<EnemyAnimator>();
            bool built = anim.Build(animator, resourcePath);
            if (!built) Object.Destroy(anim);

            return new Rigged { Root = root, Skin = skin, Anim = built ? anim : null };
        }

        static Material ZombieMaterial()
        {
            if (zombieMaterial == null) zombieMaterial = MakeMaterial("Models/Zombie/zombie");
            return zombieMaterial;
        }

        static Material BossMaterial()
        {
            if (bossMaterial == null) bossMaterial = MakeMaterial("Models/Boss/boss");
            return bossMaterial;
        }

        static Material MakeMaterial(string texturePath)
        {
            var shader = Prims.FindShader("Standard");
            Material mat = shader != null ? new Material(shader) : Prims.Mat(Color.white, true);
            var tex = Resources.Load<Texture2D>(texturePath);
            if (tex != null && mat != null) mat.mainTexture = tex;
            if (mat != null)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            }
            return mat;
        }
    }
}
