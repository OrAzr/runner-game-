using System.Collections;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>One squad member (rigged Meshy body, primitive-body fallback). Lerps toward its formation slot.</summary>
    public class Soldier : MonoBehaviour
    {
        public Transform Muzzle => weapon != null && weapon.Muzzle != null ? weapon.Muzzle : transform;
        public bool Dying { get; private set; }
        SoldierAvatar avatar;
        WeaponVisual weapon;
        float bobSeed;
        Vector3 smoothVel;

        public static Soldier Create(Transform parent)
        {
            var go = new GameObject("Soldier"); go.transform.SetParent(parent, false);
            var s = go.AddComponent<Soldier>();
            s.avatar = SoldierAvatar.Build(go.transform);
            s.weapon = s.avatar.Weapon;
            s.bobSeed = Random.value * 10f;
            return s;
        }

        public void ShowWeapon(WeaponType type) { if (weapon != null) weapon.Show(type); }

        public void Tick(Vector3 slot, bool moving, float dt, float lean, float hw)
        {
            // SmoothDamp (critically damped, frame-rate independent) instead of a per-soldier-k Lerp: the old
            // Lerp plus an independent per-soldier sine bob/tilt made the whole trailing group read as a
            // jittery blob since every soldier was relatively out of phase with its neighbors. One shared
            // smoothTime keeps the pack moving together in a stable formation.
            const float smoothTime = 0.16f;
            var p = transform.position;
            Vector3 target = new Vector3(slot.x, 0f, slot.z);
            p = Vector3.SmoothDamp(p, target, ref smoothVel, smoothTime);
            // SmoothDamp can overshoot its target when the target itself jumps quickly (a hard steering
            // reversal snaps every column's clamped slot.x sideways in one frame), which let the leftmost/
            // rightmost column punch through the road barriers; re-clamping the actual smoothed position
            // closes that gap regardless of how big the overshoot is.
            p.x = Mathf.Clamp(p.x, -hw, hw);
            // The rigged model plays its own Running clip, so the old procedural hop (added on top of every
            // frame's pose) doubled up with the clip's own motion; the primitive fallback has no locomotion
            // animation of its own, so it still gets the bob.
            bool rigged = avatar != null && avatar.IsRigged;
            float bob = moving && !rigged ? Mathf.Abs(Mathf.Sin(Time.time * 10f + bobSeed * 0.3f)) * 0.06f : 0f;
            p.y = bob;
            transform.position = p;
            transform.rotation = Quaternion.Euler(bob * 60f, 0f, lean * 0.65f);
            if (weapon != null) weapon.Tick(dt);
            if (avatar != null) avatar.Tick(moving);
        }

        /// <summary>Short, snappy knockback-and-shrink instead of vanishing instantly or ragdolling away
        /// for a long high flight - reads as a quick "pop" within about a third of a second.</summary>
        public void Kill()
        {
            if (Dying) return;
            Dying = true;
            StartCoroutine(DeathPop());
        }

        IEnumerator DeathPop()
        {
            const float duration = 0.32f;
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            Vector3 kick = new Vector3(Random.Range(-0.7f, 0.7f), 1.8f, -0.5f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                transform.localScale = startScale * (1f - u);
                transform.position = startPos + kick * u;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
