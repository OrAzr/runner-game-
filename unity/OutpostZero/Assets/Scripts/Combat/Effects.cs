using UnityEngine;
using UnityEngine.Rendering;

namespace OutpostZero
{
    /// <summary>Code-built sparks, muzzle flashes, and the flamethrower stream. No imported textures.</summary>
    public static class Effects
    {
        static ParticleSystem burstPs;
        static Material particleMat;
        static bool shadersLogged;

        static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        static Transform[] flashT;
        static Renderer[] flashR;
        static float[] flashLife;
        static int flashCursor;
        static Transform[] shellT;
        static Vector3[] shellV;
        static float[] shellLife;
        static int shellCursor;

        static Material PMat()
        {
            if (particleMat != null) return particleMat;
            particleMat = Prims.SharedAdditive();
            if (particleMat == null) particleMat = Prims.Unlit(new Color(1f, 0.55f, 0.15f));
            return particleMat;
        }

        static void PrepareRenderer(ParticleSystemRenderer r, Material mat)
        {
            r.enabled = true;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0.01f;
            r.maxParticleSize = 0.8f;
        }

        static ParticleSystem Burster()
        {
            if (burstPs) return burstPs;
            var go = new GameObject("Bursts");
            burstPs = go.AddComponent<ParticleSystem>();
            burstPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burstPs.main;
            main.startLifetime = 0.45f; main.startSpeed = 3f; main.startSize = 0.16f; main.gravityModifier = 1f;
            main.maxParticles = 800; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var em = burstPs.emission; em.rateOverTime = 0;
            var shape = burstPs.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;
            PrepareRenderer(go.GetComponent<ParticleSystemRenderer>(), PMat());
            return burstPs;
        }

