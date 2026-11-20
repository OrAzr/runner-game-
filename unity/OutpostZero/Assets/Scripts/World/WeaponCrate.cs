using UnityEngine;

namespace OutpostZero
{
    /// <summary>Field pickup: shoot it open to give the squad a temporary weapon.</summary>
    public class WeaponCrate : Damageable
    {
        WeaponType weapon; float duration; Transform box; TextMesh hpLabel;

        public static WeaponCrate Create(Transform parent, CrateDef def, float duration)
        {
            var go = new GameObject("Crate_" + def.weapon); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(def.x, 0, def.distance);
            var c = go.AddComponent<WeaponCrate>();
            c.weapon = def.weapon; c.duration = duration; c.maxHp = c.hp = def.hp; c.hitRadius = 0.75f;
            var root = new GameObject("Box").transform; root.SetParent(go.transform, false); root.localPosition = Vector3.up * 0.6f; c.box = root;
            Prims.Box(root, Vector3.zero, Vector3.one * 1.2f, new Color(0.3f, 0.33f, 0.38f));
            Prims.Box(root, Vector3.zero, new Vector3(1.24f, 0.3f, 1.24f), Palette.Orange);
            Prims.Label3D(go.transform, WeaponDef.Get(def.weapon).label, new Vector3(0, 1.7f, 0), 0.5f, Palette.Orange);
            c.hpLabel = Prims.Label3D(go.transform, Mathf.CeilToInt(def.hp).ToString(), new Vector3(0, 2.25f, 0), 0.5f, Color.white);
            return c;
        }

        void Update() { box.Rotate(0, 45f * Time.deltaTime, 0); if (flash > 0) flash -= Time.deltaTime; }
        protected override void OnHit() { hpLabel.text = Mathf.Max(0, Mathf.CeilToInt(hp)).ToString(); }

        protected override void Die()
        {
            AudioManager.I?.Play(Sfx.CrateOpen);
            Effects.Burst(transform.position + Vector3.up * 0.8f, 24, Palette.Orange, 5f);
            GameManager.I.Squad.Shooter.GiveWeapon(weapon, duration);
            Destroy(gameObject);
        }
    }
}
