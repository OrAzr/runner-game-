using UnityEngine;

namespace OutpostZero
{
    /// <summary>Analytic two-bone IK that keeps Rook's hands locked onto the active weapon's Grip_R/Grip_L
    /// points (WeaponMount.GripR/GripL) every frame, so the rifle reads as truly held rather than just
    /// floating at the chest while the arms swing through their own run-clip pose. Runs after WeaponMount
    /// (order 100) so the grips are already placed for this frame, and solves from bone POSITIONS only -
    /// never from bone local axes, which are unknown and differ between rigs.</summary>
    [DefaultExecutionOrder(110)]
    public class ArmIk : MonoBehaviour
    {
        class Arm
        {
            public Transform Upper, Fore, Hand;
            public float L1, L2;
            public bool BonesValid;
        }

        /// <summary>0 = arms stay exactly where the run clip put them, 1 = fully solved onto the grips. Eases
        /// towards WeightTarget every frame rather than snapping, so a weapon swap or death never pops the
        /// arms into place.</summary>
        public float Weight { get; private set; } = 1f;
        float weightTarget = 1f;
        const float EaseSeconds = 0.15f;

        WeaponMount mount;
        Transform bodyRoot;
        readonly Arm right = new Arm();
        readonly Arm left = new Arm();

        /// <summary>Caches bone references and measures each arm's bind-pose segment lengths (upper->elbow,
        /// elbow->wrist) once, since those lengths are constant and the IK itself only ever moves rotations.
        /// Any arm missing a bone logs one warning and is skipped forever after.</summary>
        public static ArmIk Create(GameObject root, WeaponMount mount, Transform bodyRoot,
            Transform rightUpper, Transform rightFore, Transform rightHand,
            Transform leftUpper, Transform leftFore, Transform leftHand)
        {
            var ik = root.AddComponent<ArmIk>();
            ik.mount = mount;
            ik.bodyRoot = bodyRoot;
            ik.SetupArm(ik.right, rightUpper, rightFore, rightHand, "right");
            ik.SetupArm(ik.left, leftUpper, leftFore, leftHand, "left");
            return ik;
        }

        void SetupArm(Arm arm, Transform upper, Transform fore, Transform hand, string side)
        {
            arm.Upper = upper; arm.Fore = fore; arm.Hand = hand;
            if (upper == null || fore == null || hand == null)
            {
                Debug.LogWarning("[Outpost Zero] ArmIk is missing a bone for Rook's " + side + " arm; that arm will keep its animated pose.");
                arm.BonesValid = false;
                return;
            }
            arm.L1 = Vector3.Distance(upper.position, fore.position);
            arm.L2 = Vector3.Distance(fore.position, hand.position);
            arm.BonesValid = true;
        }

        /// <summary>Called by RookAvatar.PlayDeath so the arms relax out of the grip IK instead of staying
        /// rifle-locked over a ragdoll-less death pose.</summary>
        public void ReleaseArms() { weightTarget = 0f; }

        void LateUpdate()
        {
            Weight = Mathf.MoveTowards(Weight, weightTarget, Time.deltaTime / EaseSeconds);
            if (Weight <= 0f) return;

            Vector3 bodyFwd = bodyRoot != null ? bodyRoot.forward : Vector3.forward;
            bodyFwd.y = 0f;
            if (bodyFwd.sqrMagnitude < 1e-6f) bodyFwd = Vector3.forward;
            Quaternion bodyYaw = Quaternion.LookRotation(bodyFwd, Vector3.up);

            Transform gripR = mount != null ? mount.GripR : null;
            Transform gripL = mount != null ? mount.GripL : null;
            SolveArm(right, gripR, bodyYaw * new Vector3(0.6f, -0.8f, -0.2f));
            SolveArm(left, gripL, bodyYaw * new Vector3(-0.6f, -0.8f, -0.2f));
        }

        void SolveArm(Arm arm, Transform target, Vector3 pole)
        {
            if (!arm.BonesValid || target == null) return;

            Vector3 S = arm.Upper.position;
            Vector3 T = target.position;
            Vector3 dir = (T - S).normalized;
            if (dir.sqrMagnitude < 1e-6f) return;

            float c = Mathf.Clamp(Vector3.Distance(S, T), 0.01f, 0.98f * (arm.L1 + arm.L2));
            Vector3 n = (pole - Vector3.Dot(pole, dir) * dir).normalized;
            float cosA = Mathf.Clamp((arm.L1 * arm.L1 + c * c - arm.L2 * arm.L2) / (2f * arm.L1 * c), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 elbowTarget = S + dir * (arm.L1 * cosA) + n * (arm.L1 * sinA);

            Vector3 E = arm.Fore.position;
            Quaternion upperPre = arm.Upper.rotation;
            Quaternion upperIk = Quaternion.FromToRotation(E - S, elbowTarget - S) * upperPre;
            arm.Upper.rotation = Quaternion.Slerp(upperPre, upperIk, Weight);

            // Re-read elbow/wrist: rotating Upper above already moved its children.
            E = arm.Fore.position;
            Vector3 W = arm.Hand.position;
            Vector3 wristTarget = S + dir * c;
            Quaternion forePre = arm.Fore.rotation;
            Quaternion foreIk = Quaternion.FromToRotation(W - E, wristTarget - E) * forePre;
            arm.Fore.rotation = Quaternion.Slerp(forePre, foreIk, Weight);

            // Hand bone deliberately left untouched: it inherits the forearm's new world rotation as a child.
        }
    }
}
