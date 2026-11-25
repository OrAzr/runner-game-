using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OutpostZero
{
    /// <summary>Runtime-built uGUI: title menu, HUD (progress, weapon timer, boss HP, shield button), popups, end screen.</summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI I { get; private set; }
        static RectTransform shieldRect; static Canvas canvasStatic;

        Canvas canvas; GameObject menu, hud, end, select, lockBadge, soonBanner;
        Image progressFill, bossFill, weaponFill; GameObject bossBar, weaponBox;
        Text weaponName, bossHpText, shieldText, endTitle, endStats, popup, levelText;
        Text heroName, heroRole, heroWeapon, heroSpecial; Image selectBtnImage; Button selectBtn; Text selectBtnLabel;
        Image[] rosterDots;
        RectTransform modelZone;
        CharacterStage stage;
        int heroIndex;
        bool selectDragging, selectOnModel;
        Vector2 selectStart, selectLast;
        float popupT, popupPulse;
        Font font;

        // ---------- tuning screen ----------
        GameObject tuning;
        RectTransform tuningContent;
        Text toastText;
        float toastT;
        readonly List<(TuneSpec spec, Text text)> tuneRows = new List<(TuneSpec, Text)>();

        void Awake() { I = this; font = Prims.UIFont(); Build(); }

        // ---------- tiny builders ----------
        RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = offMin; r.offsetMax = offMax; return r;
        }
        Image Img(string n, Transform p, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        { var r = Rect(n, p, aMin, aMax, oMin, oMax); var i = r.gameObject.AddComponent<Image>(); i.color = c; return i; }
        Text Txt(string n, Transform p, string s, int size, Color c, TextAnchor a, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var r = Rect(n, p, aMin, aMax, oMin, oMax); var t = r.gameObject.AddComponent<Text>();
            t.text = s; t.font = font; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.raycastTarget = false;
            r.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2); return t;
        }
        Button Btn(string n, Transform p, string label, Color c, Vector2 center, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = Img(n, p, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center - size / 2, center + size / 2);
            var b = img.gameObject.AddComponent<Button>(); b.onClick.AddListener(onClick);
            Txt("Label", img.transform, label, 44, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return b;
        }
        /// <summary>Like Btn() but anchored at an arbitrary point (e.g. (1, 0.5) for a right-edge-relative
        /// button) instead of always the parent's center - used by rows whose parent width isn't fixed.
        /// labelColor defaults to white rather than Btn()'s dark label: EdgeBtn's only callers are the tuning
        /// row -/+ buttons, which sit on Teal/TealDark backgrounds where a dark glyph is nearly invisible.</summary>
        Button EdgeBtn(string n, Transform p, string label, Color c, Vector2 anchor, Vector2 center, Vector2 size, UnityEngine.Events.UnityAction onClick, Color? labelColor = null)
        {
            var img = Img(n, p, c, anchor, anchor, center - size / 2, center + size / 2);
            var b = img.gameObject.AddComponent<Button>(); b.onClick.AddListener(onClick);
            Txt("Label", img.transform, label, 40, labelColor ?? Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return b;
        }

        void Build()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvasStatic = canvas;
            var scaler = cgo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            var root = canvas.transform;
            var safe = Rect("Safe", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // HUD
            hud = Rect("HUD", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Txt("Squad", hud.transform, "ROOK'S SQUAD", 34, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -80), new Vector2(330, -20));
            levelText = Txt("LevelNum", hud.transform, "LEVEL 1", 26, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -110), new Vector2(330, -86));
            var track = Img("Progress", hud.transform, new Color(0, 0, 0, 0.45f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(340, -62), new Vector2(-30, -38));
            progressFill = Img("Fill", track.transform, Palette.Teal, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            weaponBox = Img("Weapon", hud.transform, new Color(0.91f, 0.47f, 0.17f, 0.92f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -320), new Vector2(380, -230)).gameObject;
            weaponName = Txt("Name", weaponBox.transform, "SHOTGUN", 34, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-10, 0));
            weaponFill = Img("Time", weaponBox.transform, new Color(0.1f, 0.1f, 0.1f), Vector2.zero, new Vector2(1, 0), new Vector2(20, 10), new Vector2(-20, 20));
            weaponBox.SetActive(false);
            bossBar = Rect("Boss", hud.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -200), new Vector2(-30, -100)).gameObject;
            Txt("Title", bossBar.transform, "BRUTE BOSS", 36, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bossHpText = Txt("Hp", bossBar.transform, "", 36, Color.white, TextAnchor.UpperRight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bt = Img("Track", bossBar.transform, new Color(0, 0, 0, 0.6f), Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 44));
            bossFill = Img("Fill", bt.transform, new Color(0.85f, 0.2f, 0.15f), Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            bossBar.SetActive(false);
            var shield = Img("ShieldButton", hud.transform, Palette.Teal, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-270, 50), new Vector2(-60, 270));
            shield.gameObject.AddComponent<Button>().onClick.AddListener(() => GameManager.I.Squad.Rook.ActivateShield());
            shieldRect = shield.rectTransform;
            shieldText = Txt("Label", shield.transform, "SHIELD\nREADY", 32, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Txt("Hint", hud.transform, "Drag left / right to steer", 30, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 60), new Vector2(0, 110));
            popup = Txt("Popup", hud.transform, "", 72, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.72f), new Vector2(1, 0.82f), Vector2.zero, Vector2.zero);

            // Menu
            menu = Img("Menu", root, new Color(0.04f, 0.06f, 0.07f, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var title = Txt("Title", menu.transform, "OUTPOST <color=#e8792b>ZERO</color>", 110, new Color(0.22f, 0.82f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0, 0.55f), new Vector2(1, 0.7f), Vector2.zero, Vector2.zero);
            title.supportRichText = true;
            Txt("Sub", menu.transform, "Shoot the red numbered gates until\nthey turn blue, then run through them.\nGrab crates and take down the boss.", 34, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, new Vector2(0, 0.42f), new Vector2(1, 0.58f), Vector2.zero, Vector2.zero);
            Btn("Play", menu.transform, "PLAY", Palette.Orange, new Vector2(0, -150), new Vector2(420, 130), () => GameManager.I.StartRun());
            Btn("Tune", menu.transform, "TUNE", Palette.TealDark, new Vector2(0, -300), new Vector2(300, 100), ShowTuning);

            // End
            end = Img("End", root, new Color(0.04f, 0.06f, 0.07f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            endTitle = Txt("Title", end.transform, "VICTORY", 120, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.55f), new Vector2(1, 0.68f), Vector2.zero, Vector2.zero);
            endStats = Txt("Stats", end.transform, "", 38, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.48f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
            Btn("Again", end.transform, "PLAY AGAIN", Palette.Orange, new Vector2(0, -150), new Vector2(520, 130), () => GameManager.I.StartRun());
            end.SetActive(false);

            BuildSelect(root);
            BuildTuning(root);
            menu.SetActive(false);
        }

        void BuildSelect(Transform root)
        {
            var selectImage = Img("Select", root, new Color(0.04f, 0.06f, 0.07f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            selectImage.raycastTarget = true;
            select = selectImage.gameObject;
            Txt("Heading", select.transform, "CHOOSE YOUR HERO", 64, new Color(0.22f, 0.82f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), new Vector2(0, -40));
            var zone = Img("ModelZone", select.transform, new Color(0, 0, 0, 0.01f), new Vector2(0.16f, 0.36f), new Vector2(0.84f, 0.72f), Vector2.zero, Vector2.zero);
            modelZone = zone.rectTransform;
            Btn("Prev", select.transform, "<", Palette.Teal, new Vector2(-360, 120), new Vector2(120, 120), () => StepHero(-1));
            Btn("Next", select.transform, ">", Palette.Teal, new Vector2(360, 120), new Vector2(120, 120), () => StepHero(1));
            Txt("RotateHint", select.transform, "Drag to rotate", 28, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -80), new Vector2(0, -30));
            rosterDots = new Image[CharacterStage.Roster.Length];
            for (int i = 0; i < rosterDots.Length; i++)
            {
                float x = (i - 1) * 36f;
                rosterDots[i] = Img("Dot" + i, select.transform, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x - 8f, -130f), new Vector2(x + 8f, -114f));
            }
            var info = Img("Info", select.transform, new Color(0.03f, 0.04f, 0.05f, 0.86f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(28, 36), new Vector2(-28, 560));
            heroName = Txt("Name", info.transform, "ROOK", 72, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -110), new Vector2(-16, -16));
            heroRole = Txt("Role", info.transform, "COMMANDER", 36, Palette.Orange, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -170), new Vector2(-16, -110));
            heroWeapon = Txt("Weapon", info.transform, "WEAPON  ASSAULT RIFLE", 32, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -240), new Vector2(-16, -180));
            heroSpecial = Txt("Special", info.transform, "SPECIAL  SHIELD WALL", 32, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -310), new Vector2(-16, -250));
            lockBadge = Rect("Lock", select.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70, 40), new Vector2(70, 200)).gameObject;
            Img("ShackleL", lockBadge.transform, Color.white, new Vector2(0, 0.45f), new Vector2(0, 1), new Vector2(18, 0), new Vector2(34, -8));
            Img("ShackleR", lockBadge.transform, Color.white, new Vector2(1, 0.45f), new Vector2(1, 1), new Vector2(-34, 0), new Vector2(-18, -8));
            Img("ShackleTop", lockBadge.transform, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -28), new Vector2(-18, -10));
            Img("Body", lockBadge.transform, Color.white, new Vector2(0, 0), new Vector2(1, 0.55f), new Vector2(8, 0), new Vector2(-8, 0));
            soonBanner = Txt("Soon", select.transform, "COMING SOON", 40, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -40), new Vector2(0, 30)).gameObject;
            selectBtn = Btn("Select", info.transform, "SELECT", Palette.Orange, new Vector2(0, -180), new Vector2(460, 110), ConfirmHero);
            selectBtnImage = selectBtn.GetComponent<Image>();
            selectBtnLabel = selectBtn.GetComponentInChildren<Text>();
            var stageGo = new GameObject("CharacterStage");
            stage = stageGo.AddComponent<CharacterStage>();
            stage.Build();
            heroIndex = CharacterStage.SavedIndex();
            ApplyHero(heroIndex);
            select.SetActive(false);
            if (stage != null) stage.Hide();
        }

        public void ShowSelect()
        {
            if (hud != null) hud.SetActive(false);
            if (menu != null) menu.SetActive(false);
            if (end != null) end.SetActive(false);
            if (tuning != null) tuning.SetActive(false);
            if (select != null) select.SetActive(true);
            heroIndex = CharacterStage.SavedIndex();
            ApplyHero(heroIndex);
        }

        void StepHero(int dir)
        {
            heroIndex = (heroIndex + dir + CharacterStage.Roster.Length) % CharacterStage.Roster.Length;
            ApplyHero(heroIndex);
        }

        void ApplyHero(int index)
        {
            var slot = CharacterStage.Roster[index];
            heroName.text = slot.name;
            heroRole.text = slot.role;
            heroWeapon.text = "WEAPON  " + slot.weapon;
            heroSpecial.text = "SPECIAL  " + slot.special;
            bool open = slot.playable;
            if (lockBadge != null) lockBadge.SetActive(!open);
            if (soonBanner != null) soonBanner.SetActive(!open);
            if (selectBtn != null) selectBtn.interactable = open;
            if (selectBtnImage != null) selectBtnImage.color = open ? Palette.Orange : new Color(0.35f, 0.37f, 0.4f);
            if (selectBtnLabel != null) selectBtnLabel.text = open ? "SELECT" : "LOCKED";
            if (rosterDots != null)
            {
                for (int i = 0; i < rosterDots.Length; i++)
                    if (rosterDots[i] != null) rosterDots[i].color = i == index ? Palette.Orange : new Color(1f, 1f, 1f, 0.35f);
            }
            if (stage != null) stage.Show(index);
        }

        void ConfirmHero()
        {
            if (heroIndex < 0 || heroIndex >= CharacterStage.Roster.Length || !CharacterStage.Roster[heroIndex].playable) return;
            CharacterStage.Remember(heroIndex);
            if (select != null) select.SetActive(false);
            if (stage != null) stage.Hide();
            ShowMenu();
        }

        public void ShowMenu()
        {
            if (select != null) select.SetActive(false);
            if (stage != null) stage.Hide();
            if (tuning != null) tuning.SetActive(false);
            if (hud != null) hud.SetActive(true);
            menu.SetActive(true); end.SetActive(false);
        }
        public void ShowHud()
        {
            if (select != null) select.SetActive(false);
            if (stage != null) stage.Hide();
            if (tuning != null) tuning.SetActive(false);
            if (hud != null) hud.SetActive(true);
            menu.SetActive(false); end.SetActive(false);
        }
        public void ShowEnd(bool win, int survivors, int kills, float dist)
        {
            endTitle.text = win ? "VICTORY" : "DEFEAT";
            endTitle.color = win ? new Color(0.5f, 0.82f, 1f) : new Color(1f, 0.42f, 0.36f);
            endStats.text = win ? $"Outpost secured · {survivors} survivors · {kills} zombies down" : $"Squad lost at {Mathf.RoundToInt(dist)}m · {kills} zombies down";
            Invoke(nameof(RevealEnd), 0.7f);
        }
        void RevealEnd() { end.SetActive(true); }

        public void Popup(string text, bool good)
        {
            // Just the gate's own value (e.g. "+4", "-11") - no "-> newTotal" arrow, which read as debug
            // output. A quick pulse stands in for the old transition text instead.
            popup.text = text; popup.color = good ? new Color(0.5f, 0.82f, 1f) : new Color(1f, 0.42f, 0.36f);
            popupT = 0.9f; popupPulse = 1f;
        }

        void Update()
        {
            if (toastT > 0f && toastText != null)
            {
                toastT -= Time.deltaTime;
                var tc = toastText.color; tc.a = Mathf.Clamp01(toastT / 0.3f); toastText.color = tc;
            }
            if (tuning != null && tuning.activeSelf) return;
            if (select != null && select.activeSelf) { TickSelect(); return; }
            var gm = GameManager.I; if (gm == null || gm.Squad == null) return;
            if (levelText != null) levelText.text = "LEVEL " + gm.LevelNumber;
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(gm.Distance / gm.level.bossDistance), 1);
            var sh = gm.Squad.Shooter;
            weaponBox.SetActive(sh.Current != WeaponType.AssaultRifle);
            if (weaponBox.activeSelf)
            {
                weaponName.text = WeaponDef.Get(sh.Current).label;
                weaponFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(sh.PickupTimeLeft / gm.level.pickupDuration), 0);
            }
            bool bossUp = gm.Boss != null && gm.Boss.Alive;
            bossBar.SetActive(bossUp);
            if (bossUp) { bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(gm.Boss.hp / gm.Boss.maxHp), 1); bossHpText.text = Mathf.Max(0, Mathf.CeilToInt(gm.Boss.hp)).ToString(); }
            var rook = gm.Squad.Rook;
            shieldText.text = rook.ShieldActive ? "SHIELD\nACTIVE" : rook.Cooldown > 0 ? $"SHIELD\n{Mathf.CeilToInt(rook.Cooldown)}s" : "SHIELD\nREADY";
            if (popupT > 0) { popupT -= Time.deltaTime; var c = popup.color; c.a = Mathf.Clamp01(popupT / 0.4f); popup.color = c; }
            if (popupPulse > 0f) { popupPulse = Mathf.Max(0f, popupPulse - Time.deltaTime * 4.5f); float sc = 1f + popupPulse * 0.35f; popup.transform.localScale = Vector3.one * sc; }
        }

        void TickSelect()
        {
            bool began = false, held = false, ended = false;
            Vector2 pos = Vector2.zero;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                began = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            else
            {
                pos = Input.mousePosition;
                began = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                ended = Input.GetMouseButtonUp(0);
            }
            if (began)
            {
                selectDragging = true;
                selectStart = selectLast = pos;
                selectOnModel = modelZone != null && RectTransformUtility.RectangleContainsScreenPoint(modelZone, pos, null);
                if (selectOnModel && stage != null) stage.BeginDrag();
            }
            if (selectDragging && held && selectOnModel && stage != null)
            {
                stage.Drag((pos.x - selectLast.x) * 0.45f);
                selectLast = pos;
            }
            if (ended && selectDragging)
            {
                Vector2 delta = pos - selectStart;
                if (!selectOnModel && Mathf.Abs(delta.x) > 90f && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f)
                    StepHero(delta.x < 0f ? 1 : -1);
                if (selectOnModel && stage != null) stage.EndDrag();
                selectDragging = false;
            }
        }

        /// <summary>True when a press at this screen X/Y lands on the shield button, so it doesn't start a steer drag.</summary>
        public static bool PointerOverButton(float x)
        {
            if (shieldRect == null) return false;
            Vector2 pos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            return RectTransformUtility.RectangleContainsScreenPoint(shieldRect, pos, null);
        }

        // ================= Tuning screen =================
        enum TuneKind { Mult, Percent, Int, CapOrOff }

        class TuneSpec
        {
            public string Label;
            public Func<float> Get;
            public Action<float> Set;
            public float Step, Min, Max;
            public TuneKind Kind;
        }

        static TuneSpec Mult(string label, Func<float> get, Action<float> set, float step, float min, float max) =>
            new TuneSpec { Label = label, Get = get, Set = set, Step = step, Min = min, Max = max, Kind = TuneKind.Mult };
        static TuneSpec Pct(string label, Func<float> get, Action<float> set, float step) =>
            new TuneSpec { Label = label, Get = get, Set = set, Step = step, Min = 0f, Max = 1f, Kind = TuneKind.Percent };
        static TuneSpec IntSpec(string label, Func<float> get, Action<float> set, float step, float min, float max) =>
            new TuneSpec { Label = label, Get = get, Set = set, Step = step, Min = min, Max = max, Kind = TuneKind.Int };
        static TuneSpec CapOrOff(string label, Func<float> get, Action<float> set, float step, float max) =>
            new TuneSpec { Label = label, Get = get, Set = set, Step = step, Min = 0f, Max = max, Kind = TuneKind.CapOrOff };

        // Get/Set always go through Tuning.Current (never a cached local) so rows stay correct after
        // RESET/PASTE swap in a brand new TuningSettings instance underneath them.
        static (string, TuneSpec[])[] Sections() => new (string, TuneSpec[])[]
        {
            ("WEAPONS", new[] {
                Mult("Rook fire rate", () => Tuning.Current.rookFireRateMult, v => Tuning.Current.rookFireRateMult = v, 0.1f, 0f, 5f),
                Mult("Soldier fire rate", () => Tuning.Current.soldierFireRateMult, v => Tuning.Current.soldierFireRateMult = v, 0.1f, 0f, 5f),
                Mult("Damage per hit", () => Tuning.Current.damageMult, v => Tuning.Current.damageMult = v, 0.1f, 0f, 5f),
                CapOrOff("Max shots/sec", () => Tuning.Current.maxShotsPerSecond, v => Tuning.Current.maxShotsPerSecond = v, 5f, 500f),
                Pct("Soldiers that shoot", () => Tuning.Current.soldierShootFraction, v => Tuning.Current.soldierShootFraction = v, 0.05f),
            }),
            ("ZOMBIES", new[] {
                Mult("Zombie count", () => Tuning.Current.zombieCountMult, v => Tuning.Current.zombieCountMult = v, 0.1f, 0f, 5f),
                IntSpec("Extra zombie waves", () => Tuning.Current.extraZombieWaves, v => Tuning.Current.extraZombieWaves = Mathf.RoundToInt(v), 1f, 0f, 20f),
                Mult("Zombie HP", () => Tuning.Current.zombieHpMult, v => Tuning.Current.zombieHpMult = v, 0.1f, 0f, 10f),
                Mult("Zombie speed", () => Tuning.Current.zombieSpeedMult, v => Tuning.Current.zombieSpeedMult = v, 0.05f, 0.2f, 5f),
                Mult("Zombie touch damage", () => Tuning.Current.zombieContactDamageMult, v => Tuning.Current.zombieContactDamageMult = v, 0.25f, 0f, 10f),
                Pct("Waves scale w/ squad", () => Tuning.Current.waveSquadScale, v => Tuning.Current.waveSquadScale = v, 0.05f),
            }),
            ("GATES", new[] {
                Mult("Positive gate value", () => Tuning.Current.positiveGateMult, v => Tuning.Current.positiveGateMult = v, 0.1f, 0f, 5f),
                Mult("Multiplier gate factor", () => Tuning.Current.multiplierGateFactorMult, v => Tuning.Current.multiplierGateFactorMult = v, 0.1f, 0f, 5f),
                Pct("Bonus to negative chance", () => Tuning.Current.negativeGateChance, v => Tuning.Current.negativeGateChance = v, 0.05f),
                Mult("Negative gate value", () => Tuning.Current.negativeGateMult, v => Tuning.Current.negativeGateMult = v, 0.1f, 0f, 5f),
                Mult("Counter gate depth", () => Tuning.Current.counterGateDepthMult, v => Tuning.Current.counterGateDepthMult = v, 0.1f, 0f, 5f),
            }),
            ("SQUAD", new[] {
                Mult("Squad cap", () => Tuning.Current.squadCapMult, v => Tuning.Current.squadCapMult = v, 0.1f, 0.2f, 5f),
                IntSpec("Starting squad size", () => Tuning.Current.startingSquadSize, v => Tuning.Current.startingSquadSize = Mathf.RoundToInt(v), 1f, 1f, 80f),
            }),
            ("BOSS", new[] {
                Mult("Boss HP", () => Tuning.Current.bossHpMult, v => Tuning.Current.bossHpMult = v, 0.1f, 0.1f, 10f),
                Mult("Boss speed", () => Tuning.Current.bossSpeedMult, v => Tuning.Current.bossSpeedMult = v, 0.1f, 0.1f, 5f),
            }),
            ("PROGRESSION", new[] {
                IntSpec("Starting level", () => Tuning.Current.startingLevel, v => Tuning.Current.startingLevel = Mathf.RoundToInt(v), 1f, 1f, 200f),
            }),
        };

        static string FormatValue(TuneSpec s)
        {
            float v = s.Get();
            switch (s.Kind)
            {
                case TuneKind.Percent: return Mathf.RoundToInt(v * 100f) + "%";
                case TuneKind.Int: return Mathf.RoundToInt(v).ToString();
                case TuneKind.CapOrOff: return v <= 0f ? "OFF" : Mathf.RoundToInt(v).ToString();
                default: return v.ToString("0.00") + "x";
            }
        }

        void BuildTuning(Transform root)
        {
            tuning = Img("Tuning", root, new Color(0.04f, 0.06f, 0.07f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Txt("Title", tuning.transform, "TUNING", 56, new Color(0.22f, 0.82f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -120), new Vector2(0, -30));
            var back = Img("Back", tuning.transform, Palette.TealDark, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -120), new Vector2(190, -30));
            back.gameObject.AddComponent<Button>().onClick.AddListener(HideTuning);
            Txt("Label", back.transform, "BACK", 32, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var viewportImg = Img("Viewport", tuning.transform, new Color(0, 0, 0, 0.001f), Vector2.zero, Vector2.one, new Vector2(20, 170), new Vector2(-20, -150));
            var viewportGo = viewportImg.gameObject;
            viewportGo.AddComponent<RectMask2D>();
            var scrollRect = viewportGo.AddComponent<ScrollRect>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            tuningContent = (RectTransform)contentGo.transform;
            tuningContent.anchorMin = new Vector2(0, 1); tuningContent.anchorMax = new Vector2(1, 1);
            tuningContent.pivot = new Vector2(0.5f, 1f);
            tuningContent.anchoredPosition = Vector2.zero;
            tuningContent.sizeDelta = Vector2.zero;

            scrollRect.viewport = (RectTransform)viewportGo.transform;
            scrollRect.content = tuningContent;
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            BuildTuneRows();
            BuildTuningButtons();
            toastText = Txt("Toast", tuning.transform, "", 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 140), new Vector2(0, 170));
            var toastC = toastText.color; toastC.a = 0f; toastText.color = toastC;
            tuning.SetActive(false);
        }

        void BuildTuneRows()
        {
            float y = 0f;
            foreach (var (header, specs) in Sections())
            {
                AddTuneHeader(header, ref y);
                foreach (var spec in specs) AddTuneRow(spec, ref y);
            }
            tuningContent.sizeDelta = new Vector2(0, y + 30f);
        }

        void AddTuneHeader(string text, ref float y)
        {
            const float h = 64f;
            float top = -y, bottom = -(y + h);
            Txt("Header", tuningContent, text, 32, Palette.Orange, TextAnchor.LowerLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, bottom), new Vector2(-24, top));
            y += h;
        }

        void AddTuneRow(TuneSpec spec, ref float y)
        {
            const float h = 104f;
            float top = -y, bottom = -(y + h);
            var rowRt = Rect(spec.Label, tuningContent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, bottom), new Vector2(0, top));
            Txt("Label", rowRt, spec.Label, 28, Color.white, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-455, 0));
            // Value column sits between the label and the -/+ buttons, anchored to the row's right edge.
            Img("ValueBg", rowRt, new Color(1f, 1f, 1f, 0.08f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-440, -36), new Vector2(-285, 36));
            var valueText = Txt("Value", rowRt, FormatValue(spec), 34, Palette.Orange, TextAnchor.MiddleCenter, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-440, 0), new Vector2(-285, 0));
            EdgeBtn("Minus", rowRt, "-", Palette.TealDark, new Vector2(1, 0.5f), new Vector2(-220, 0), new Vector2(100, 80), () => AdjustTune(spec, -1, valueText));
            EdgeBtn("Plus", rowRt, "+", Palette.Teal, new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(100, 80), () => AdjustTune(spec, 1, valueText));
            tuneRows.Add((spec, valueText));
            y += h;
        }

        void AdjustTune(TuneSpec spec, int dir, Text valueText)
        {
            float v = Mathf.Clamp(spec.Get() + dir * spec.Step, spec.Min, spec.Max);
            spec.Set(v);
            valueText.text = FormatValue(spec);
            Tuning.Save();
        }

        void RefreshTuningValues()
        {
            foreach (var (spec, text) in tuneRows) text.text = FormatValue(spec);
        }

        void BuildTuningButtons()
        {
            string[] labels = { "RESET", "PASTE", "COPY", "PLAY" };
            Color[] colors = { new Color(0.45f, 0.27f, 0.25f), Palette.TealDark, Palette.Teal, Palette.Orange };
            UnityEngine.Events.UnityAction[] actions = { OnResetTuning, OnPasteTuning, OnCopyTuning, OnPlayTuned };
            const float h = 110f, margin = 14f;
            for (int i = 0; i < labels.Length; i++)
            {
                float x0 = i / 4f, x1 = (i + 1) / 4f;
                var img = Img("TuneBtn" + i, tuning.transform, colors[i], new Vector2(x0, 0), new Vector2(x1, 0), new Vector2(margin, 24), new Vector2(-margin, 24 + h));
                img.gameObject.AddComponent<Button>().onClick.AddListener(actions[i]);
                Txt("Label", img.transform, labels[i], 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }

        void ShowToast(string msg) { if (toastText == null) return; toastText.text = msg; var c = toastText.color; c.a = 1f; toastText.color = c; toastT = 1.4f; }

        void OnCopyTuning() { Tuning.CopyToClipboard(); ShowToast("Copied!"); }

        void OnPasteTuning()
        {
            if (Tuning.TryPasteFromClipboard(out var error)) { RefreshTuningValues(); ShowToast("Pasted!"); }
            else ShowToast(error ?? "Paste failed");
        }

        void OnResetTuning() { Tuning.Reset(); RefreshTuningValues(); ShowToast("Reset"); }

        void OnPlayTuned()
        {
            GameManager.I.SetLevelNumber(Tuning.Current.startingLevel);
            GameManager.I.BuildRun();
            GameManager.I.StartRun();
            tuning.SetActive(false);
        }

        public void ShowTuning()
        {
            if (menu != null) menu.SetActive(false);
            if (hud != null) hud.SetActive(false);
            if (end != null) end.SetActive(false);
            if (select != null) select.SetActive(false);
            if (stage != null) stage.Hide();
            RefreshTuningValues();
            tuning.SetActive(true);
        }

        void HideTuning()
        {
            tuning.SetActive(false);
            ShowMenu();
        }
    }

    /// <summary>Keeps HUD inside the device safe area (notches, home indicator).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea; if (sa == last || Screen.width == 0) return; last = sa;
            var r = (RectTransform)transform;
            // Clamp01: Screen.safeArea has been observed to report a frame or two of stale/mismatched values
            // around iOS startup/orientation changes, which without clamping pushes this rect's anchor past
            // the real screen edge - and every corner-anchored HUD element (e.g. the bottom-right shield
            // button) along with it, reading as "cut off" even though the element's own offsets are fine.
            r.anchorMin = new Vector2(Mathf.Clamp01(sa.xMin / Screen.width), Mathf.Clamp01(sa.yMin / Screen.height));
            r.anchorMax = new Vector2(Mathf.Clamp01(sa.xMax / Screen.width), Mathf.Clamp01(sa.yMax / Screen.height));
        }
    }
}
