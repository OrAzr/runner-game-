using System.Collections;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Basic shambler. Walks toward the squad once close; touching the squad kills one soldier (and the zombie).</summary>
    public class Zombie : Damageable
    {
        public float moveSpeed = 1.6f, aggroRange = 30f;
        protected Transform armL, armR; protected Renderer[] skin; protected float phase; protected Vector3 baseScale = Vector3.one;
        /// <summary>Non-null only for the rigged Meshy body (has its own Walking/Running/Punch_Combo/Dead
        /// clips); null for the primitive box fallback, which still wants the procedural arm swing/bob.</summary>
        protected EnemyAnimator anim;
        float animStartTime;
        static float groanCooldown;
        bool waveScaled;

        /// <summary>Reference height (metres) a zombie at scale 1 should read as - matches the primitive
        /// body's own built height so the rigged swap doesn't change gameplay framing. Boss passes its own
        /// scale (3.2) through the same formula to land on its existing ~5.2m size.</summary>
        const float BaseHeight = 1.6f;

        protected virtual string ResourcePath => "Models/Zombie/zombie";
        protected virtual bool IsBossModel => false;

        public static Zombie Create(Transform parent, Vector3 pos, float hp, float scale = 1f, float speed = 1.6f)
        {
            var go = new GameObject("Zombie"); go.transform.SetParent(parent, false); go.transform.position = pos;
            var z = go.AddComponent<Zombie>(); z.Build(scale); z.maxHp = z.hp = hp; z.moveSpeed = speed; return z;
        }

        protected void Build(float scale)
        {
            baseScale = Vector3.one * scale;
            hitRadius = 0.45f * scale;
            phase = Random.value * 6f;
            if (!BuildRigged(scale)) BuildPrimitive(scale);
        }

        bool BuildRigged(float scale)
        {
            var rigged = EnemyAvatar.Build(transform, ResourcePath, BaseHeight * scale, IsBossModel);
            if (rigged == null) return false;
            skin = new Renderer[] { rigged.Skin };
            anim = rigged.Anim;
            if (anim != null)
            {
                // Per-instance phase/speed jitter so a crowd of otherwise-identical bodies doesn't shamble
                // in lockstep (same trick SoldierAvatar uses for the squad's run cycle).
                animStartTime = Random.Range(0f, 1f);
                anim.Speed = Random.Range(0.9f, 1.1f);
            }
            return true;
        }

        void BuildPrimitive(float scale)
        {
            var t = transform;
            var legs = Prims.Box(t, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.3f), new Color(0.24f, 0.23f, 0.2f));
            var body = Prims.Part(PrimitiveType.Cube, t, new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.7f, 0.4f), Prims.Mat(Palette.ZombieShirt, true));
            var head = Prims.Part(PrimitiveType.Cube, t, new Vector3(0, 1.42f, -0.02f), Vector3.one * 0.42f, Prims.Mat(Palette.ZombieSkin, true));
            var eyeM = Prims.Unlit(new Color(1f, 0.23f, 0.16f));
            Prims.Part(PrimitiveType.Cube, t, new Vector3(-0.1f, 1.46f, -0.22f), new Vector3(0.08f, 0.06f, 0.02f), eyeM);
            Prims.Part(PrimitiveType.Cube, t, new Vector3(0.1f, 1.46f, -0.22f), new Vector3(0.08f, 0.06f, 0.02f), eyeM);
            armL = Prims.Part(PrimitiveType.Cube, t, new Vector3(-0.38f, 1.05f, -0.28f), new Vector3(0.16f, 0.16f, 0.6f), head.GetComponent<Renderer>().sharedMaterial).transform;
            armR = Prims.Part(PrimitiveType.Cube, t, new Vector3(0.38f, 1.05f, -0.28f), new Vector3(0.16f, 0.16f, 0.6f), head.GetComponent<Renderer>().sharedMaterial).transform;
            skin = new[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
            t.localScale = baseScale;
        }

        protected virtual void Update()
        {
            var gm = GameManager.I; if (gm == null || !Alive) return;
            float dt = Time.deltaTime; phase += dt * 6f;
            if (anim == null)
            {
                armL.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 15f, 0, 0);
                armR.localRotation = Quaternion.Euler(-Mathf.Sin(phase) * 15f, 0, 0);
            }
            Flash(dt);
            if (!gm.IsPlaying) return;
            var squad = gm.Squad; var sp = squad.transform.position; var p = transform.position;
            float dz = p.z - sp.z;
            if (dz < aggroRange && dz > -3f)
            {
                if (!waveScaled) { waveScaled = true; ScaleForSquad(gm, squad); }
                var dir = new Vector3(sp.x - p.x, 0, sp.z - p.z).normalized;
                p += dir * moveSpeed * dt;
                if (anim == null) p.y = Mathf.Abs(Mathf.Sin(phase)) * 0.06f;
                transform.position = p;
                // Meshy model faces +Z; the old box placeholder faced -Z.
                transform.rotation = Quaternion.LookRotation(anim != null ? dir : -dir);
                anim?.Play(moveSpeed >= 2.4f ? "Running" : "Walking", true, animStartTime);
                groanCooldown -= dt / 8f; // shared across zombies (each zombie ticks it)
                if (groanCooldown <= 0) { AudioManager.I?.Play(Sfx.ZombieGroan, 0.5f, Random.Range(0.85f, 1.15f)); groanCooldown = Random.Range(2.5f, 4.5f); }
            }
            // Contact uses the actual rendered bodies (TouchesBody), not the formation's enclosing circle -
            // that circle is centered ahead of Rook and much bigger than any individual body, so it used to
            // trigger a loss metres before a zombie was anywhere near a visible soldier.
            if (squad.TouchesBody(p, hitRadius))
            {
                if (squad.Rook.ShieldActive) { gm.Kills++; AudioManager.I?.Play(Sfx.ZombieHit, 0.6f); Effects.Spark(p + Vector3.up, new Color(0.6f, 0.94f, 0.94f)); }
                else squad.Lose(Mathf.Max(0, Mathf.RoundToInt(Tuning.Current.zombieContactDamageMult)), "zombie");
                Alive = false; gm.Unregister(this);
                Effects.Burst(p + Vector3.up, 10, new Color(0.56f, 0.69f, 0.29f), 4f);
                // Pop() grows and floats the zombie in place for its last 0.16s - played at the raw
                // contact position that could still be right against (or just inside) Rook's rendered
                // silhouette, so the dying zombie ballooning in size visibly clipped through him. Nudging
                // the start position away from the squad first keeps the whole animation clear of him.
                var away = new Vector3(p.x - sp.x, 0f, p.z - sp.z);
                transform.position = away.sqrMagnitude > 0.0001f ? p + away.normalized * 0.6f : p + Vector3.back * 0.6f;
                StartCoroutine(Pop(true));
                return;
            }
            if (dz < -6f) Destroy(gameObject);
        }

        /// <summary>Runs once, the first time this zombie aggros, so a wave's HP reflects how big the squad
        /// has actually grown (via gates passed earlier in the run) rather than the fixed value it was
        /// spawned with at BuildRun time - only meaningful once the owner's waveSquadScale knob is above 0.</summary>
        void ScaleForSquad(GameManager gm, SquadController squad)
        {
            float factor = Mathf.Clamp01(Tuning.Current.waveSquadScale);
            if (factor <= 0f) return;
            int baseline = Mathf.Max(1, gm.level.startCount);
            float ratio = Mathf.Max(1f, (float)squad.Count / baseline);
            float scale = Mathf.Lerp(1f, ratio, factor);
            hp *= scale; maxHp *= scale;
        }

        static readonly MaterialPropertyBlock hitBlock = new MaterialPropertyBlock();
        protected void Flash(float dt)
        {
            if (flash > 0f) flash -= dt;
            bool white = flash > 0f;
            if (skin == null) return;
            for (int i = 0; i < skin.Length; i++)
            {
                var r = skin[i];
                if (r == null) continue;
                var c = white || r.sharedMaterial == null ? Color.white : r.sharedMaterial.color;
                hitBlock.Clear();
                hitBlock.SetColor("_Color", c);
                hitBlock.SetColor("_BaseColor", c);
                r.SetPropertyBlock(hitBlock);
            }
        }

        protected override void OnHit(bool fromBullet, float amount)
        {
            if (!fromBullet) return;
            var p = transform.position;
            p.z += 0.16f;
            transform.position = p;
            if (amount >= 0.9f) Effects.Number(p + Vector3.up * 1.6f, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), new Color(1f, 0.89f, 0.48f));
        }

        protected override void Die()
        {
            GameManager.I.Kills++;
            AudioManager.I?.Play(Sfx.ZombieHit, 0.72f, Random.Range(0.9f, 1.14f), 0.05f);
            Effects.Burst(transform.position + Vector3.up, 12, new Color(0.56f, 0.69f, 0.29f), 4.5f);
            Effects.Spark(transform.position + Vector3.up, new Color(0.84f, 1f, 0.6f));
            StartCoroutine(Pop(false));
        }

        const float DeathHoldSeconds = 1.2f;
        const float PunchBeforeDeathSeconds = 0.4f;

        /// <summary>Plays the rigged body's death out (Punch_Combo into Dead for a kill-on-contact, straight
        /// to Dead for a bullet kill), holding the corpse for DeathHoldSeconds before despawning - the actual
        /// kill/HP math already happened the instant this was called, so this delay is purely cosmetic.
        /// Falls back to the old grow-and-float pop for the primitive body, which has no clips of its own.</summary>
        protected IEnumerator Pop(bool combatKill)
        {
            if (anim != null)
            {
                if (combatKill) anim.TriggerOnce("Punch_Combo");
                else anim.TriggerOnce("Dead");
                bool deadStarted = !combatKill;
                float t = 0f;
                while (t < DeathHoldSeconds)
                {
                    t += Time.deltaTime;
                    if (!deadStarted && t >= PunchBeforeDeathSeconds) { anim.TriggerOnce("Dead"); deadStarted = true; }
                    yield return null;
                }
                Destroy(gameObject);
                yield break;
            }

            float pt = 0f;
            while (pt < 0.16f)
            {
                pt += Time.deltaTime;
                transform.localScale = baseScale * (1f + pt * 3.2f);
                transform.position += Vector3.up * Time.deltaTime * 2.2f;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
