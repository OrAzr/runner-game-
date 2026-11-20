using UnityEngine;

namespace OutpostZero
{
    /// <summary>Sunflower (phyllotaxis) formation: slot 0 is the hero at the front, soldiers pack in behind.</summary>
    public static class Formation
    {
        public const float Spacing = 0.42f;
        public static Vector3 Offset(int i)
        {
            if (i == 0) return new Vector3(0, 0, 0.2f);
            float a = i * 2.39996f, r = Spacing * Mathf.Sqrt(i);
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            return new Vector3(x, 0, -(Mathf.Abs(z) * 0.9f + 0.35f + z * 0.2f));
        }
        public static float Radius(int visible) => Spacing * Mathf.Sqrt(Mathf.Max(1, visible)) + 0.45f;
    }
}
