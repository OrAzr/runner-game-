using UnityEngine;

namespace OutpostZero
{
    /// <summary>End-of-run brute. Big HP bar; walks down the road and devours soldiers on contact.</summary>
    public class Boss : Zombie
    {
        float speed, roarTimer = 6f, fightStart;
        // Sped up (same reasoning as Zombie.DeadSpeed) so the collapse reads as finished within the
        // existing 0.9s win-delay window instead of changing that delay.
        const float DeadSpeed = 2.2f;

        // The old hitRadius (1.6) was tuned against the boss's old ~5.12m height (BaseHeight * the legacy
        // 3.2 scale passed to Build below); grown by the same ratio so reach keeps pace with the new,
        // taller BossTargetHeight instead of leaving the boss under-reaching its own rendered body.
        const float LegacyHeight = BaseHeight * 3.2f;
        const float LegacyHitRadius = 1.6f;
        const float HitRadius = LegacyHitRadius * (BossTargetHeight / LegacyHeight); // ~2.1

        // Walking clip speed estimate: the Meshy source body is life-size (~1.72m) and its Walking clip
        // bakes in its own ground speed at that size; scaling the body up to BossTargetHeight scales the
        // clip's travel distance by the same factor, so the clip's effective ground speed scales with it
        // too. WalkClipGroundSpeedAtSource is a rough eyeballed estimate of that baked speed at 1.72m -
        // tune this if the tester's playtest shows feet sliding.
        const float SourceModelHeight = 1.72f;
        const float WalkClipGroundSpeedAtSource = 1.4f;
        const float WalkClipSpeed = WalkClipGroundSpeedAtSource * (BossTargetHeight / SourceModelHeight);
        const float MinWalkMultiplier = 0.6f, MaxWalkMultiplier = 2.5f;

        // Intro scream: plays once, the first time the boss reaches melee range, before any slams.
        const float ScreamSpeedMultiplier = 1.5f;
        const float ScreamClipLength = 2.8f; // authored Zombie_Scream length (seconds) at 1x
        const float ScreamDuration = ScreamClipLength / ScreamSpeedMultiplier; // ~1.9s

        // Slam cadence: TriggerOnce plays the clip at 1.2x, so its 3.0s authored length takes exactly the
        // same 2.5s as the cadence itself - no idle gap and no overlap between one slam's end and the next
        // one's start. The impact (damage/effects) lands at ImpactDelay seconds into that window, matching
        // the clip's own ~1.8s (authored) impact frame at 1.2x.
        const float SlamSpeedMultiplier = 1.2f;
        const float SlamClipLength = 3.0f; // authored Charged_Ground_Slam length (seconds) at 1x
        const float SlamCadence = SlamClipLength / SlamSpeedMultiplier; // 2.5s
        const float ImpactDelay = 1.5f;

        bool hasScreamed;
        float screamTimer;
        float slamCadenceTimer;
        float slamImpactTimer = -1f; // -1 when no slam impact is pending

        // Purely visual hit feedback: offsets the rigged model's own child transform, never the Boss root,
        // so a crowd of bullets can't push the boss's actual gameplay position (and thus his approach speed)
        // backward - that was the bug the tester caught (root crept away from the squad over a long fight).
        Transform modelRoot;
        float recoil, recoilVelocity;
        const float RecoilPerHit = 0.06f;
        const float RecoilMax = 0.3f;
        const float RecoilSmoothTime = 0.15f;

        protected override string ResourcePath => "Models/Boss/boss";
        protected override bool IsBossModel => true;

