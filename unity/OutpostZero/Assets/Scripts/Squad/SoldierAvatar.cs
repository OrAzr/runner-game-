using UnityEngine;

namespace OutpostZero
{
    /// <summary>Visual body for one rank-and-file squad member. Tries the rigged Meshy FBX first (one shared
    /// material and import settings tuned for a crowd of 48-70, unlike Rook's single hero-grade body), falling
    /// back to the old primitive body if Resources.Load misses so a partial checkout never breaks the squad.</summary>
    public class SoldierAvatar
    {
        public Transform Root;
        public WeaponVisual Weapon;
        public HeroAnimator Anim;

        /// <summary>True for the rigged Meshy model (has its own run/idle clips), false for the code-built
        /// primitive fallback (still wants the procedural bob Soldier.Tick applies to its root).</summary>
        public bool IsRigged => Anim != null;

        // Tuned against RookAvatar's own in-gameplay height (RiggedTargetHeight), not life-size, so soldiers
        // read as clearly smaller than their commander instead of to-scale with him.
        const float HeightRatioToRook = 0.825f;
        const float TargetHeight = RookAvatar.RiggedTargetHeight * HeightRatioToRook;

        static Material sharedMaterial;

        public static SoldierAvatar Build(Transform parent)
        {
            return BuildRigged(parent) ?? BuildPrimitive(parent);
        }

        static SoldierAvatar BuildRigged(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/Soldier/soldier");
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = "Body";
            var root = go.transform;
            root.SetParent(parent, false);

            var animator = go.GetComponentInChildren<Animator>();
            var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator == null || skinned == null) { Object.Destroy(go); return null; }
            animator.applyRootMotion = false;
            // A crowd this size doesn't need a full skin pass for members off-screen or behind the camera;
            // CullUpdateTransforms still advances the clip (so it doesn't desync) but skips the mesh update.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var bounds = Prims.CombinedBounds(root);
            if (bounds == null) { Object.Destroy(go); return null; }
            root.localScale = Vector3.one * (TargetHeight / Mathf.Max(0.01f, bounds.Value.size.y));

            var mat = RiggedMaterial();
            var renderers = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = mat;
                // 48-70 of these on a phone: no shadow casting and the cheapest skin quality that still
                // reads fine at this body size.
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (renderers[i] is SkinnedMeshRenderer smr) smr.quality = SkinQuality.Bone2;
            }

            // Same hand-bone socket convention as RookAvatar.BuildRigged, so the shared rifle model rides
            // the hand's animated pose instead of the Mixamo rest-pose angle.
            var handBone = RookAvatar.FindDeep(root, "mixamorig:RightHand");
            if (handBone == null) Debug.LogWarning("[Outpost Zero] soldier.fbx has no mixamorig:RightHand bone; weapon will attach to the body root instead of the hand.");
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetPositionAndRotation((handBone != null ? handBone.position : root.position) + root.forward * 0.02f, root.rotation);
            socket.SetParent(handBone != null ? handBone : root, true);

            var avatar = new SoldierAvatar { Root = root };
            // Same body-blow-up factor as RookAvatar.BuildRigged, so the squad's rifles read at the same
            // proportions as the hero's instead of as a tiny sliver lost against the inflated rigged body.
            avatar.Weapon = WeaponVisual.Create(socket, false, Vector3.zero, RookAvatar.GameplayScale);
            var anim = go.AddComponent<HeroAnimator>();
            avatar.Anim = anim.Build(animator, "Models/Soldier/soldier") ? anim : null;
            if (avatar.Anim == null) Object.Destroy(anim);
            else
            {
                avatar.Anim.LockHipsHeight(RookAvatar.FindDeep(root, "mixamorig:Hips"));
                // Slight per-soldier speed jitter plus a randomized run-cycle start phase (see HeroAnimator.
                // Play's startTime) so a crowd of otherwise-identical rigs doesn't move in lockstep.
                avatar.Anim.Speed = Random.Range(0.92f, 1.08f);
                avatar.runStartTime = Random.Range(0f, 1f);
            }

            return avatar;
        }

        float runStartTime;

        static Material RiggedMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;
            var shader = Prims.FindShader("Standard");
            Material mat = shader != null ? new Material(shader) : Prims.Mat(Color.white, true);
            var tex = Resources.Load<Texture2D>("Models/Soldier/soldier");
            if (tex != null && mat != null) mat.mainTexture = tex;
            if (mat != null)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            }
            sharedMaterial = mat;
            return sharedMaterial;
        }

        static SoldierAvatar BuildPrimitive(Transform parent)
        {
            var avatar = new SoldierAvatar();
            var go = new GameObject("Body");
            go.transform.SetParent(parent, false);
            avatar.Root = go.transform;
            var t = avatar.Root;

            Prims.Box(t, new Vector3(0, 0.16f, 0), new Vector3(0.36f, 0.32f, 0.24f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0, 0.55f, 0), new Vector3(0.42f, 0.5f, 0.3f), Palette.Teal);
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0, 0.98f, 0), Vector3.one * 0.38f, Prims.Mat(Palette.TealDark));
            avatar.Weapon = WeaponVisual.Create(t, false, new Vector3(0.18f, 0.62f, 0.18f));
            return avatar;
        }

        /// <summary>Running while the squad is running, Idle otherwise (menu/defeat); no-op on the primitive
        /// body (Anim is null), which has no locomotion clips of its own.</summary>
        public void Tick(bool running)
        {
            Anim?.Play(running ? "Running" : "Idle", true, running ? runStartTime : 0f);
        }
    }
}
