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
        /// <summary>Set for the rigged model only (chest-anchored weapon); the primitive fallback keeps using
        /// Weapon instead, since it has no bones for WeaponMount to anchor against.</summary>
        public WeaponMount Mount;
        public HeroAnimator Anim;
        public ArmIk Ik;

        // Procedural flinch for squad-loss hits: a quick backward torso tilt layered on top of the normal
        // run/shoot pose instead of the old TriggerOnce("Gunshot") full-body reaction clip, which locked out
        // Play() for its whole ~2-2.5s length (arms up, leaning back, stops shooting) and read as the squad
        // slowing into slow motion on every single hit. FlinchPitch rises and falls smoothly over
        // FlinchDuration (see Tick), so it never snaps, and Cooldown stops repeated hits from restacking it.
        const float FlinchDuration = 0.22f;
        const float FlinchCooldown = 0.35f;
        float flinchT, flinchCooldownUntil;

        /// <summary>0 at rest, peaking mid-flinch, back to 0 by FlinchDuration - always a smooth rise and
        /// fall so a one-shot always blends back instead of snapping.</summary>
        public float FlinchPitch { get; private set; }

        /// <summary>True for the rigged Meshy model (which has its own run cycle), false for the code-built
        /// primitive fallback (which has no locomotion animation of its own and still wants the procedural bob).</summary>
        public bool IsRigged => Anim != null;

        /// <summary>Single knob for how large the rigged hero reads in gameplay. 1x used to match the old
        /// primitive body's own height so the swap wouldn't change gameplay framing; device testing showed
        /// that "to scale" hero read as barely bigger than the squad grunts and tiny next to gates, so this
        /// is boosted well past life-size on purpose. CharacterStage's select-screen preview re-frames its
        /// camera from the model's own bounds (see CharacterStage.FrameHero), so it isn't affected by this.</summary>
        public const float GameplayScale = 2.5f;
        public const float BaseRiggedHeight = 1.47f; // primitive body's own height (1.05 scale * ~1.4m)
        /// <summary>Rook's actual in-gameplay world height when the rigged model loads. SoldierAvatar sizes
        /// the squad relative to this so soldiers read as clearly smaller than their commander.</summary>
        public const float RiggedTargetHeight = BaseRiggedHeight * GameplayScale;

        /// <summary>Rook's actual built height (RiggedTargetHeight for the rigged model, BaseRiggedHeight for
        /// the primitive fallback) - SquadController reads this to float the squad-count badge above his head
        /// instead of a fixed offset that only matched one of the two body sizes.</summary>
        public float Height { get; private set; }

        /// <summary>HeroRook and CharacterStage only ever call this factory and read Root/Chest/ArmL/ArmR/
        /// Cape/Weapon/Anim plus Tick(). Tries the rigged Meshy FBX first and falls back to the primitive body
        /// if Resources.Load misses or the model is missing an Animator/mesh, so a partial checkout never
        /// breaks the hero.</summary>
        public static RookAvatar Build(Transform parent, bool silhouette)
        {
            return BuildRigged(parent, silhouette) ?? BuildPrimitive(parent, silhouette);
        }

        static RookAvatar BuildRigged(Transform parent, bool silhouette)
        {
            var prefab = Resources.Load<GameObject>("Models/Hero/rook");
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = "Rook";
            var root = go.transform;
            root.SetParent(parent, false);

            var animator = go.GetComponentInChildren<Animator>();
            var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator == null || skinned == null) { Object.Destroy(go); return null; }
            // HeroAnimator drives clips through its own PlayableGraph rather than an AnimatorController, so
            // nothing calls OnAnimatorMove to consume root motion anyway; this just makes sure the Animator
            // never nudges the transform on its own if that ever changes.
            animator.applyRootMotion = false;

            var bounds = Prims.CombinedBounds(root);
            if (bounds == null) { Object.Destroy(go); return null; }
            root.localScale = Vector3.one * (RiggedTargetHeight / Mathf.Max(0.01f, bounds.Value.size.y));
            Debug.Log("[SCALE] rook height=" + (bounds.Value.size.y * root.localScale.y));

            var mat = RiggedMaterial();
            var renderers = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = mat;

            // LockAimPose no longer overrides these for Rook (lockArms: false below) - ArmIk drives them
            // onto the weapon's actual grip points instead - but they're still looked up here since ArmIk
            // needs the same bones, and LockAimPose still needs the hand bone reference for its signature.
            var rightUpper = FindDeep(root, "mixamorig:RightArm");
            var rightFore = FindDeep(root, "mixamorig:RightForeArm");
            var handBone = FindDeep(root, "mixamorig:RightHand");
            if (handBone == null) Debug.LogWarning("[Outpost Zero] rook.fbx has no mixamorig:RightHand bone; LockAimPose will skip the right arm.");
            var leftUpper = FindDeep(root, "mixamorig:LeftArm");
            var leftFore = FindDeep(root, "mixamorig:LeftForeArm");
            var leftHand = FindDeep(root, "mixamorig:LeftHand");
            var spine2Bone = FindDeep(root, "mixamorig:Spine2");
            if (spine2Bone == null) Debug.LogWarning("[Outpost Zero] rook.fbx has no mixamorig:Spine2 bone; the weapon mount will anchor to the model root instead of the chest.");

            var headBone = FindDeep(root, "mixamorig:Head");
            if (headBone != null)
            {
                var glow = Prims.Part(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.05f, Prims.Unlit(new Color(0.3f, 1f, 0.95f)));
                glow.transform.position = headBone.position + root.forward * 0.11f + root.up * 0.03f;
                glow.transform.SetParent(headBone, true);
            }

            var avatar = new RookAvatar { Root = root, Height = RiggedTargetHeight };
            avatar.Mount = WeaponMount.Create(root, root, spine2Bone, RiggedTargetHeight);
            avatar.Ik = ArmIk.Create(go, avatar.Mount, root,
                rightUpper, rightFore, handBone,
                leftUpper, leftFore, leftHand);
            var anim = go.AddComponent<HeroAnimator>();
            avatar.Anim = anim.Build(animator, "Models/Hero/rook") ? anim : null;
            if (avatar.Anim == null) Object.Destroy(anim);
            else
            {
                avatar.Anim.LockHipsHeight(FindDeep(root, "mixamorig:Hips"));
                avatar.Anim.LockAimPose(root,
                    rightUpper, rightFore, handBone,
                    leftUpper, leftFore, leftHand,
                    spine2Bone,
                    // Rook-only: straightens the running clip's forward stoop (SoldierAvatar doesn't pass these).
                    FindDeep(root, "mixamorig:Spine"), FindDeep(root, "mixamorig:Spine1"),
                    lockArms: false);
            }

            if (silhouette) TintDark(root);
            return avatar;
        }

        /// <summary>Depth-first bone lookup by exact name, shared with SoldierAvatar for the same
        /// Mixamo-rig hand/hips attach points.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static Material RiggedMaterial()
        {
            var shader = Prims.FindShader("Standard");
            Material mat = shader != null ? new Material(shader) : Prims.Mat(Color.white, true);
            var tex = Resources.Load<Texture2D>("Models/Hero/rook");
            if (tex != null && mat != null) mat.mainTexture = tex;
            if (mat != null)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            }
            return mat;
        }

        static RookAvatar BuildPrimitive(Transform parent, bool silhouette)
        {
            var avatar = new RookAvatar { Height = BaseRiggedHeight };
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

        public void Tick(float dt, bool moving)
        {
            if (flinchT > 0f)
            {
                flinchT = Mathf.Max(0f, flinchT - dt);
                float phase = 1f - flinchT / FlinchDuration;
                FlinchPitch = Mathf.Sin(phase * Mathf.PI);
            }
            else FlinchPitch = 0f;

            float sway = Mathf.Sin(Time.time * (moving ? 11f : 1.7f)) * (moving ? 16f : 4f);
            if (ArmL != null) ArmL.localRotation = Quaternion.Euler(sway, 0f, 8f);
            if (ArmR != null) ArmR.localRotation = Quaternion.Euler(-sway * 0.35f, 0f, -8f);
            if (Chest != null)
            {
                float breath = 1f + Mathf.Sin(Time.time * 1.6f) * (moving ? 0.03f : 0.025f);
                Chest.localScale = new Vector3(ChestBase.x, ChestBase.y * breath, ChestBase.z);
            }
            if (Cape != null)
                Cape.localRotation = Quaternion.Euler(moving ? Mathf.Sin(Time.time * 9f) * 16f : 0f, 0f, 0f);
            if (Mount != null) Mount.Tick(dt);
            else if (Weapon != null) Weapon.Tick(dt);
            // The squad is effectively always firing while moving (AutoShooter has no "holding fire" state),
            // so Run_and_Shoot covers both; Running is loaded and available for a future non-firing state.
            // Idle plays whenever the squad isn't actually advancing (parked at the boss, menu, defeat) -
            // LockAimPose/ArmIk aren't keyed to the clip name, so the rifle still stays held and aimed
            // forward through Idle exactly as it does through Run_and_Shoot.
            if (Anim != null) Anim.Play(moving ? "Run_and_Shoot" : "Idle", true);
        }

        /// <summary>Short flinch for a squad-loss hit: keeps running and shooting, just a quick recoil.
        /// Gated by FlinchCooldown so a run of hits doesn't restack the tilt into something bigger.</summary>
        public void PlayHitReaction()
        {
            if (Time.time < flinchCooldownUntil) return;
            flinchCooldownUntil = Time.time + FlinchCooldown;
            flinchT = FlinchDuration;
        }

        /// <summary>One-shot death clip. No-op on the primitive body (Anim is null) or if the clip is missing.
        /// Also eases the arm IK's weight to 0, so the hands let go of the grips instead of staying rifle-
        /// locked through the death pose.</summary>
        public void PlayDeath()
        {
            Anim?.TriggerOnce("Dead");
            Ik?.ReleaseArms();
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