        public void Init(float hpValue, float walkSpeed)
        {
            Build(3.2f);
            // Floor the approach speed so a zero/near-zero level entry can never read as "the boss just
            // stands there" - it should always close the gap at a visibly threatening pace.
            maxHp = hp = hpValue; speed = Mathf.Max(2.2f, walkSpeed); hitRadius = HitRadius;
            if (anim == null)
            {
                skin[0].material.color = new Color(0.29f, 0.18f, 0.16f);
                skin[1].material.color = new Color(0.44f, 0.56f, 0.23f);
                for (int i = 0; i < 3; i++)
                {
                    Prims.Part(PrimitiveType.Capsule, transform, new Vector3(-0.2f + i * 0.2f, 1.75f, 0.05f), new Vector3(0.1f, 0.18f, 0.1f), Prims.Mat(new Color(0.85f, 0.82f, 0.72f)));
                }
            }
            // No scream here - it used to fire at spawn and lock the animator for ~2.8s before the first
            // Walking frame ever played; the scream now fires on first melee contact instead (see Update).
            modelRoot = transform.Find("BossBody"); // name set by EnemyAvatar.Build; null for the primitive fallback
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
            recoil = Mathf.SmoothDamp(recoil, 0f, ref recoilVelocity, RecoilSmoothTime);
            if (modelRoot != null) modelRoot.localPosition = new Vector3(0f, 0f, -recoil);
            if (!gm.IsPlaying) return;
            var squad = gm.Squad; var sp = squad.transform.position; var p = transform.position;
            var toSquad = new Vector3(sp.x - p.x, 0, sp.z - p.z);
            // Meshy model faces +Z; the old box placeholder faced -Z.
            if (toSquad.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(anim != null ? toSquad.normalized : -toSquad.normalized);
            // Track the squad's x even once in melee range (not just while closing the gap), so it doesn't
            // drift out of alignment while the player keeps steering during the fight.
            p.x = Mathf.Lerp(p.x, sp.x, 0.3f * dt);

            bool inRange = squad.TouchesBody(p, hitRadius);
            if (!inRange)
            {
                p.z -= speed * dt;
                float mult = Mathf.Clamp(speed / WalkClipSpeed, MinWalkMultiplier, MaxWalkMultiplier);
                anim?.Play("Walking", true, 0f, mult);
                // Re-closing the gap resets the cadence/impact so catching back up doesn't immediately pay
                // off a slam that was already interrupted mid-windup.
                slamCadenceTimer = 0f;
                slamImpactTimer = -1f;
            }
            else if (!hasScreamed)
            {
                hasScreamed = true;
                screamTimer = ScreamDuration;
                anim?.TriggerOnce("Zombie_Scream", ScreamSpeedMultiplier);
            }
            else if (screamTimer > 0f)
            {
                // Standing still holding the scream's last pose - deliberately no Play() call here, since
                // calling Play(Walking) while in range (even once the scream's own lock has expired) would
                // have him start walking in place before the first slam.
                screamTimer -= dt;
                if (screamTimer <= 0f) slamCadenceTimer = SlamCadence;
            }
            else
            {
                if (slamImpactTimer >= 0f)
                {
                    slamImpactTimer -= dt;
                    if (slamImpactTimer <= 0f)
                    {
                        slamImpactTimer = -1f;
                        if (squad.TouchesBody(p, hitRadius))
                        {
                            squad.Lose(Mathf.Max(1, Mathf.CeilToInt(squad.Count * 0.04f)), "boss");
                            Effects.Burst(sp + Vector3.up * 1.3f, 14, new Color(1f, 0.42f, 0.33f), 5f);
                            gm.Shake(0.25f);
                        }
                    }
                }
                slamCadenceTimer -= dt;
                if (slamCadenceTimer <= 0f)
                {
                    slamCadenceTimer = SlamCadence;
                    slamImpactTimer = ImpactDelay;
                    anim?.TriggerOnce("Charged_Ground_Slam", SlamSpeedMultiplier);
                }
            }
            transform.position = p;
            roarTimer -= dt;
            if (roarTimer <= 0) { roarTimer = 7f; AudioManager.I?.Play(Sfx.BossRoar, 0.7f); gm.Shake(0.35f); }
        }

        protected override void OnHit(bool fromBullet, float amount)
        {
            if (!fromBullet) return;
            recoil = Mathf.Min(RecoilMax, recoil + RecoilPerHit);
            var p = transform.position;
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
            if (anim != null) { anim.TriggerOnce("Dead", DeadSpeed); Invoke(nameof(HideBody), 0.9f); }
            else gameObject.SetActive(false);
        }

        /// <summary>Lets the Dead clip play for the same 0.9s window GameManager already waits before
        /// WinAfterBoss, instead of hiding the model the instant hp hits 0 - purely cosmetic staging, the
        /// win delay itself is unchanged.</summary>
        void HideBody() => gameObject.SetActive(false);
    }
}
