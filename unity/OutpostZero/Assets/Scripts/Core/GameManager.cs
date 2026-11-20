using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    public enum GameState { Menu, Running, BossFight, Victory, Defeat }

    /// <summary>Owns the run: state machine, spawning, win/lose. Everything else talks to GameManager.I.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public LevelConfig level = new LevelConfig();
        public bool autoStart = false;

        public GameState State { get; private set; } = GameState.Menu;
        public SquadController Squad { get; private set; }
        public Boss Boss { get; private set; }
        public float Distance => Squad ? Squad.Distance : 0f;
        public int Kills { get; set; }

        readonly List<Damageable> targets = new List<Damageable>();
        public IReadOnlyList<Damageable> Targets => targets;
        public void Register(Damageable d) { if (!targets.Contains(d)) targets.Add(d); }
        public void Unregister(Damageable d) { targets.Remove(d); }

        GameObject runRoot;
        CameraFollow cam;
        float hitstop, nextStop;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        void Start()
        {
            cam = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            BuildRun();
            GameUI.I?.ShowMenu();
            if (autoStart) StartRun();
        }

        public void BuildRun()
        {
            if (runRoot) Destroy(runRoot);
            targets.Clear(); Kills = 0; Boss = null;
            runRoot = new GameObject("Run");
            LevelBuilder.Build(level, runRoot.transform);
            var squadGo = new GameObject("Squad"); squadGo.transform.SetParent(runRoot.transform);
            Squad = squadGo.AddComponent<SquadController>();
            Squad.Init(level);
            if (cam) cam.target = Squad.transform;
            State = GameState.Menu;
        }

        public void StartRun()
        {
            if (State == GameState.Victory || State == GameState.Defeat) BuildRun();
            State = GameState.Running;
            AudioManager.I?.PlayMusic();
            AudioManager.I?.Play(Sfx.UIClick);
            GameUI.I?.ShowHud();
        }

        void Update()
        {
            Effects.TickNumbers();
            if (hitstop > 0f)
            {
                hitstop -= Time.unscaledDeltaTime;
                Time.timeScale = 0.06f;
                if (hitstop <= 0f) Time.timeScale = 1f;
            }
            if (State == GameState.Running && Squad.Distance >= level.bossDistance) BeginBoss();
        }

        /// <summary>Brief slow-motion punch. Cooldown keeps a big squad from freezing the boss.</summary>
        public void HitStop(float seconds)
        {
            if (Time.unscaledTime < nextStop) return;
            hitstop = Mathf.Max(hitstop, seconds);
            nextStop = Time.unscaledTime + 0.18f;
        }

        void BeginBoss()
        {
            State = GameState.BossFight;
            var go = new GameObject("Boss"); go.transform.SetParent(runRoot.transform);
            go.transform.position = new Vector3(0, 0, level.bossDistance + level.bossStartGap);
            Boss = go.AddComponent<Boss>();
            Boss.Init(level.bossHp, level.bossSpeed);
            AudioManager.I?.Play(Sfx.BossRoar);
            cam?.Shake(0.6f);
            GameUI.I?.Popup("BOSS!", false);
        }

        public bool IsPlaying => State == GameState.Running || State == GameState.BossFight;

        public void EndRun(bool win)
        {
            if (!IsPlaying) return;
            hitstop = 0f; Time.timeScale = 1f;
            State = win ? GameState.Victory : GameState.Defeat;
            AudioManager.I?.StopMusic();
            AudioManager.I?.StopFlame();
            AudioManager.I?.Play(win ? Sfx.Victory : Sfx.Defeat);
            GameUI.I?.ShowEnd(win, Squad.Count, Kills, Squad.Distance);
        }

        public void WinAfterBoss() { EndRun(true); }

        public void Shake(float amount) { cam?.Shake(amount); }
    }
}
