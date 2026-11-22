using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>The squad: forward motion, steering, head count, formation, gate math, losses.
    /// Only up to MaxVisible bodies are spawned; the real count can be larger (shown on the label).</summary>
    [RequireComponent(typeof(SteeringInput))]
    public class SquadController : MonoBehaviour
    {
        public const int MaxVisible = 80;
        public int Count { get; private set; }
        public float Distance => transform.position.z;
        public float Radius => Formation.Radius(Mathf.Min(Count, MaxVisible));
        public HeroRook Rook { get; private set; }
        public AutoShooter Shooter { get; private set; }

        readonly List<Soldier> soldiers = new List<Soldier>(); // index 0 unused (Rook)
        LevelConfig level; SteeringInput input; TextMesh countLabel; int shownCount = -1;
        float vx, lean, countPop;

        public IReadOnlyList<Soldier> Soldiers => soldiers;

        public void Init(LevelConfig cfg)
        {
            level = cfg;
            input = gameObject.GetComponent<SteeringInput>() ?? gameObject.AddComponent<SteeringInput>();
            input.Init(cfg.roadHalfWidth);
            Rook = gameObject.AddComponent<HeroRook>(); Rook.Build();
            Shooter = gameObject.AddComponent<AutoShooter>(); Shooter.Init(this);
            ShowWeapon(WeaponType.AssaultRifle);
            countLabel = Prims.Label3D(transform, "1", new Vector3(0, 2.8f, 0), 0.9f, Color.white);
            Count = cfg.startCount;
            transform.position = Vector3.zero;
        }

        void Update()
        {
            var gm = GameManager.I; float dt = Time.deltaTime;
            bool playing = gm != null && gm.IsPlaying;
            bool running = playing && gm.State == GameState.Running;
            if (playing)
            {
                var p = transform.position;
                if (gm.State == GameState.Running) p.z = Mathf.Min(level.bossDistance, p.z + level.runSpeed * dt);
                float err = input.TargetX - p.x;
                float desired = Mathf.Clamp(err * 8f, -10f, 10f);
                vx = Mathf.Lerp(vx, desired, Mathf.Min(1f, dt * 12f));
                // Margin scales with Rook's visual size (RookAvatar.GameplayScale) so his wider silhouette at
                // 2.5x still clears the road edge/gate posts instead of clipping through them at full steer.
                // The multiplier is bigger than his static bind-pose half-width: the Run_and_Shoot clip swings
                // his gun arm out to the side while running, and that reach was clipping through the barriers
                // even with the margin PR #13 tuned against the (arms-in) bind pose.
                float edgeMargin = 0.75f * RookAvatar.GameplayScale;
                p.x = Mathf.Clamp(p.x + vx * dt, -level.roadHalfWidth + edgeMargin, level.roadHalfWidth - edgeMargin);
                lean = Mathf.Lerp(lean, Mathf.Clamp(-vx * 3.2f, -18f, 18f), Mathf.Min(1f, dt * 9f));
                transform.position = p;
            }
            SyncBodies(running, dt);
            Rook.Tick(dt, Radius);
            if (Rook.Body) Rook.Body.localRotation = Quaternion.Euler(running ? 8f : 2f, 0, lean);
            if (shownCount != Count) { shownCount = Count; countLabel.text = Count.ToString(); countPop = 1f; }
            countPop = Mathf.Max(0f, countPop - dt * 3.2f);
            float pop = 1f + countPop * 0.5f;
            countLabel.transform.localScale = Vector3.one * pop;
            countLabel.transform.localPosition = new Vector3(0, 2.55f + Radius * 0.25f + countPop * 0.25f, -0.2f);
        }

        void SyncBodies(bool running, float dt)
        {
            int vis = Mathf.Min(Count, MaxVisible);
            while (soldiers.Count < Mathf.Max(1, vis)) soldiers.Add(soldiers.Count == 0 ? null : SpawnSoldier(soldiers.Count, vis));
            for (int i = soldiers.Count - 1; i >= Mathf.Max(1, vis); i--) { if (soldiers[i]) Destroy(soldiers[i].gameObject); soldiers.RemoveAt(i); }
            float hw = level.roadHalfWidth - 0.3f;
            for (int i = 1; i < soldiers.Count; i++)
            {
                var slot = transform.position + Formation.Offset(i, vis, vx);
                slot.x = Mathf.Clamp(slot.x, -hw, hw);
                soldiers[i].Tick(slot, running, dt, lean, hw);
            }
            // The rigged model plays its own Run_and_Shoot cycle, so the old procedural hop (added on top of
            // every frame's pose) doubled up with the clip's own motion and read as a visible mid-air bounce;
            // the primitive fallback has no locomotion animation of its own, so it still gets the bob.
            float bob = running && !Rook.IsRigged ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.07f : 0f;
            Rook.Body.localPosition = Formation.Offset(0, vis, vx) + Vector3.up * bob;
            Rook.Body.gameObject.SetActive(Count > 0);
        }

        Soldier SpawnSoldier(int i, int vis)
        {
            var s = Soldier.Create(transform.parent);
            s.ShowWeapon(Shooter != null ? Shooter.Current : WeaponType.AssaultRifle);
            s.transform.position = transform.position + Formation.Offset(i, vis, vx) + Vector3.back * 1.8f;
            return s;
        }

        public void ShowWeapon(WeaponType type)
        {
            if (Rook != null) Rook.ShowWeapon(type);
            for (int i = 1; i < soldiers.Count; i++) if (soldiers[i] != null) soldiers[i].ShowWeapon(type);
        }

        /// <summary>Muzzle positions of visible bodies (Rook first).</summary>
        public Vector3 MuzzleOf(int i)
        {
            if (i <= 0 || i >= soldiers.Count || soldiers[i] == null)
                return Rook != null && Rook.Muzzle != null ? Rook.Muzzle.position : transform.position + new Vector3(0.2f, 0.9f, 0.8f);
            return soldiers[i].Muzzle.position;
        }

        public void ApplyGate(GateOp op)
        {
            int before = Count;
            Count = Mathf.Max(0, op.Apply(Count));
            ResolveGate(op.Text, before, op.Good);
        }

        public void ApplyCounter(int delta)
        {
            int before = Count;
            Count = Mathf.Max(0, Count + delta);
            ResolveGate(GateOp.Format(delta), before, delta >= 0);
        }

        void ResolveGate(string text, int before, bool good)
        {
            AudioManager.I?.Play(good ? Sfx.GateGood : Sfx.GateBad, good ? 0.95f : 0.8f);
            GameUI.I?.Popup(text + "  →  " + Count, Count >= before);
            Effects.Burst(transform.position + Vector3.up * 1.3f, good ? 16 : 10, good ? new Color(0.56f, 0.78f, 1f) : new Color(1f, 0.42f, 0.33f), 5f);
            GameManager.I.Shake(good ? 0.2f : 0.32f);
            countPop = 1f;
            if (Count <= 0) GameManager.I.EndRun(false);
        }

        public void Lose(int n)
        {
            if (Rook.ShieldActive || n <= 0) return;
            Count = Mathf.Max(0, Count - n);
            countPop = 1f;
            AudioManager.I?.Play(Sfx.SoldierLost, 0.75f, 1f, 0.06f);
            GameManager.I.Shake(0.15f);
            Rook.PlayHitReaction();
            if (Count <= 0) { Rook.PlayDeath(); GameManager.I.EndRun(false); }
        }

        public float TargetX { get => input.TargetX; set => input.TargetX = value; }
    }
}
