using UnityEngine;

namespace OutpostZero
{
    /// <summary>Rook the commander: the starting hero. Assault rifle + special "Shield Wall"
    /// (blocks all squad losses for a few seconds; zombies that touch it die).</summary>
    public class HeroRook : MonoBehaviour
    {
        public float shieldDuration = 4f, shieldCooldown = 18f;
        public float ShieldTime { get; private set; }
        public float Cooldown { get; private set; }
        public bool ShieldActive => ShieldTime > 0;
        public Transform Body { get; private set; }
        GameObject shieldWall;

        public void Build()
        {
            var go = new GameObject("Rook"); go.transform.SetParent(transform, false); Body = go.transform;
            var t = Body;
            Prims.Box(t, new Vector3(0, 0.17f, 0), new Vector3(0.42f, 0.34f, 0.28f), Palette.Gunmetal);
            Prims.Box(t, new Vector3(0, 0.6f, 0), new Vector3(0.5f, 0.55f, 0.34f), Palette.Teal);
            Prims.Part(PrimitiveType.Sphere, t, new Vector3(0, 1.06f, 0), Vector3.one * 0.42f, Prims.Mat(Palette.TealDark));
            Prims.Box(t, new Vector3(-0.33f, 0.85f, 0), new Vector3(0.2f, 0.14f, 0.34f), Palette.Orange);
            Prims.Box(t, new Vector3(0.33f, 0.85f, 0), new Vector3(0.2f, 0.14f, 0.34f), Palette.Orange);
            Prims.Box(t, new Vector3(0, 0.36f, 0), new Vector3(0.5f, 0.06f, 0.36f), Palette.Orange);
            Prims.Box(t, new Vector3(0.2f, 0.66f, 0.36f), new Vector3(0.1f, 0.1f, 0.7f), new Color(0.11f, 0.13f, 0.15f));
            Body.localScale = Vector3.one * 1.35f;

            shieldWall = Prims.Part(PrimitiveType.Cube, transform, new Vector3(0, 0.8f, 1.1f), new Vector3(2f, 1.6f, 0.12f),
                Prims.Mat(new Color(0.22f, 0.88f, 0.88f, 0.45f), true));
            shieldWall.SetActive(false);
        }

        public void ActivateShield()
        {
            if (Cooldown > 0 || GameManager.I == null || !GameManager.I.IsPlaying) return;
            ShieldTime = shieldDuration; Cooldown = shieldCooldown;
            AudioManager.I?.Play(Sfx.GateGood, 0.8f, 0.7f);
        }

        public void Tick(float dt, float squadRadius)
        {
            if (ShieldTime > 0) ShieldTime -= dt;
            if (Cooldown > 0) Cooldown -= dt;
            shieldWall.SetActive(ShieldActive);
            if (ShieldActive)
            {
                shieldWall.transform.localScale = new Vector3(squadRadius * 2 + 0.6f, 1.6f, 0.12f);
                shieldWall.transform.position = new Vector3(transform.position.x, 0.8f, transform.position.z + 1.1f);
            }
        }
    }
}
