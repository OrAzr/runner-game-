using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Pooled tracer bullet. Moves straight and tests against registered Damageables.</summary>
    public class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new Stack<Projectile>();
        static Transform poolRoot;
        Vector3 velocity, prev; float life, damage; int gatePoints; Renderer rend, glow;

        public static void Fire(Vector3 pos, Vector3 dir, float speed, float range, float damage, Color color, int gatePoints = 1, bool heavy = false)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Create();
            p.transform.position = pos; p.prev = pos; p.transform.rotation = Quaternion.LookRotation(dir);
            p.transform.localScale = heavy ? new Vector3(0.09f, 0.09f, 0.42f) : new Vector3(0.03f, 0.03f, 0.95f);
            p.velocity = dir * speed; p.life = range / speed; p.damage = damage; p.gatePoints = gatePoints;
            mpb.Clear();
            mpb.SetColor("_Color", color); mpb.SetColor("_TintColor", color); mpb.SetColor("_BaseColor", color);
            p.rend.SetPropertyBlock(mpb);
            if (p.glow != null) p.glow.SetPropertyBlock(mpb);
            p.gameObject.SetActive(true);
        }

        static Material sharedMat;
        static MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static Projectile Create()
        {
            if (!poolRoot) poolRoot = new GameObject("ProjectilePool").transform;
            if (sharedMat == null)
            {
                sharedMat = Prims.SharedAdditive();
                if (sharedMat == null) sharedMat = Prims.Unlit(new Color(1f, 0.83f, 0.42f));
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(poolRoot); go.transform.localScale = new Vector3(0.03f, 0.03f, 0.95f);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = sharedMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(trail.GetComponent<Collider>());
            trail.transform.SetParent(go.transform, false);
            trail.transform.localPosition = new Vector3(0f, 0f, -0.35f);
            trail.transform.localScale = new Vector3(2.4f, 2.4f, 0.45f);
            var gr = trail.GetComponent<Renderer>(); gr.sharedMaterial = sharedMat; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<Projectile>(); p.rend = r; p.glow = gr; return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            prev = transform.position;
            transform.position += velocity * dt; life -= dt;
            var pos = transform.position;
            if (Gate.TryHit(prev, pos, gatePoints)) { Effects.Spark(pos, new Color(1f, 0.94f, 0.7f)); Effects.Impact(pos, new Color(1f, 0.9f, 0.55f)); Recycle(); return; }
            var gm = GameManager.I;
            if (gm != null)
            {
                var targets = gm.Targets;
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var t = targets[i]; if (t == null || !t.Alive) continue;
                    var tp = t.transform.position;
                    if (Mathf.Abs(tp.x - pos.x) < t.hitRadius && Mathf.Abs(tp.z - pos.z) < t.hitRadius + 0.35f)
                    { t.TakeDamage(damage, true); Effects.Spark(pos, new Color(1f, 0.94f, 0.7f)); Effects.Impact(pos, new Color(1f, 0.75f, 0.35f)); Recycle(); return; }
                }
            }
            if (life <= 0) Recycle();
        }

        void Recycle() { gameObject.SetActive(false); pool.Push(this); }
        public static void ClearPool() { pool.Clear(); if (poolRoot) Destroy(poolRoot.gameObject); }
    }
}
