using UnityEngine;

namespace OutpostZero
{
    /// <summary>Anything bullets/flames can hurt (zombies, crates, boss). Uses simple radius checks (no physics layers needed).</summary>
    public abstract class Damageable : MonoBehaviour
    {
        public float maxHp = 3, hp = 3;
        public float hitRadius = 0.45f;
        public bool Alive { get; protected set; } = true;
        protected float flash;

        protected virtual void OnEnable() { GameManager.I?.Register(this); }
        protected virtual void OnDisable() { GameManager.I?.Unregister(this); }

        public virtual void TakeDamage(float amount, bool fromBullet = false)
        {
            if (!Alive) return;
            hp -= amount; flash = 0.08f;
            OnHit(fromBullet, amount);
            if (hp <= 0) { Alive = false; GameManager.I?.Unregister(this); Die(); }
        }
        protected virtual void OnHit(bool fromBullet, float amount) { }
        protected abstract void Die();
    }
}
