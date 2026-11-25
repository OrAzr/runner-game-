using UnityEngine;

namespace OutpostZero
{
    /// <summary>Organic crowd behind the hero: a golden-angle phyllotaxis ("sunflower") pack, restricted to
    /// the back half-plane by filtering the natural (unmodified) spiral down to the slots that already
    /// land behind Rook, rather than mirroring the front-half slots back there - mirroring broke the
    /// spiral's own non-overlap guarantee, because a slot's mirror image can land almost exactly on top of
    /// a different, already-behind slot at a similar radius. Filtering never moves a kept point, so every
    /// pairwise spacing the spiral already guaranteed stays intact. Small fixed per-soldier jitter (hashed
    /// from slot index, not re-rolled every frame) and a slow per-slot-phased breathing drift on top make
    /// the pack read as a loose, living huddle rather than a rigid lattice. Soldier.Tick's SmoothDamp
    /// already supplies the lag chasing a moving slot; the steer-driven sway below is layered on top so
    /// the outer edge of the pack leans into a turn instead of the whole block snapping sideways together.
    /// </summary>
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

            // Walk the full, unmodified golden-angle spiral and take the i-th slot whose natural angle
            // already falls in the back half (sin <= 0) - roughly every other m qualifies, so this scans
            // about 2x as many candidates as slots requested, trivial at the squad's size cap (<=30).
            int m = 0, found = 0;
            float angle = 0f, sin = 0f;
            while (found < i)
            {
                m++;
                angle = m * GoldenAngle;
                sin = Mathf.Sin(angle);
                if (sin <= 0f) found++;
            }
            float cos = Mathf.Cos(angle);
            float r = spacing * Mathf.Sqrt(m);

            float sway = steer * Mathf.Min(0.22f, r * 0.05f);
            // Slow sine drift, phased per slot (via Hash) so soldiers don't breathe in lockstep - small
            // enough next to Jitter/spacing that it never opens a gap wide enough to read as overlap risk.
            float breathe = Mathf.Sin(time * 0.55f + Hash(i, 3) * Mathf.PI) * 0.05f;
            float x = cos * r - sway + Hash(i, 1) * Jitter + breathe;
            float z = sin * r - FrontGap + Hash(i, 2) * Jitter + breathe * 0.5f; // sin <= 0, so this only ever pushes back
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

        // The back-half filter in Offset needs roughly 2x as many spiral candidates (m) as requested slots
        // (i) to find enough with sin <= 0, so the outermost kept slot's radius grows by about sqrt(2) over
        // the old "every slot kept" sqrt(visible) estimate.
        public static float Radius(int visible, float spacing) => spacing * Mathf.Sqrt(2f * Mathf.Max(1, visible)) + 0.4f;
    }
}
