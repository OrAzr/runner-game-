using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Pooled tracer bullet. Moves straight and tests against registered Damageables.</summary>
    public class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new Stack<Projectile>();
        static Transform poolRoot;
        Vector3 velocity, prev; float life, damage; int gatePoints; Renderer rend;

        public static void Fire(Vector3 pos, Vector3 dir, float speed, float range, float damage, Color color, int gatePoints = 1)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Create();
            p.transform.position = pos; p.prev = pos; p.transform.rotation = Quaternion.LookRotation(dir);
            p.velocity = dir * speed; p.life = range / speed; p.damage = damage; p.gatePoints = gatePoints;
            mpb.SetColor("_Color", color); mpb.SetColor("_BaseColor", color); p.rend.SetPropertyBlock(mpb);
            p.gameObject.SetActive(true);
        }

        static Material sharedMat;
        static MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static Projectile Create()
        {
            if (!poolRoot) poolRoot = new GameObject("ProjectilePool").transform;
            if (sharedMat == null) sharedMat = Prims.Unlit(new Color(1f, 0.83f, 0.42f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(poolRoot); go.transform.localScale = new Vector3(0.045f, 0.045f, 1.4f);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = sharedMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<Projectile>(); p.rend = r; return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            prev = transform.position;
            transform.position += velocity * dt; life -= dt;
            var pos = transform.position;
            if (Gate.TryHit(prev, pos, gatePoints)) { Effects.Spark(pos, new Color(1f, 0.94f, 0.7f)); Recycle(); return; }
            var gm = GameManager.I;
            if (gm != null)
            {
                var targets = gm.Targets;
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var t = targets[i]; if (t == null || !t.Alive) continue;
                    var tp = t.transform.position;
                    if (Mathf.Abs(tp.x - pos.x) < t.hitRadius && Mathf.Abs(tp.z - pos.z) < t.hitRadius + 0.35f)
                    { t.TakeDamage(damage, true); Effects.Spark(pos, new Color(1f, 0.94f, 0.7f)); Recycle(); return; }
                }
            }
            if (life <= 0) Recycle();
        }

        void Recycle() { gameObject.SetActive(false); pool.Push(this); }
        public static void ClearPool() { pool.Clear(); if (poolRoot) Destroy(poolRoot.gameObject); }
    }
}
