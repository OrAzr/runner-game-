using UnityEngine;

namespace OutpostZero
{
    /// <summary>One squad member (placeholder low-poly body). Lerps toward its formation slot.</summary>
    public class Soldier : MonoBehaviour
    {
        public Transform muzzle;
        float bobSeed;

        public static Soldier Create(Transform parent)
        {
            var go = new GameObject("Soldier"); go.transform.SetParent(parent, false);
            var t = go.transform;
            Prims.Box(t, new Vector3(0, 0.16f, 0), new Vector3(0.36f, 0.32f, 0.24f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0, 0.55f, 0), new Vector3(0.42f, 0.5f, 0.3f), Palette.Teal);
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0, 0.98f, 0), Vector3.one * 0.38f, Prims.Mat(Palette.TealDark));
            var gun = Prims.Box(t, new Vector3(0.16f, 0.62f, 0.3f), new Vector3(0.09f, 0.09f, 0.55f), new Color(0.11f, 0.13f, 0.15f));
            var s = go.AddComponent<Soldier>();
            s.muzzle = new GameObject("Muzzle").transform; s.muzzle.SetParent(t, false); s.muzzle.localPosition = new Vector3(0.16f, 0.62f, 0.6f);
            s.bobSeed = Random.value * 10f;
            return s;
        }

        public void Tick(Vector3 slot, bool running, float dt, float lean)
        {
            float k = 4.2f + (bobSeed % 5f) * 1.15f;
            var p = transform.position;
            p.x = Mathf.Lerp(p.x, slot.x, Mathf.Min(1f, dt * k));
            p.z = Mathf.Lerp(p.z, slot.z, Mathf.Min(1f, dt * (k + 1.5f)));
            float step = running ? Mathf.Sin(Time.time * 13f + bobSeed) : 0f;
            p.y = running ? Mathf.Abs(step) * 0.1f : 0f;
            transform.position = p;
            transform.rotation = Quaternion.Euler(step * 10f, 0f, lean * (0.65f + (bobSeed % 3f) * 0.12f));
        }
    }
}
