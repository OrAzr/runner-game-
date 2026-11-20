using UnityEngine;

namespace OutpostZero
{
    /// <summary>Tiny code-built particle effects (sparks, bursts, flamethrower) so no prefabs are needed.</summary>
    public static class Effects
    {
        static ParticleSystem burstPs;
        static Material particleMat;

        static Material PMat()
        {
            if (particleMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                particleMat = new Material(sh);
            }
            return particleMat;
        }

        static ParticleSystem Burster()
        {
            if (burstPs) return burstPs;
            var go = new GameObject("Bursts");
            burstPs = go.AddComponent<ParticleSystem>();
            var main = burstPs.main; main.startLifetime = 0.5f; main.startSpeed = 3f; main.startSize = 0.22f; main.gravityModifier = 1f;
            main.maxParticles = 1500; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var em = burstPs.emission; em.rateOverTime = 0;
            var shape = burstPs.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat();
            return burstPs;
        }

        public static void Burst(Vector3 pos, int n, Color c, float speed = 3f)
        {
            var ps = Burster();
            var ep = new ParticleSystem.EmitParams { position = pos, startColor = c, applyShapeToPosition = true };
            for (int i = 0; i < n; i++) { ep.velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.5f; ps.Emit(ep, 1); }
        }
        public static void Spark(Vector3 pos) => Burst(pos, 1, new Color(1f, 0.94f, 0.6f), 1.5f);

        public static ParticleSystem CreateFlame(Transform parent)
        {
            var go = new GameObject("Flame"); go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.7f, 0.6f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = 0.55f; main.startSpeed = 14f; main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.48f, 0.1f), new Color(1f, 0.76f, 0.23f));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = 600;
            var em = ps.emission; em.rateOverTime = 180;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat();
            return ps;
        }
    }
}