        public static void Burst(Vector3 pos, int n, Color c, float speed = 3f)
        {
            var ps = Burster();
            if (!ps.isPlaying) ps.Play(true);
            var ep = new ParticleSystem.EmitParams { position = pos, startColor = c, applyShapeToPosition = true };
            for (int i = 0; i < n; i++) { ep.velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.5f; ps.Emit(ep, 1); }
        }
        public static void Spark(Vector3 pos) => Spark(pos, new Color(1f, 0.94f, 0.6f));
        public static void Spark(Vector3 pos, Color c) => Burst(pos, 4, c, 4.5f);
        public static void Embers(Vector3 pos) { Burst(pos, 8, new Color(1f, 0.42f, 0.08f), 2.4f); Impact(pos, new Color(1f, 0.5f, 0.1f)); }
        public static void Flash(Vector3 pos, Color c) => MuzzleFlash(pos, Vector3.forward, c, 0.22f);

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, Color c, float size)
        {
            EnsureFx();
            int i = flashCursor; flashCursor = (flashCursor + 1) % flashT.Length;
            var t = flashT[i];
            t.gameObject.SetActive(true);
            t.position = pos;
            var cam = Camera.main;
            t.rotation = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);
            t.localScale = new Vector3(size, size, 1f);
            block.Clear();
            block.SetColor("_Color", c);
            block.SetColor("_TintColor", c);
            block.SetColor("_BaseColor", c);
            flashR[i].SetPropertyBlock(block);
            flashLife[i] = 0.06f;
        }

        public static void Impact(Vector3 pos, Color c) => MuzzleFlash(pos, Vector3.up, c, 0.42f);

        public static void Shell(Vector3 pos)
        {
            EnsureFx();
            int i = shellCursor; shellCursor = (shellCursor + 1) % shellT.Length;
            shellT[i].gameObject.SetActive(true);
            shellT[i].position = pos;
            shellT[i].localScale = new Vector3(0.035f, 0.035f, 0.07f);
            float side = Random.value < 0.5f ? -1f : 1f;
            shellV[i] = new Vector3(side * 1.6f, 2.1f, -0.3f);
            shellLife[i] = 0.45f;
        }

        static void EnsureFx()
        {
            if (flashT != null) return;
            var glow = PMat();
            flashT = new Transform[8]; flashR = new Renderer[8]; flashLife = new float[8];
            for (int i = 0; i < flashT.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = glow;
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                flashT[i] = go.transform; flashR[i] = r;
            }
            var metal = Prims.Mat(new Color(0.55f, 0.48f, 0.28f));
            shellT = new Transform[8]; shellV = new Vector3[8]; shellLife = new float[8];
            for (int i = 0; i < shellT.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = metal;
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                shellT[i] = go.transform;
            }
        }

        static readonly TextMesh[] numbers = new TextMesh[12];
        static float[] numberLife = new float[12];
        static int numberCursor;
        public static void Number(Vector3 pos, string text, Color c)
        {
            if (numbers[0] == null)
            {
                for (int i = 0; i < numbers.Length; i++)
                {
                    numbers[i] = Prims.Label3D(null, "", Vector3.zero, 0.5f, Color.white);
                    numbers[i].gameObject.SetActive(false);
                }
            }
            var tm = numbers[numberCursor];
            // Hit positions sit dead ahead in the hero's own firing lane, so a run of hits used to stack
            // every number right over his line of fire. Spreading them to alternating sides keeps the lane
            // clear while still reading as "numbers near the hit".
            float side = (numberCursor % 2 == 0 ? -1f : 1f) * (0.3f + (numberCursor % 3) * 0.12f);
            numberLife[numberCursor] = 0.55f; numberCursor = (numberCursor + 1) % numbers.Length;
            tm.gameObject.SetActive(true); tm.text = text; tm.color = c; tm.transform.position = pos + Vector3.right * side;
        }

        public static void TickNumbers()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] == null || numberLife[i] <= 0f) continue;
                numberLife[i] -= dt;
                numbers[i].transform.position += Vector3.up * dt * 1.8f;
                var c = numbers[i].color; c.a = Mathf.Clamp01(numberLife[i] / 0.55f); numbers[i].color = c;
                if (numberLife[i] <= 0f) numbers[i].gameObject.SetActive(false);
            }
            if (flashT != null)
            {
                for (int i = 0; i < flashT.Length; i++)
                {
                    if (flashLife[i] <= 0f) continue;
                    flashLife[i] -= dt;
                    if (flashLife[i] <= 0f) flashT[i].gameObject.SetActive(false);
                }
            }
            if (shellT != null)
            {
                for (int i = 0; i < shellT.Length; i++)
                {
                    if (shellLife[i] <= 0f) continue;
                    shellLife[i] -= dt;
                    shellV[i].y -= 12f * dt;
                    shellT[i].position += shellV[i] * dt;
                    if (shellLife[i] <= 0f) shellT[i].gameObject.SetActive(false);
                }
            }
        }

        public static FlameStream CreateFlame(Transform parent)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var stream = go.AddComponent<FlameStream>();
            stream.Build();
            return stream;
        }

        public static void LogKeyShaders(string flameShader)
        {
            if (shadersLogged) return;
            shadersLogged = true;
            Debug.Log("[Outpost Zero] shaders hero=" + Prims.ShaderName(Prims.Mat(Palette.Teal))
                + " weapon=" + Prims.ShaderName(Prims.Mat(Palette.Gunmetal))
                + " tracer=" + Prims.ShaderName(Prims.SharedAdditive())
                + " flame=" + flameShader);
        }
    }

    /// <summary>Particle stream plus unlit mesh jets parented to a muzzle. Local space so it stays on the moving squad.</summary>
    public class FlameStream : MonoBehaviour
    {
        ParticleSystem ps, smoke;
        Transform[] jets;
        Renderer[] jetR;
        Transform shimmer;
        bool playing;
        public string ShaderLabel { get; private set; } = "null";

        public void Build()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.duration = 1f;
            main.startLifetime = 0.42f;
            main.startSpeed = 11f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.35f), new Color(1f, 0.38f, 0.05f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 280;
            main.gravityModifier = -0.15f;
            var em = ps.emission;
            em.enabled = false;
            em.rateOverTime = 110f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.04f;
            shape.rotation = Vector3.zero;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var mat = Prims.SharedAdditive();
            if (mat == null) mat = Prims.Unlit(new Color(1f, 0.5f, 0.08f));
            ShaderLabel = Prims.ShaderName(mat);
            var rend = GetComponent<ParticleSystemRenderer>();
            rend.enabled = true;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.minParticleSize = 0.01f;
            rend.maxParticleSize = 0.8f;

            var smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(transform, false);
            smoke = smokeGo.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = smoke.main;
            sm.playOnAwake = false;
            sm.loop = true;
            sm.startLifetime = 1.15f;
            sm.startSpeed = 0.45f;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
            sm.startColor = new Color(0.32f, 0.32f, 0.32f, 0.45f);
            sm.simulationSpace = ParticleSystemSimulationSpace.Local;
            sm.scalingMode = ParticleSystemScalingMode.Hierarchy;
            sm.maxParticles = 80;
            sm.gravityModifier = -0.35f;
            var se = smoke.emission;
            se.enabled = false;
            se.rateOverTime = 16f;
            var ss = smoke.shape;
            ss.enabled = true;
            ss.shapeType = ParticleSystemShapeType.Sphere;
            ss.radius = 0.12f;
            var smokeMat = mat != null ? new Material(mat) : Prims.Unlit(new Color(0.3f, 0.3f, 0.3f));
            if (smokeMat != null)
            {
                smokeMat.SetColor("_Color", new Color(0.28f, 0.28f, 0.28f, 0.4f));
                smokeMat.SetColor("_TintColor", new Color(0.28f, 0.28f, 0.28f, 0.4f));
            }
            var smokeRend = smokeGo.GetComponent<ParticleSystemRenderer>();
            smokeRend.enabled = true;
            smokeRend.renderMode = ParticleSystemRenderMode.Billboard;
            smokeRend.alignment = ParticleSystemRenderSpace.View;
            smokeRend.sharedMaterial = smokeMat;
            smokeRend.shadowCastingMode = ShadowCastingMode.Off;
            smokeRend.receiveShadows = false;

            var heatMat = mat != null ? new Material(mat) : Prims.Unlit(new Color(1f, 0.45f, 0.1f, 0.25f));
            if (heatMat != null)
            {
                heatMat.SetColor("_Color", new Color(1f, 0.45f, 0.12f, 0.22f));
                heatMat.SetColor("_TintColor", new Color(1f, 0.45f, 0.12f, 0.22f));
            }
            var heat = Prims.Part(PrimitiveType.Quad, transform, new Vector3(0f, 0.05f, 1.2f), new Vector3(0.7f, 0.45f, 1f), heatMat);
            heat.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            shimmer = heat.transform;
            var heatR = heat.GetComponent<Renderer>();
            heatR.shadowCastingMode = ShadowCastingMode.Off;
            heatR.enabled = false;

            jets = new Transform[3];
            jetR = new Renderer[3];
            Color[] colors = { new Color(1f, 0.9f, 0.25f), new Color(1f, 0.48f, 0.06f), new Color(0.85f, 0.18f, 0.04f) };
            for (int i = 0; i < jets.Length; i++)
            {
                var jet = Prims.Part(PrimitiveType.Cube, transform, new Vector3((i - 1) * 0.04f, 0f, 0.6f), new Vector3(0.16f, 0.1f, 1.2f), Prims.Unlit(colors[i]));
                jetR[i] = jet.GetComponent<Renderer>();
                jetR[i].shadowCastingMode = ShadowCastingMode.Off;
                jetR[i].enabled = false;
                jets[i] = jet.transform;
            }
        }

        public void SetPlaying(bool on)
        {
            if (ps == null || on == playing) return;
            playing = on;
            var em = ps.emission;
            em.enabled = on;
            if (on) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (smoke != null)
            {
                var sem = smoke.emission;
                sem.enabled = on;
                if (on) smoke.Play(true);
                else smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (shimmer != null)
            {
                var sr = shimmer.GetComponent<Renderer>();
                if (sr != null) sr.enabled = on;
            }
            for (int i = 0; i < jetR.Length; i++) if (jetR[i] != null) jetR[i].enabled = on;
        }

        public void Tick(float range)
        {
            if (!playing || jets == null) return;
            float scaleZ = Mathf.Max(0.001f, transform.lossyScale.z);
            for (int i = 0; i < jets.Length; i++)
            {
                float flick = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(Time.time * (26f + i * 11f) + i * 1.7f));
                float len = Mathf.Max(0.4f, range * (0.42f + i * 0.22f) * flick / scaleZ);
                float w = (0.1f + i * 0.04f) * flick;
                jets[i].localScale = new Vector3(w, w * 0.45f, len);
                jets[i].localPosition = new Vector3((i - 1) * 0.05f, 0.01f * i, len * 0.5f);
            }
            float end = Mathf.Max(0.5f, range * 0.82f / scaleZ);
            if (smoke != null) smoke.transform.localPosition = new Vector3(0f, 0.15f, end);
            if (shimmer != null)
            {
                float flick = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 23f));
                shimmer.localPosition = new Vector3(0f, 0.04f, end * 0.62f);
                shimmer.localScale = new Vector3(0.55f * flick, 0.32f * flick, 1f);
            }
        }
    }
}
