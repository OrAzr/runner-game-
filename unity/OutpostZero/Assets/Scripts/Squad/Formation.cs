using UnityEngine;

namespace OutpostZero
{
    /// <summary>Loose swarm: slot 0 is the hero just ahead, everyone else drifts around a breathing phyllotaxis cloud.</summary>
    public static class Formation
    {
        public const float Spacing = 0.42f;
        public static Vector3 Offset(int i, float time, float steer)
        {
            float sway = Mathf.Sin(time * 1.35f + i * 0.8f) * 0.16f;
            float flow = Mathf.Cos(time * 1.05f + i * 0.45f) * 0.12f;
            if (i == 0) return new Vector3(sway * 0.25f, 0, 0.15f);
            float a = i * 2.399963f + Mathf.Sin(time * 0.75f + i * 1.3f) * 0.42f;
            float breathe = 1f + 0.09f * Mathf.Sin(time * 1.25f + i * 0.6f);
            float r = (Spacing * Mathf.Sqrt(i) + 0.16f) * breathe;
            float x = Mathf.Cos(a) * r + sway - steer * Mathf.Min(0.18f, i * 0.01f);
            float z = -(Mathf.Abs(Mathf.Sin(a)) * r * 0.85f + 0.55f + (i % 4) * 0.1f + flow);
            return new Vector3(x, 0, z);
        }
        public static float Radius(int visible) => Spacing * Mathf.Sqrt(Mathf.Max(1, visible)) + 0.4f;
    }
}
