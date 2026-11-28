using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>The squad: forward motion, steering, head count, formation, gate math, losses.
    /// Only up to MaxVisible bodies are spawned; the real count can be larger (shown on the label).</summary>
    [RequireComponent(typeof(SteeringInput))]
    public class SquadController : MonoBehaviour
    {
        // Caps rendered soldier bodies for performance/readability at big squad sizes; Count (and the
        // count-badge label, which reads Count directly) still tracks the true squad size above this.
        public const int MaxVisible = 30;
        public int Count { get; private set; }
        public float Distance => transform.position.z;
        public float Radius => Formation.Radius(Mathf.Min(Count, MaxVisible), spacing);
        public HeroRook Rook { get; private set; }
        public AutoShooter Shooter { get; private set; }

        readonly List<Soldier> soldiers = new List<Soldier>(); // index 0 unused (Rook)
        LevelConfig level; SteeringInput input;
        float vx, lean;
        // Eased toward Formation.SpacingFor's target every frame (see Update) instead of snapping to it,
        // so a gate/wave that jumps Count doesn't yank the whole pack to a new spacing in one frame.
        float spacing;

        public IReadOnlyList<Soldier> Soldiers => soldiers;

        public void Init(LevelConfig cfg)
        {
            level = cfg;
            input = gameObject.GetComponent<SteeringInput>() ?? gameObject.AddComponent<SteeringInput>();
            input.Init(cfg.roadHalfWidth);
            Rook = gameObject.AddComponent<HeroRook>(); Rook.Build();
            Shooter = gameObject.AddComponent<AutoShooter>(); Shooter.Init(this);
            ShowWeapon(WeaponType.AssaultRifle);
            Count = cfg.startCount;
            spacing = Formation.SpacingFor(Mathf.Min(Count, MaxVisible));
            transform.position = Vector3.zero;
        }

        void Update()
        {
            var gm = GameManager.I; float dt = Time.deltaTime;
            bool playing = gm != null && gm.IsPlaying;
            // Includes BossFight (not just Running) so soldiers keep playing the run-and-shoot pose - with
            // the aim override from HeroAnimator.LockAimPose still locking their rifles forward - instead of
            // falling back to the idle/look-around clip once the squad parks at the boss and stops advancing
            // (see the p.z clamp below); matches HeroRook.Tick's own running check for the same state.
            bool running = playing && (gm.State == GameState.Running || gm.State == GameState.BossFight);
            int vis = Mathf.Min(Count, MaxVisible);
            // Eases over roughly a second rather than snapping, so the pack visibly loosens/tightens as
            // Count changes instead of every soldier jumping to a new slot the instant a gate resolves.
            spacing = Mathf.Lerp(spacing, Formation.SpacingFor(vis), Mathf.Min(1f, dt * 1.2f));
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
            SyncBodies(vis, running, dt);
            Rook.Tick(dt, Radius);
            // A squad-loss hit adds a quick backward pitch on top of the run pose (see RookAvatar.FlinchPitch)
            // instead of interrupting it, so Rook keeps running and shooting through the reaction.
            // Pitch is a small lean-into-the-run tilt (not the hunch fix - see HeroAnimator's Spine/Spine1
            // correction for that), dropped from 8 to 4 since it compounded with the clip's own forward
            // curve. Yaw turns Rook's whole visual body ~20 deg toward the camera (3/4 view: shows the
            // armor/backpack side instead of a flat back view) while he still reads as running forward -
            // cosmetic only, since fire direction (AutoShooter/Projectile) is always hardcoded world-forward
            // regardless of this transform's rotation, and there are no colliders on the visual body.
            if (Rook.Body) Rook.Body.localRotation = Quaternion.Euler((running ? 4f : 2f) - Rook.HitFlinch * 22f, 20f, lean);
        }

        void SyncBodies(int vis, bool running, float dt)
        {
            while (soldiers.Count < Mathf.Max(1, vis)) soldiers.Add(soldiers.Count == 0 ? null : SpawnSoldier(soldiers.Count, vis));
            // Removed soldiers play a short death pop themselves (Soldier.Kill) rather than vanishing
            // instantly, so they're dropped from the formation list immediately but keep animating on their
            // own until the coroutine destroys them.
            for (int i = soldiers.Count - 1; i >= Mathf.Max(1, vis); i--) { if (soldiers[i]) soldiers[i].Kill(); soldiers.RemoveAt(i); }
            float hw = level.roadHalfWidth - 0.3f;
            float t = Time.time;
            for (int i = 1; i < soldiers.Count; i++)
            {
                var slot = transform.position + Formation.Offset(i, vis, vx, spacing, t);
                slot.x = Mathf.Clamp(slot.x, -hw, hw);
                soldiers[i].Tick(slot, running, dt, lean, hw);
            }
            // The rigged model plays its own Run_and_Shoot cycle, so the old procedural hop (added on top of
            // every frame's pose) doubled up with the clip's own motion and read as a visible mid-air bounce;
            // the primitive fallback has no locomotion animation of its own, so it still gets the bob.
            float bob = running && !Rook.IsRigged ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.07f : 0f;
            Rook.Body.localPosition = Formation.Offset(0, vis, vx, spacing, t) + Vector3.up * bob;
            Rook.Body.gameObject.SetActive(Count > 0);
        }

        Soldier SpawnSoldier(int i, int vis)
        {
            var s = Soldier.Create(transform.parent);
            s.ShowWeapon(Shooter != null ? Shooter.Current : WeaponType.AssaultRifle);
            s.transform.position = transform.position + Formation.Offset(i, vis, vx, spacing, Time.time) + Vector3.back * 1.8f;
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
            Count = Mathf.Clamp(op.Apply(Count), 0, level.squadCap);
            ResolveGate(op.Text, before, op.Good);
        }

        public void ApplyCounter(int delta)
        {
            int before = Count;
            Count = Mathf.Clamp(Count + delta, 0, level.squadCap);
            ResolveGate(GateOp.Format(delta), before, delta >= 0);
        }

        void ResolveGate(string text, int before, bool good)
        {
            AudioManager.I?.Play(good ? Sfx.GateGood : Sfx.GateBad, good ? 0.95f : 0.8f);
            GameUI.I?.Popup(text, Count >= before);
            Effects.Burst(transform.position + Vector3.up * 1.3f, good ? 16 : 10, good ? new Color(0.56f, 0.78f, 1f) : new Color(1f, 0.42f, 0.33f), 5f);
            GameManager.I.Shake(good ? 0.2f : 0.32f);
            if (Count <= 0) GameManager.I.EndRun(false);
        }

        public void Lose(int n, string reason)
        {
            if (Rook.ShieldActive)
            {
                Debug.Log("[SQUAD] lose n=" + n + " reason=blocked-shield" + " count=" + Count);
                return;
            }
            if (n <= 0) return;
            Count = Mathf.Max(0, Count - n);
            AudioManager.I?.Play(Sfx.SoldierLost, 0.75f, 1f, 0.06f);
            GameManager.I.Shake(0.15f);
            Rook.PlayHitReaction();
            Debug.Log("[SQUAD] lose n=" + n + " reason=" + reason + " count=" + Count);
            if (Count <= 0) { Rook.PlayDeath(); GameManager.I.EndRun(false); }
        }

        /// <summary>True only when worldPos is actually within reach of a rendered body (Rook or a live
        /// soldier) - not the formation's enclosing circle, which is centered ahead of Rook and much bigger
        /// than any individual body, so using it for contact let zombies/the boss "hit" empty air.</summary>
        public bool TouchesBody(Vector3 worldPos, float extraReach)
        {
            var p2 = new Vector2(worldPos.x, worldPos.z);
            if (Rook.Body != null && Rook.Body.gameObject.activeSelf)
            {
                var bp = Rook.Body.position;
                if (Vector2.Distance(p2, new Vector2(bp.x, bp.z)) < 0.9f + extraReach) return true;
            }
            for (int i = 1; i < soldiers.Count; i++)
            {
                var s = soldiers[i];
                if (s == null || s.Dying) continue;
                var sp = s.transform.position;
                if (Vector2.Distance(p2, new Vector2(sp.x, sp.z)) < 0.4f + extraReach) return true;
            }
            return false;
        }

        public float TargetX { get => input.TargetX; set => input.TargetX = value; }
    }
}
