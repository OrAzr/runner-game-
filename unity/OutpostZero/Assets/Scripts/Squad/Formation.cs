using UnityEngine;

namespace OutpostZero
{
    /// <summary>Organic crowd behind the hero: a golden-angle phyllotaxis ("sunflower") pack, folded so it
    /// only ever fills in behind and beside Rook, never in front of him, plus a small fixed per-soldier
    /// jitter (hashed from slot index, not re-rolled every frame) so the pack reads as a loose huddle
    /// rather than a rigid lattice. Soldier.Tick's SmoothDamp already supplies the lag chasing a moving
    /// slot; the steer-driven sway below is layered on top so the outer edge of the pack leans into a turn
    /// instead of the whole block snapping sideways together.</summary>
    public static class Formation
    {
        // Shoulder-to-shoulder width of the rigged soldier model at its in-game scale (SoldierAvatar.
        // TargetHeight) scales with HeightRatioToRook; at the current 0.62 ratio that's ~0.52m
        // (0.385m measured at the mesh's native 1.7m height, scaled). Spacing clears that with a small
        // margin so neighbouring soldiers never interpenetrate.
        public const float Spacing = 0.6f;
        const float GoldenAngle = 2.39996323f; // 137.5 degrees, in radians - classic sunflower packing step
        // Minimum clearance straight behind Rook's own (GameplayScale-blown-up) back, independent of the
        // pack's radius - without this floor a soldier could land at almost zero depth offset from Rook.
        const float FrontGap = 0.8f;
        const float Jitter = 0.12f;

        public static Vector3 Offset(int i, int visibleCount, float steer)
        {
            if (i == 0) return new Vector3(0f, 0f, 0.15f);

            int k = i; // 1-based index into the sunflower sequence
            float angle = k * GoldenAngle;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            if (sin > 0f) sin = -sin; // fold the front half behind so the pack never reads as surrounding Rook from ahead
            float r = Spacing * Mathf.Sqrt(k);
            float behind = -sin * r; // >= 0

            float sway = steer * Mathf.Min(0.22f, r * 0.05f);
            float x = cos * r - sway + Hash(i, 1) * Jitter;
            float z = -(FrontGap + behind) + Hash(i, 2) * Jitter;
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

        public static float Radius(int visible) => Spacing * Mathf.Sqrt(Mathf.Max(1, visible)) + 0.4f;
    }
}
