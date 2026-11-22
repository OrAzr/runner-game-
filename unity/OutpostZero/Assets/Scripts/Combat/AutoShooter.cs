using UnityEngine;

namespace OutpostZero
{
    /// <summary>Squad auto-fire. Fire rate scales with head count (capped at visible bodies);
    /// timed pickups swap the weapon for PickupDuration seconds.</summary>
    public class AutoShooter : MonoBehaviour
    {
        public WeaponType Current { get; private set; } = WeaponType.AssaultRifle;
        public float PickupTimeLeft { get; private set; }
        SquadController squad; float shotAcc, emberT; bool flameOn; FlameStream flameFx;
        WeaponType shown = (WeaponType)(-1);

        public void Init(SquadController s)
        {
            squad = s;
            var muzzle = s.Rook != null ? s.Rook.Muzzle : transform;
            flameFx = Effects.CreateFlame(muzzle);
            Effects.LogKeyShaders(flameFx != null ? flameFx.ShaderLabel : "null");
        }

        public void GiveWeapon(WeaponType w, float seconds)
        {
            Current = w; PickupTimeLeft = seconds;
            GameUI.I?.Popup(WeaponDef.Get(w).label + "!", true);
            ApplyLook();
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.IsPlaying) { SetFlame(false); return; }
            float dt = Time.deltaTime;
            if (Current != WeaponType.AssaultRifle) { PickupTimeLeft -= dt; if (PickupTimeLeft <= 0) Current = WeaponType.AssaultRifle; }
            ApplyLook();
            var w = WeaponDef.Get(Current);
            var tune = Tuning.Current;
            int vis = Mathf.Max(1, Mathf.Min(squad.Count, SquadController.MaxVisible));
            int totalVisible = Mathf.Min(squad.Count, SquadController.MaxVisible);
            int soldierShooters = Mathf.RoundToInt(Mathf.Max(0, totalVisible - 1) * Mathf.Clamp01(tune.soldierShootFraction));
            // Rook and soldiers get independent fire-rate multipliers, but mirror the original "shooters"
            // count exactly when both are 1 (the default), including the totalVisible == 0 edge case.
            float effectiveShooters = totalVisible <= 0 ? 0f
                : Mathf.Max(0f, tune.rookFireRateMult) + soldierShooters * Mathf.Max(0f, tune.soldierFireRateMult);
            float damage = w.damage * Mathf.Max(0f, tune.damageMult);

            if (Current == WeaponType.Flamethrower)
            {
                SetFlame(true);
                if (flameFx != null) flameFx.Tick(w.range);
                float R = w.range + squad.Radius * 0.5f, dps = w.dps * Mathf.Max(0f, tune.damageMult) * (4 + effectiveShooters);
                var targets = gm.Targets; var sp = transform.position;
                emberT += dt;
                bool toasted = false;
                Vector3 toastAt = sp;
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var t = targets[i]; if (t == null || !t.Alive) continue;
                    var tp = t.transform.position; float dz = tp.z - sp.z, dx = tp.x - sp.x;
                    if (dz > -0.5f && dz < R && Mathf.Abs(dx) < squad.Radius + 0.8f + dz * w.cone)
                    {
                        t.TakeDamage(dps * dt);
                        toasted = true; toastAt = tp + Vector3.up * 0.6f;
                    }
                }
                if (toasted && emberT > 0.16f) { emberT = 0f; Effects.Embers(toastAt); }
                Gate.FlameTick(sp, R, w.gateRate * dt);
                return;
            }
            SetFlame(false);
            float rate = effectiveShooters * w.rate;
            if (tune.maxShotsPerSecond > 0f) rate = Mathf.Min(rate, tune.maxShotsPerSecond);
            shotAcc += rate * dt;
            int shots = Mathf.FloorToInt(shotAcc); shotAcc -= shots;
            if (shots > 0) AudioManager.I?.Play(w.sfx, Current == WeaponType.AssaultRifle ? 0.46f : 0.62f, Random.Range(0.92f, 1.12f), Current == WeaponType.AssaultRifle ? 0.055f : 0.12f);
            bool shotgun = Current == WeaponType.Shotgun;
            while (shots-- > 0)
            {
                Vector3 m = squad.MuzzleOf(Random.Range(0, vis));
                Effects.MuzzleFlash(m, Vector3.forward, w.tracer, shotgun ? 0.72f : 0.26f);
                if (squad.Rook != null) squad.Rook.Kick(shotgun ? 1f : 0.35f);
                if (shotgun && gm != null) gm.Shake(0.08f);
                if (!shotgun && Random.value < 0.22f) Effects.Shell(m);
                for (int p = 0; p < w.pellets; p++)
                {
                    float a = Random.Range(-w.spread, w.spread);
                    Projectile.Fire(m, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), w.speed, w.range, damage, w.tracer, w.gate, shotgun);
                }
            }
        }

        void ApplyLook()
        {
            if (shown == Current) return;
            shown = Current;
            if (squad != null) squad.ShowWeapon(Current);
        }

        void SetFlame(bool on)
        {
            if (flameFx == null) return;
            if (on == flameOn) return;
            flameOn = on;
            flameFx.SetPlaying(on);
            if (on) AudioManager.I?.StartFlame(); else AudioManager.I?.StopFlame();
        }
    }
}
