using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace OutpostZero
{
    /// <summary>Drives the rigged Rook's clips with a hand-rolled PlayableGraph instead of an authored
    /// AnimatorController asset. Every clip gets its own fixed mixer input created once in Build(); Play()
    /// just retargets which input is trending to weight 1, and Update() advances each active/fading slot's
    /// own time (looping it manually, since looping is not guaranteed to survive import) and crossfades the
    /// weights with a simple MoveTowards.</summary>
    public class HeroAnimator : MonoBehaviour
    {
        const float BlendSeconds = 0.18f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clipPlayables;
        AnimationClip[] clipList;
        float[] localTime;
        bool[] loopFlag;
        float[] weight;
        int activeIndex = -1;
        float lockUntil;

        Transform hips;
        Vector3 hipsLockedXZ;
        float hipsSmoothedY;

        Transform aimRoot, rightArm, rightForeArm, rightHand, leftArm, leftForeArm, leftHand, spine2, spine, spine1;

        // Aim-pose offsets are Euler angles applied on top of aimRoot.rotation (the character's own facing),
        // not the bones' own rig-local axes - those point wherever the FBX exporter happened to put them,
        // but aimRoot's +Z-forward/+Y-up/+X-right is the one convention already established project-wide
        // (see the weapon socket in RookAvatar/SoldierAvatar.BuildRigged). Pitch sign comes from Unity's
        // left-handed Euler.X: positive pitches +Z (forward) down towards -Y, so negative values raise an
        // arm from a down-at-the-side rest towards up-and-forward.
        static readonly Quaternion RightArmAim = Quaternion.Euler(-65f, -8f, 8f);
        static readonly Quaternion RightForeArmAim = Quaternion.Euler(-18f, 0f, 0f);
        static readonly Quaternion RightHandAim = Quaternion.identity; // zero offset: hand's local +Z stays exactly aimRoot.forward, so the muzzle is always level and forward regardless of the other bones' pose.
        static readonly Quaternion LeftArmAim = Quaternion.Euler(-65f, 8f, -8f);
        static readonly Quaternion LeftForeArmAim = Quaternion.Euler(-18f, 0f, 0f);
        static readonly Quaternion LeftHandAim = Quaternion.identity;
        static readonly Quaternion Spine2Aim = Quaternion.Euler(4f, 0f, 0f);
        // Rook's running clip carries a heavy forward stoop through the lower/mid spine (a Mixamo combat-run
        // trait); Spine/Spine1 get the same partial-Slerp treatment as Spine2 but pulled all the way to
        // aimRoot's own zero-pitch orientation (no small forward offset) and blended harder, since this is
        // specifically correcting a hunch rather than just damping a swing. Rook-only - never wired up for
        // soldiers, who don't have this complaint.

        /// <summary>Loads every clip under the given Resources path (e.g. "Models/Hero/rook" or
        /// "Models/Soldier/soldier") and wires one mixer input per clip. Returns false (nothing built) if the
        /// resource folder has no clips, so the caller can skip this component entirely.</summary>
        public bool Build(Animator animator, string resourcePath)
        {
            if (animator == null) return false;
            clipList = Resources.LoadAll<AnimationClip>(resourcePath);
            if (clipList == null || clipList.Length == 0) return false;

            graph = PlayableGraph.Create("RookAnimator");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "RookOutput", animator);
            mixer = AnimationMixerPlayable.Create(graph, clipList.Length);
            output.SetSourcePlayable(mixer);

            clipPlayables = new AnimationClipPlayable[clipList.Length];
            localTime = new float[clipList.Length];
            loopFlag = new bool[clipList.Length];
            weight = new float[clipList.Length];
            for (int i = 0; i < clipList.Length; i++)
            {
                clipPlayables[i] = AnimationClipPlayable.Create(graph, clipList[i]);
                clipPlayables[i].SetApplyFootIK(false);
                graph.Connect(clipPlayables[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }
            graph.Play();
            // Starts already fully weighted onto one clip (Idle if present, else whatever loaded first)
            // instead of leaving every mixer input at 0 until the first real Play() call ramps one in.
            // That all-zero-weight gap was briefly visible after a QaShotRunner restart: the restart's
            // eager HeroAnimator.LockAimPose->ApplyAimLock call corrected the hand bone before anything
            // else ran, but AnimationMixerPlayable's own treatment of an all-zero-weight pose (effectively
            // "no clip contributing") clobbered it again the moment the real Play() call ramped the first
            // clip's weight up from 0 - a transient that normal mid-game blends between two already
            // nonzero-weighted clips never hit.
            int startIdx = FindIndex("Idle");
            if (startIdx < 0) startIdx = 0;
            activeIndex = startIdx;
            weight[startIdx] = 1f;
            mixer.SetInputWeight(startIdx, 1f);
            return true;
        }

        /// <summary>The Mixamo running clips also bake forward travel straight into mixamorig:Hips' local X/Z
        /// (there's no separate root node to strip at import time, though HeroModelImport now extracts it there
        /// too - see that file), so the rig would otherwise slide forward across every loop and snap back the
        /// instant the clip wraps to time 0, on top of SquadController's own code-driven position. Pinning
        /// Hips' local X/Z to its value right after Build() (bind pose, before any clip has been evaluated)
        /// removes that slide while leaving every rotation - so leg/arm/torso motion - untouched. Y is only
        /// damped (not hard-locked): PR #13 pinned Y to this same bind sample and that fought the Run_and_Shoot
        /// clip's own (lower, crouched-run) hip height, driving the knees/ankles through the floor as a
        /// backward-leaning squat. Smoothing towards the clip's own Y keeps the stride looking grounded instead.</summary>
        public void LockHipsHeight(Transform hipsBone)
        {
            hips = hipsBone;
            if (hips == null) return;
            var p = hips.localPosition;
            hipsLockedXZ = new Vector3(p.x, 0f, p.z);
            hipsSmoothedY = p.y;
        }

        /// <summary>Freezes the shoulder/elbow/hand chain to a fixed rifle-aim pose every frame instead of
        /// letting the Running/Run_and_Shoot clip's own arm-swing drive it. Those clips were authored
        /// empty-handed, so the swing that reads fine without a prop sends an attached rifle through random
        /// angles once one is bolted onto the hand bone. Any bone left null (e.g. a model missing Spine2) is
        /// just skipped. Legs/hips/torso keep playing the clip normally - only this chain is overridden.</summary>
        public void LockAimPose(Transform root, Transform rightArm, Transform rightForeArm, Transform rightHand,
            Transform leftArm, Transform leftForeArm, Transform leftHand, Transform spine2,
            Transform spine = null, Transform spine1 = null)
        {
            aimRoot = root;
            this.rightArm = rightArm; this.rightForeArm = rightForeArm; this.rightHand = rightHand;
            this.leftArm = leftArm; this.leftForeArm = leftForeArm; this.leftHand = leftHand;
            this.spine2 = spine2; this.spine = spine; this.spine1 = spine1;
            // Applied once immediately (not just on the next LateUpdate) so a freshly built avatar - e.g.
            // QaShotRunner restarting the run mid-frame - never has even a single frame where the hand/muzzle
            // sits at the FBX bind pose instead of the locked aim pose.
            ApplyAimLock();
            if (root != null && root.name == "Rook") Debug.Log("HA_DBG LockAimPose frame=" + Time.frameCount + " fwd=" + (rightHand != null ? rightHand.forward.ToString() : "null"));
        }

        int FindIndex(string contains)
        {
            if (clipList == null) return -1;
            for (int i = 0; i < clipList.Length; i++)
                if (clipList[i].name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>Per-instance playback rate for the active clip (1 = authored speed). Soldier crowds
        /// randomize this slightly per instance so 48-70 of them don't all stride in lockstep.</summary>
        public float Speed = 1f;

        /// <summary>Crossfades to the named clip (substring match, e.g. "Idle", "Run_and_Shoot") if it isn't
        /// already the active one. Ignored while a TriggerOnce one-shot is still playing. startTime seeds the
        /// clip's local time the first time it becomes active (mod'd against clip length in Update), letting
        /// a crowd of otherwise-identical soldiers start their run cycles at different phases.</summary>
        public void Play(string contains, bool loop, float startTime = 0f)
        {
            if (!graph.IsValid() || Time.time < lockUntil) return;
            int idx = FindIndex(contains);
            if (idx < 0 || idx == activeIndex) return;
            activeIndex = idx; loopFlag[idx] = loop; localTime[idx] = startTime;
        }

        /// <summary>Plays the named clip once, locking out Play() until it finishes.</summary>
        public void TriggerOnce(string contains)
        {
            if (!graph.IsValid()) return;
            int idx = FindIndex(contains);
            if (idx < 0) return;
            activeIndex = idx; loopFlag[idx] = false; localTime[idx] = 0f;
            lockUntil = Time.time + Mathf.Max(0.05f, clipList[idx].length);
        }

        bool DebugTag => aimRoot != null && aimRoot.name == "Rook";

        void Update()
        {
            if (DebugTag) Debug.Log("HA_DBG U-start frame=" + Time.frameCount + " activeIndex=" + activeIndex + " fwd=" + (rightHand != null ? rightHand.forward.ToString() : "null"));
            if (!graph.IsValid() || activeIndex < 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < clipPlayables.Length; i++)
            {
                bool isActive = i == activeIndex;
                weight[i] = Mathf.MoveTowards(weight[i], isActive ? 1f : 0f, dt / BlendSeconds);
                mixer.SetInputWeight(i, weight[i]);
                if (weight[i] <= 0f && !isActive) continue;
                localTime[i] += dt * Speed;
                float len = Mathf.Max(0.0001f, clipList[i].length);
                float t = loopFlag[i] ? localTime[i] % len : Mathf.Min(localTime[i], len);
                clipPlayables[i].SetTime(t);
            }
            if (DebugTag) Debug.Log("HA_DBG U-end frame=" + Time.frameCount + " fwd=" + (rightHand != null ? rightHand.forward.ToString() : "null"));
        }

        /// <summary>Runs after the PlayableGraph has evaluated this frame's pose (Unity updates bound
        /// AnimationPlayableOutputs between Update and LateUpdate), so this is the first point a correction
        /// to the posed Hips bone sticks instead of being immediately overwritten.</summary>
        void LateUpdate()
        {
            if (DebugTag) Debug.Log("HA_DBG LU-start frame=" + Time.frameCount + " fwd=" + (rightHand != null ? rightHand.forward.ToString() : "null"));
            if (hips != null)
            {
                var p = hips.localPosition;
                hipsSmoothedY = Mathf.Lerp(hipsSmoothedY, p.y, Mathf.Min(1f, Time.deltaTime * 6f));
                hips.localPosition = new Vector3(hipsLockedXZ.x, hipsSmoothedY, hipsLockedXZ.z);
            }

            ApplyAimLock();
            if (DebugTag) Debug.Log("HA_DBG LU-end frame=" + Time.frameCount + " fwd=" + (rightHand != null ? rightHand.forward.ToString() : "null"));
        }

        /// <summary>The actual pose lock: straightens the spine, then overrides the shoulder/elbow/hand
        /// chain to the fixed rifle-aim pose. Called every LateUpdate, and once eagerly from LockAimPose
        /// (see there for why).</summary>
        void ApplyAimLock()
        {
            if (aimRoot == null) return;
            // Arms/hands (and therefore the muzzle, via RightHandAim/LeftHandAim == identity) lock to
            // aimRoot.parent's rotation rather than aimRoot.rotation itself: SquadController/Soldier write
            // Rook/soldier's cosmetic yaw+pitch+lean (camera-facing 3/4 turn, run lean, steering tilt, hit
            // flinch) straight onto aimRoot's own localRotation, so locking to aimRoot.rotation dragged the
            // rifle through every one of those cosmetic swings - including the ~20 deg yaw added for the 3/4
            // view, which is exactly what sent the muzzle up over the shoulder instead of down the road. The
            // parent has no cosmetic rotation of its own (only position moves), so this keeps the rifle
            // pinned to true forward while the body underneath still turns/leans/flinches freely.
            var armBasis = aimRoot.parent != null ? aimRoot.parent.rotation : Quaternion.identity;
            // Spine2/Spine/Spine1 are ANCESTORS of the arm chain in the Mixamo rig (Hips->Spine->Spine1->
            // Spine2->Shoulder->Arm->ForeArm->Hand), so they must be straightened before the arm/hand
            // bones below are locked - not after. Each arm/hand assignment below sets an absolute world
            // rotation, so as long as nothing runs after it touches an ancestor, the muzzle ends up exactly
            // on armBasis every frame; doing the spine Slerps afterward re-introduced drift into the whole
            // arm subtree underneath them, which is what sent the muzzle up over the shoulder.
            // Spine2 is only partially pulled towards upright-forward (not hard-locked like the arms), so
            // the torso still breathes a little with the run cycle instead of reading as a stiff mannequin.
            if (spine2 != null) spine2.rotation = Quaternion.Slerp(spine2.rotation, aimRoot.rotation * Spine2Aim, 0.35f);
            // Straightens the lower/mid spine most of the way to upright (no forward offset, unlike Spine2's
            // small +4 deg allowance) - this is the bone-level fix for Rook's hunch, since the whole-body
            // forward pitch set in SquadController is a separate, much smaller "leaning into the run" tilt.
            if (spine != null) spine.rotation = Quaternion.Slerp(spine.rotation, aimRoot.rotation, 0.6f);
            if (spine1 != null) spine1.rotation = Quaternion.Slerp(spine1.rotation, aimRoot.rotation, 0.6f);

            if (rightArm != null) rightArm.rotation = armBasis * RightArmAim;
            if (rightForeArm != null) rightForeArm.rotation = armBasis * RightForeArmAim;
            if (rightHand != null) rightHand.rotation = armBasis * RightHandAim;
            if (leftArm != null) leftArm.rotation = armBasis * LeftArmAim;
            if (leftForeArm != null) leftForeArm.rotation = armBasis * LeftForeArmAim;
            if (leftHand != null) leftHand.rotation = armBasis * LeftHandAim;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
