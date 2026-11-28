using UnityEngine;

namespace OutpostZero
{
    /// <summary>End-of-run brute. Big HP bar; walks down the road and devours soldiers on contact.</summary>
    public class Boss : Zombie
    {
        float speed, biteTimer, roarTimer = 6f, fightStart;

        protected override string ResourcePath => "Models/Boss/boss";
        protected override bool IsBossModel => true;

        public void Init(float hpValue, float walkSpeed)
        {
            Build(3.2f);
            // Floor the approach speed so a zero/near-zero level entry can never read as "the boss just
            // stands there" - it should always close the gap at a visibly threatening pace.
            maxHp = hp = hpValue; speed = Mathf.Max(2.2f, walkSpeed); hitRadius = 1.6f;
            if (anim == null)
            {
                skin[0].material.color = new Color(0.29f, 0.18f, 0.16f);
                skin[1].material.color = new Color(0.44f, 0.56f, 0.23f);
                for (int i = 0; i < 3; i++)
                {
                    Prims.Part(PrimitiveType.Capsule, transform, new Vector3(-0.2f + i * 0.2f, 1.75f, 0.05f), new Vector3(0.1f, 0.18f, 0.1f), Prims.Mat(new Color(0.85f, 0.82f, 0.72f)));
                }
            }
            else anim.TriggerOnce("Zombie_Scream");
            GameManager.I.Register(this);
            fightStart = Time.time;
            Debug.Log("[BOSS] fight start hp=" + hpValue);
        }

        protected override void Update()
        {
            var gm = GameManager.I; if (gm == null || !Alive) return;
            float dt = Time.deltaTime; phase += dt * 3f;
            if (anim == null)
            {
                armL.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 20f, 0, 0);
                armR.localRotation = Quaternion.Euler(-Mathf.Sin(phase) * 20f, 0, 0);
            }
            Flash(dt);
            if (!gm.IsPlaying) return;
            var squad = gm.Squad; var sp = squad.transform.position; var p = transform.position;
            var toSquad = new Vector3(sp.x - p.x, 0, sp.z - p.z);
            // Meshy model faces +Z; the old box placeholder faced -Z.
            if (toSquad.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(anim != null ? toSquad.normalized : -toSquad.normalized);
            // Track the squad's x even once in biting range (not just while closing the gap), so it doesn't
            // drift out of alignment while the player keeps steering during the fight.
            p.x = Mathf.Lerp(p.x, sp.x, 0.3f * dt);
            if (!squad.TouchesBody(p, 1.6f))
            {
                p.z -= speed * dt;
                anim?.Play("Walking", true);
            }
            else
            {
                // Holds on the Walking loop between bites - acceptable idle-hold while fighting (see
                // EnemyAnimator.Play; it no-ops if Walking is already the active clip).
                anim?.Play("Walking", true);
                biteTimer -= dt;
                if (biteTimer <= 0 && squad.TouchesBody(p, 1.6f))
                {
                    biteTimer = 2.5f;
                    // Starts right as the bite cycle begins, same instant as the damage/burst below, so the
                    // slam's impact reads in sync with the actual hit.
                    anim?.TriggerOnce("Charged_Ground_Slam");
                    squad.Lose(Mathf.Max(1, Mathf.CeilToInt(squad.Count * 0.04f)), "boss");
                    Effects.Burst(sp + Vector3.up * 1.3f, 14, new Color(1f, 0.42f, 0.33f), 5f);
                    gm.Shake(0.25f);
                }
            }
            transform.position = p;
            roarTimer -= dt;
            if (roarTimer <= 0) { roarTimer = 7f; AudioManager.I?.Play(Sfx.BossRoar, 0.7f); gm.Shake(0.35f); }
        }

        protected override void OnHit(bool fromBullet, float amount)
        {
            if (!fromBullet) return;
            var p = transform.position; p.z += 0.12f; transform.position = p;
            Effects.Number(p + Vector3.up * 3.1f, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), new Color(1f, 0.89f, 0.48f));
        }

        public override void TakeDamage(float amount, bool fromBullet = false)
        {
            base.TakeDamage(amount, fromBullet);
            if (fromBullet && amount >= 0.9f) GameManager.I?.HitStop(0.04f);
        }

        protected override void Die()
        {
            Debug.Log("[BOSS] down in " + (Time.time - fightStart).ToString("F2") + "s hpWas=" + maxHp);
            AudioManager.I?.Play(Sfx.BossRoar, 0.8f, 1.3f);
            Effects.Burst(transform.position + Vector3.up * 2, 60, new Color(0.62f, 0.82f, 0.29f), 8f);
            Effects.Burst(transform.position + Vector3.up * 2, 30, new Color(1f, 0.38f, 0.19f), 6f);
            GameManager.I.Invoke(nameof(GameManager.WinAfterBoss), 0.9f);
            if (anim != null) { anim.TriggerOnce("Dead"); Invoke(nameof(HideBody), 0.9f); }
            else gameObject.SetActive(false);
        }

        /// <summary>Lets the Dead clip play for the same 0.9s window GameManager already waits before
        /// WinAfterBoss, instead of hiding the model the instant hp hits 0 - purely cosmetic staging, the
        /// win delay itself is unchanged.</summary>
        void HideBody() => gameObject.SetActive(false);
    }
}
