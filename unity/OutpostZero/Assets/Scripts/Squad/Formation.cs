using UnityEngine;

namespace OutpostZero
{
    /// <summary>Orderly phalanx: slot 0 is the hero just ahead; everyone else fills rows behind him, widest
    /// row first. Deliberately has no per-frame oscillation of its own (the old version layered three
    /// independent sine waves with per-index phases, which read as a jittery blob rather than a formation) -
    /// Soldier.Tick is solely responsible for smoothing the walk from slot to slot.</summary>
    public static class Formation
    {
        public const float Spacing = 0.42f;

        /// <summary>Row i sits behind the hero; wider squads get wider rows (fewer, deeper rows) instead of
        /// an ever-widening single rank, so the block stays roughly road-width even at MaxVisible.</summary>
        public static Vector3 Offset(int i, int visibleCount, float steer)
        {
            if (i == 0) return new Vector3(0f, 0f, 0.15f);

            int perRow = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, visibleCount))), 4, 10);
            int idx = i - 1;
            int row = idx / perRow, col = idx % perRow;
            int colsInThisRow = Mathf.Min(perRow, Mathf.Max(1, visibleCount - 1) - row * perRow);

            float x = (col - (colsInThisRow - 1) * 0.5f) * Spacing - steer * Mathf.Min(0.18f, row * 0.02f);
            float z = -(0.85f + row * Spacing * 1.3f);
            return new Vector3(x, 0f, z);
        }

        public static float Radius(int visible) => Spacing * Mathf.Sqrt(Mathf.Max(1, visible)) + 0.4f;
    }
}
