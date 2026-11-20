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

        public IReadOnlyList<Soldier> Soldiers => soldiers;

        public void Init(LevelConfig cfg)
        {
            level = cfg;
            input = gameObject.GetComponent<SteeringInput>() ?? gameObject.AddComponent<SteeringInput>();
            input.Init(cfg.roadHalfWidth);
            Rook = gameObject.AddComponent<HeroRook>(); Rook.Build();
            Shooter = gameObject.AddComponent<AutoShooter>(); Shooter.Init(this);
            countLabel = Prims.Label3D(transform, "1", new Vector3(0, 2.8f, 0), 0.9f, Color.white);
            Count = cfg.startCount;
            transform.position = Vector3.zero;
        }

        void Update()
        {
            var gm = GameManager.I; float dt = Time.deltaTime;
            bool playing = gm != null && gm.IsPlaying;
            if (playing)
            {
                var p = transform.position;
                if (gm.State == GameState.Running) p.z = Mathf.Min(level.bossDistance, p.z + level.runSpeed * dt);
                p.x = Mathf.Lerp(p.x, input.TargetX, Mathf.Min(1, dt * 10f));
                transform.position = p;
            }
            SyncBodies(playing && gm.State == GameState.Running, dt);
            Rook.Tick(dt, Radius);
            if (shownCount != Count) { shownCount = Count; countLabel.text = Count.ToString(); }
            countLabel.transform.localPosition = new Vector3(0, 2.6f + Radius * 0.3f, -0.3f);
        }

        void SyncBodies(bool running, float dt)
        {
            int vis = Mathf.Min(Count, MaxVisible);
            while (soldiers.Count < Mathf.Max(1, vis)) soldiers.Add(soldiers.Count == 0 ? null : SpawnSoldier(soldiers.Count));
            for (int i = soldiers.Count - 1; i >= Mathf.Max(1, vis); i--) { if (soldiers[i]) Destroy(soldiers[i].gameObject); soldiers.RemoveAt(i); }
            float hw = level.roadHalfWidth - 0.3f;
            for (int i = 1; i < soldiers.Count; i++)
            {
                var slot = transform.position + Formation.Offset(i);
                slot.x = Mathf.Clamp(slot.x, -hw, hw);
                soldiers[i].Tick(slot, running, dt);
            }
            Rook.Body.localPosition = Formation.Offset(0) + Vector3.up * (running ? Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.06f : 0);
            Rook.Body.gameObject.SetActive(Count > 0);
        }

        Soldier SpawnSoldier(int i)
        {
            var s = Soldier.Create(transform.parent);
            s.transform.position = transform.position + Formation.Offset(i) + Vector3.back * 2f;
            return s;
        }

        /// <summary>Muzzle positions of visible bodies (Rook first).</summary>
        public Vector3 MuzzleOf(int i)
        {
            if (i <= 0 || i >= soldiers.Count) return Rook.Body.position + new Vector3(0.27f, 0.9f, 0.6f);
            return soldiers[i].muzzle.position;
        }

        public void ApplyGate(GateOp op)
        {
            int before = Count;
            Count = Mathf.Max(0, op.Apply(Count));
            AudioManager.I?.Play(op.Good ? Sfx.GateGood : Sfx.GateBad);
            GameUI.I?.Popup(op.Text + "  →  " + Count, Count >= before);
            if (Count <= 0) GameManager.I.EndRun(false);
        }

        public void Lose(int n)
        {
            if (Rook.ShieldActive || n <= 0) return;
            Count = Mathf.Max(0, Count - n);
            AudioManager.I?.Play(Sfx.SoldierLost, 0.9f, 1f, 0.06f);
            GameManager.I.Shake(0.15f);
            if (Count <= 0) GameManager.I.EndRun(false);
        }

        public float TargetX { get => input.TargetX; set => input.TargetX = value; }
    }
}
