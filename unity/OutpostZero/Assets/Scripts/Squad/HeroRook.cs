using UnityEngine;

namespace OutpostZero
{
    /// <summary>Rook the commander: the starting hero. Assault rifle + special "Shield Wall"
    /// (blocks all squad losses for a few seconds; zombies that touch it die).</summary>
    public class HeroRook : MonoBehaviour
    {
        public float shieldDuration = 4f, shieldCooldown = 18f;
        public float ShieldTime { get; private set; }
        public float Cooldown { get; private set; }
        public bool ShieldActive => ShieldTime > 0;
        public Transform Body { get; private set; }
        public float Height => avatar != null ? avatar.Height : RookAvatar.BaseRiggedHeight;
        public bool IsRigged => avatar != null && avatar.IsRigged;
        public float HitFlinch => avatar != null ? avatar.FlinchPitch : 0f;
        public Transform Muzzle => avatar != null && avatar.Mount != null ? avatar.Mount.Muzzle
            : weapon != null && weapon.Muzzle != null ? weapon.Muzzle : Body;
        GameObject shieldWall;
        RookAvatar avatar;
        WeaponVisual weapon;

        public void Build()
        {
            avatar = RookAvatar.Build(transform, false);
            Body = avatar.Root;
            weapon = avatar.Weapon;
            shieldWall = Prims.Part(PrimitiveType.Cube, transform, new Vector3(0, 0.8f, 1.1f), new Vector3(2f, 1.6f, 0.12f),
                Prims.Mat(new Color(0.22f, 0.88f, 0.88f, 0.45f), true));
            shieldWall.SetActive(false);
        }

        public void ShowWeapon(WeaponType type)
        {
            if (avatar != null && avatar.Mount != null) avatar.Mount.Show(type);
            else if (weapon != null) weapon.Show(type);
        }

        public void Kick(float amount)
        {
            if (avatar != null && avatar.Mount != null) avatar.Mount.Kick(amount);
            else if (weapon != null) weapon.Kick(amount);
        }
        public void PlayHitReaction() { avatar?.PlayHitReaction(); }
        public void PlayDeath() { avatar?.PlayDeath(); }

        public void ActivateShield()
        {
            if (Cooldown > 0 || GameManager.I == null || !GameManager.I.IsPlaying) return;
            ShieldTime = shieldDuration; Cooldown = shieldCooldown;
            AudioManager.I?.Play(Sfx.GateGood, 0.8f, 0.7f);
        }

        public void Tick(float dt, float squadRadius, bool moving)
        {
            if (ShieldTime > 0) ShieldTime -= dt;
            if (Cooldown > 0) Cooldown -= dt;
            shieldWall.SetActive(ShieldActive);
            if (ShieldActive)
            {
                shieldWall.transform.localScale = new Vector3(squadRadius * 2 + 0.6f, 1.6f, 0.12f);
                shieldWall.transform.position = new Vector3(transform.position.x, 0.8f, transform.position.z + 1.1f);
            }
            if (avatar != null) avatar.Tick(dt, moving);
        }
    }
}
