using UnityEngine;

namespace OutpostZero
{
    /// <summary>Organic crowd behind the hero: a golden-angle phyllotaxis ("sunflower") pack, folded so it
    /// only ever fills in behind and beside Rook, never in front of him, plus a small fixed per-soldier
    /// jitter (hashed from slot index, not re-rolled every frame) and a slow per-slot-phased breathing
    /// drift so the pack reads as a loose, living huddle rather than a rigid lattice. Soldier.Tick's
    /// SmoothDamp already supplies the lag chasing a moving slot; the steer-driven sway below is layered
    /// on top so the outer edge of the pack leans into a turn instead of the whole block snapping sideways
    /// together.</summary>
    public static class Formation
    {
        // Shoulder-to-shoulder width of the rigged soldier model at its in-game scale (SoldierAvatar.
        // TargetHeight) scales with HeightRatioToRook; at the current 0.62 ratio that's ~0.52m
        // (0.385m measured at the mesh's native 1.7m height, scaled). SpacingFor never returns less than
        // this plus a small margin, regardless of what Tuning's squadSpacingMin is set to, so neighbouring
        // soldiers never interpenetrate even from a bad TUNE-screen value.
        const float BodyWidth = 0.52f;
        const float GoldenAngle = 2.39996323f; // 137.5 degrees, in radians - classic sunflower packing step
        // Minimum clearance straight behind Rook's own (GameplayScale-blown-up) back, independent of the
        // pack's radius - without this floor a soldier could land at almost zero depth offset from Rook.
        const float FrontGap = 0.8f;
        const float Jitter = 0.12f;
        const int LooseCount = 5;  // squads this size or smaller use the loosest (Tuning squadSpacingMax) spacing
        const int TightCount = SquadController.MaxVisible; // spacing bottoms out (squadSpacingMin) by the visible cap

        /// <summary>Spacing between soldier centers for a squad currently showing `visibleCount` bodies:
        /// loose for small squads, smoothly tightening (via smoothstep, not a linear ramp) toward a still
        /// non-overlapping minimum by the time the squad reaches its visible cap. Callers are expected to
        /// ease their own spacing value toward this target over time (see SquadController.spacing) rather
        /// than snap to it the instant Count changes, so the whole pack doesn't jump in one frame.</summary>
        public static float SpacingFor(int visibleCount)
        {
            float min = Mathf.Max(BodyWidth * 1.05f, Tuning.Current.squadSpacingMin);
            float max = Mathf.Max(min, Tuning.Current.squadSpacingMax);
            float t = Mathf.InverseLerp(LooseCount, TightCount, visibleCount);
            t = t * t * (3f - 2f * t); // smoothstep
            return Mathf.Lerp(max, min, t);
        }

        public static Vector3 Offset(int i, int visibleCount, float steer, float spacing, float time)
        {
            if (i == 0) return new Vector3(0f, 0f, 0.15f);

            int k = i; // 1-based index into the sunflower sequence
            float angle = k * GoldenAngle;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            if (sin > 0f) sin = -sin; // fold the front half behind so the pack never reads as surrounding Rook from ahead
            float r = spacing * Mathf.Sqrt(k);
            float behind = -sin * r; // >= 0

            float sway = steer * Mathf.Min(0.22f, r * 0.05f);
            // Slow sine drift, phased per slot (via Hash) so soldiers don't breathe in lockstep - small
            // enough next to Jitter/spacing that it never opens a gap wide enough to read as overlap risk.
            float breathe = Mathf.Sin(time * 0.55f + Hash(i, 3) * Mathf.PI) * 0.05f;
            float x = cos * r - sway + Hash(i, 1) * Jitter + breathe;
            float z = -(FrontGap + behind) + Hash(i, 2) * Jitter + breathe * 0.5f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Cheap deterministic pseudo-random in [-1, 1], stable per slot index so the huddle's
        /// organic look doesn't crawl frame to frame.</summary>
        static float Hash(int i, int seed)
        {
            unchecked
            {
                int h = i * 374761393 + seed * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h & 0x7fffffff) / (float)int.MaxValue) * 2f - 1f;
            }
        }

        public static float Radius(int visible, float spacing) => spacing * Mathf.Sqrt(Mathf.Max(1, visible)) + 0.4f;
    }
}
