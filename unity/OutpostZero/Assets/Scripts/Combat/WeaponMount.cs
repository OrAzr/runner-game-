using UnityEngine;

namespace OutpostZero
{
    /// <summary>Anchors the rigged Rook's weapon to the chest instead of the hand, so the gun's position no
    /// longer depends on the arm-swing animation. Runs after HeroAnimator's LateUpdate pose fixes (default
    /// order 0) so chest.position already reflects this frame's final pose, even though the mount itself
    /// never parents to a bone.</summary>
    [DefaultExecutionOrder(100)]
    public class WeaponMount : MonoBehaviour
    {
        /// <summary>Right/up/forward of the chest bone, as fractions of body height. Public static so every
        /// rigged Rook instance can be retuned from one place. Measured on the 1.7m model with a Blender IK
        /// test (plan-notes/ik_proto2.py) that proved this is the one spot both of Rook's hands can reach
        /// the weapon's Grip_R/Grip_L points. Y sits the rifle at mid-chest, about 17cm below Spine2, so it
        /// reads below the shoulder pad instead of behind it in side view. X/Z pushed further right/forward
        /// so the receiver and magazine clear the shoulder pad in side view instead of being covered by it.</summary>
        public static readonly Vector3 MountOffset = new Vector3(0.05f, -0.10f, 0.153f);

        public Transform GripR { get; private set; }
        public Transform GripL { get; private set; }
        public Transform Muzzle { get; private set; }

        /// <summary>When set, the mount aims along this transform's flattened forward instead of world +Z.
        /// Null in gameplay (always down the road); CharacterStage points this at the preview body root so
        /// the gun follows Rook's own facing on the spinning turntable instead of always aiming at the
        /// stage camera.</summary>
        public Transform AimReference;

        Transform bodyRoot, chest, pilotGlow;
        float height, recoil;
        GameObject model;
        WeaponType current = (WeaponType)(-1);
        bool missingLogged;

        /// <summary>Called by RookAvatar.BuildRigged with the model root as both parent and bodyRoot (so the
        /// mount inherits the root's uniform scale and its forward defines body yaw), the chest bone to
        /// anchor to (mixamorig:Spine2; may be null if the FBX is missing it), and the body's world height.</summary>
        public static WeaponMount Create(Transform parent, Transform bodyRoot, Transform chest, float height)
        {
            var go = new GameObject("WeaponMount");
            go.transform.SetParent(parent, false);
            var mount = go.AddComponent<WeaponMount>();
            mount.bodyRoot = bodyRoot;
            mount.chest = chest;
            mount.height = height;

            var muzzleGo = new GameObject("Muzzle");
            mount.Muzzle = muzzleGo.transform;
            mount.Muzzle.SetParent(go.transform, false);

            // Persistent flamethrower pilot glow, same look as WeaponVisual's: created once so AutoShooter's
            // flame effect (parented to Muzzle at init) always has a stable sibling, shown only while the
            // flamethrower is the active weapon.
            var glow = Prims.Part(PrimitiveType.Sphere, mount.Muzzle, Vector3.zero, Vector3.one * 0.06f, Prims.Unlit(new Color(1f, 0.72f, 0.2f)));
            mount.pilotGlow = glow.transform;
            mount.pilotGlow.gameObject.SetActive(false);

            mount.Show(WeaponType.AssaultRifle);
            return mount;
        }

        /// <summary>Swaps to type's model if it differs from the current one, so exactly one Grip_R/Grip_L/
        /// Muzzle set exists under Rook at any time. Muzzle itself is never destroyed - AutoShooter parents
        /// the flamethrower's flame effect to it once at init - so this only repositions it to match the new
        /// model's own Muzzle empty.</summary>
        public void Show(WeaponType type)
        {
            if (type == current && model != null) return;
            current = type;
            if (model != null) { Destroy(model); model = null; }

            var def = WeaponDefinition.Get(type);
            // targetLength = def.lengthMeters (scale 1): the mount already inherits the model root's uniform
            // scale, which matches body height, so the weapon's own metres need no extra factor here.
            model = WeaponModels.TryBuild(def, transform, def.lengthMeters, out var mounts);
            if (model == null)
            {
                if (!missingLogged)
                {
                    missingLogged = true;
                    Debug.LogWarning("[Outpost Zero] WeaponMount could not build a model for " + type + "; the muzzle stays at the mount.");
                }
                GripR = null; GripL = null;
                Muzzle.localPosition = Vector3.zero;
                Muzzle.localRotation = Quaternion.identity;
            }
            else
            {
                GripR = mounts.GripR;
                GripL = mounts.GripL;
                if (mounts.Muzzle != null)
                {
                    Muzzle.localPosition = transform.InverseTransformPoint(mounts.Muzzle.position);
                    Muzzle.localRotation = Quaternion.Inverse(transform.rotation) * mounts.Muzzle.rotation;
                }
                else
                {
                    Muzzle.localPosition = Vector3.zero;
                    Muzzle.localRotation = Quaternion.identity;
                }
            }

            if (pilotGlow != null) pilotGlow.gameObject.SetActive(type == WeaponType.Flamethrower);
        }

        public void Kick(float amount) { recoil = Mathf.Max(recoil, amount); }

        public void Tick(float dt)
        {
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 9f);
            if (pilotGlow != null && pilotGlow.gameObject.activeSelf)
            {
                float p = 0.05f + 0.02f * Mathf.Abs(Mathf.Sin(Time.time * 28f));
                pilotGlow.localScale = new Vector3(p, p, p);
            }
        }

        // Anchors to the chest bone rather than the hand, so arm-swing animation cannot drag the gun off its
        // grips, and aims along AimReference (or world +Z in gameplay) so body yaw never points the barrel
        // sideways. Only rotation comes from the aim basis; position also tracks bodyYaw so the chest-relative
        // offset still reads correctly as Rook turns.
        void LateUpdate()
        {
            Vector3 bodyFwd = bodyRoot != null ? bodyRoot.forward : Vector3.forward;
            bodyFwd.y = 0f;
            if (bodyFwd.sqrMagnitude < 1e-6f) bodyFwd = Vector3.forward;
            Quaternion bodyYaw = Quaternion.LookRotation(bodyFwd, Vector3.up);

            Vector3 aimFwd = Vector3.forward;
            if (AimReference != null)
            {
                aimFwd = AimReference.forward;
                aimFwd.y = 0f;
                if (aimFwd.sqrMagnitude < 1e-6f) aimFwd = Vector3.forward;
            }
            Quaternion aimRot = Quaternion.LookRotation(aimFwd, Vector3.up);

            Vector3 anchor = chest != null ? chest.position : (bodyRoot != null ? bodyRoot.position : transform.position);
            transform.position = anchor + bodyYaw * (MountOffset * height) - aimRot * Vector3.forward * (recoil * 0.02f * height);
            transform.rotation = aimRot;
        }
    }
}
