using UnityEngine;

namespace OutpostZero
{
    /// <summary>Squad auto-fire. Fire rate scales with head count (capped at visible bodies);
    /// timed pickups swap the weapon for PickupDuration seconds.</summary>
    public class AutoShooter : MonoBehaviour
    {
        public WeaponType Current { get; private set; } = WeaponType.AssaultRifle;
        public float PickupTimeLeft { get; private set; }
        SquadController squad; float shotAcc; bool flameOn; ParticleSystem flameFx;

        public void Init(SquadController s)
        {
            squad = s;
            flameFx = Effects.CreateFlame(transform);
        }

        public void GiveWeapon(WeaponType w, float seconds)
        {
            Current = w; PickupTimeLeft = seconds;
            GameUI.I?.Popup(WeaponDef.Get(w).label + "!", true);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.IsPlaying) { SetFlame(false); return; }
            float dt = Time.deltaTime;
            if (Current != WeaponType.AssaultRifle) { PickupTimeLeft -= dt; if (PickupTimeLeft <= 0) Current = WeaponType.AssaultRifle; }
            var w = WeaponDef.Get(Current);
            int shooters = Mathf.Min(squad.Count, SquadController.MaxVisible);

            if (Current == WeaponType.Flamethrower)
            {
                SetFlame(true);
                float R = w.range + squad.Radius * 0.5f, dps = w.dps * (4 + shooters);
                var targets = gm.Targets; var sp = transform.position;
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var t = targets[i]; if (t == null || !t.Alive) continue;
                    var tp = t.transform.position; float dz = tp.z - sp.z, dx = tp.x - sp.x;
                    if (dz > -0.5f && dz < R && Mathf.Abs(dx) < squad.Radius + 0.8f + dz * w.cone) t.TakeDamage(dps * dt);
                }
                return;
            }
            SetFlame(false);
            shotAcc += shooters * w.rate * dt;
            int shots = Mathf.FloorToInt(shotAcc); shotAcc -= shots;
            if (shots > 0) AudioManager.I?.Play(w.sfx, Current == WeaponType.AssaultRifle ? 0.5f : 0.7f, 1f, Current == WeaponType.AssaultRifle ? 0.075f : 0.18f);
            int vis = Mathf.Max(1, Mathf.Min(squad.Count, SquadController.MaxVisible));
            while (shots-- > 0)
            {
                Vector3 m = squad.MuzzleOf(Random.Range(0, vis));
                for (int p = 0; p < w.pellets; p++)
                {
                    float a = Random.Range(-w.spread, w.spread);
                    Projectile.Fire(m, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), w.speed, w.range, w.damage, w.tracer);
                }
            }
        }

        void SetFlame(bool on)
        {
            if (on == flameOn) return; flameOn = on;
            if (on) { flameFx.Play(); AudioManager.I?.StartFlame(); } else { flameFx.Stop(); AudioManager.I?.StopFlame(); }
        }
    }
}
