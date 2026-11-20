using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Pooled tracer bullet. Moves straight and tests against registered Damageables.</summary>
    public class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new Stack<Projectile>();
        static Transform poolRoot;
        Vector3 velocity; float life, damage; Renderer rend;

        public static void Fire(Vector3 pos, Vector3 dir, float speed, float range, float damage, Color color)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Create();
            p.transform.position = pos; p.transform.rotation = Quaternion.LookRotation(dir);
            p.velocity = dir * speed; p.life = range / speed; p.damage = damage;
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
            go.transform.SetParent(poolRoot); go.transform.localScale = new Vector3(0.07f, 0.07f, 0.7f);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = sharedMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<Projectile>(); p.rend = r; return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += velocity * dt; life -= dt;
            var gm = GameManager.I;
            if (gm != null)
            {
                var targets = gm.Targets; var pos = transform.position;
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var t = targets[i]; if (t == null || !t.Alive) continue;
                    var tp = t.transform.position;
                    if (Mathf.Abs(tp.x - pos.x) < t.hitRadius && Mathf.Abs(tp.z - pos.z) < t.hitRadius + 0.3f)
                    { t.TakeDamage(damage); Effects.Spark(pos); Recycle(); return; }
                }
            }
            if (life <= 0) Recycle();
        }

        void Recycle() { gameObject.SetActive(false); pool.Push(this); }
        public static void ClearPool() { pool.Clear(); if (poolRoot) Destroy(poolRoot.gameObject); }
    }
}
