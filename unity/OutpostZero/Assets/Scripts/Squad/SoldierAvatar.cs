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
        // read as clearly smaller than their commander instead of to-scale with him. QA screenshots at 0.825
        // showed the squad reading almost as tall as Rook once camera-distance perspective was factored in
        // (soldiers sit closer to the camera than he does), swallowing him entirely at big squad sizes -
        // dropped into the 60-65% band the brief calls for so he reads as the clearly-larger leader.
        const float HeightRatioToRook = 0.62f;
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

            // Same hand-bone socket convention as RookAvatar.BuildRigged: zero local offset, so the socket
            // just rides the hand bone's rotation exactly - HeroAnimator.LockAimPose forces that bone back
            // to a fixed forward-facing pose every frame instead of letting the Running/Run_and_Shoot clip's
            // own arm swing carry the rifle through random angles.
            var handBone = RookAvatar.FindDeep(root, "mixamorig:RightHand");
            if (handBone == null) Debug.LogWarning("[Outpost Zero] soldier.fbx has no mixamorig:RightHand bone; weapon will attach to the body root instead of the hand.");
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(handBone != null ? handBone : root, false);
            socket.localPosition = Vector3.zero;
            socket.localRotation = Quaternion.identity;

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
                avatar.Anim.LockAimPose(root,
                    RookAvatar.FindDeep(root, "mixamorig:RightArm"), RookAvatar.FindDeep(root, "mixamorig:RightForeArm"), handBone,
                    RookAvatar.FindDeep(root, "mixamorig:LeftArm"), RookAvatar.FindDeep(root, "mixamorig:LeftForeArm"), RookAvatar.FindDeep(root, "mixamorig:LeftHand"),
                    RookAvatar.FindDeep(root, "mixamorig:Spine2"));
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
                // The scene's ambient (OutpostSceneBuilder) is tuned in Gamma color space (ProjectSettings
                // m_ActiveColorSpace: 0), where bright sky-ambient plus the directional sun sum to well past
                // 1.0 on upward-facing surfaces with no linear-space falloff to soften it. The soldier
                // texture's khaki (~0.67 sRGB) clips to near-white there while Rook's darker teal/gunmetal
                // palette doesn't - same material recipe, same light, different result. A flat albedo tint
                // pulls the khaki back under the clip point without touching global lighting (which every
                // other already-reviewed scene element depends on). Glossiness/Metallic dropped near zero so
                // the rounded helmet doesn't pick up a bright specular highlight on top of that - the
                // reference art is a flat/matte stylized render, not a glossy plastic one.
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.72f, 0.7f, 0.64f));
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.04f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
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
