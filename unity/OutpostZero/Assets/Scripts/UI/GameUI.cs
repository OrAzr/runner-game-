using UnityEngine;
using UnityEngine.UI;

namespace OutpostZero
{
    /// <summary>Runtime-built uGUI: title menu, HUD (progress, weapon timer, boss HP, shield button), popups, end screen.</summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI I { get; private set; }
        static RectTransform shieldRect; static Canvas canvasStatic;

        Canvas canvas; GameObject menu, hud, end;
        Image progressFill, bossFill, weaponFill; GameObject bossBar, weaponBox;
        Text weaponName, bossHpText, shieldText, endTitle, endStats, popup; float popupT;
        Font font;

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
            var track = Img("Progress", hud.transform, new Color(0, 0, 0, 0.45f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(340, -62), new Vector2(-30, -38));
            progressFill = Img("Fill", track.transform, Palette.Teal, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            weaponBox = Img("Weapon", hud.transform, new Color(0.91f, 0.47f, 0.17f, 0.92f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -190), new Vector2(380, -100)).gameObject;
            weaponName = Txt("Name", weaponBox.transform, "SHOTGUN", 34, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-10, 0));
            weaponFill = Img("Time", weaponBox.transform, new Color(0.1f, 0.1f, 0.1f), Vector2.zero, new Vector2(1, 0), new Vector2(20, 10), new Vector2(-20, 20));
            bossBar = Rect("Boss", hud.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -200), new Vector2(-30, -100)).gameObject;
            Txt("Title", bossBar.transform, "BRUTE BOSS", 36, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bossHpText = Txt("Hp", bossBar.transform, "", 36, Color.white, TextAnchor.UpperRight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bt = Img("Track", bossBar.transform, new Color(0, 0, 0, 0.6f), Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 44));
            bossFill = Img("Fill", bt.transform, new Color(0.85f, 0.2f, 0.15f), Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            var shield = Img("ShieldButton", hud.transform, Palette.Teal, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-250, 50), new Vector2(-40, 260));
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

            // End
            end = Img("End", root, new Color(0.04f, 0.06f, 0.07f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            endTitle = Txt("Title", end.transform, "VICTORY", 120, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.55f), new Vector2(1, 0.68f), Vector2.zero, Vector2.zero);
            endStats = Txt("Stats", end.transform, "", 38, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.48f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
            Btn("Again", end.transform, "PLAY AGAIN", Palette.Orange, new Vector2(0, -150), new Vector2(520, 130), () => GameManager.I.StartRun());
            end.SetActive(false);
        }

        public void ShowMenu() { menu.SetActive(true); end.SetActive(false); }
        public void ShowHud() { menu.SetActive(false); end.SetActive(false); }
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
            popup.text = text; popup.color = good ? new Color(0.5f, 0.82f, 1f) : new Color(1f, 0.42f, 0.36f); popupT = 0.9f;
        }

        void Update()
        {
            var gm = GameManager.I; if (gm == null || gm.Squad == null) return;
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(gm.Distance / gm.level.bossDistance), 1);
            var sh = gm.Squad.Shooter;
            weaponBox.SetActive(sh.Current != WeaponType.AssaultRifle);
            if (weaponBox.activeSelf)
            {
                weaponName.text = WeaponDef.Get(sh.Current).label;
                weaponFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(sh.PickupTimeLeft / gm.level.pickupDuration), 0);
            }
            bossBar.SetActive(gm.Boss != null);
            if (gm.Boss != null) { bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(gm.Boss.hp / gm.Boss.maxHp), 1); bossHpText.text = Mathf.Max(0, Mathf.CeilToInt(gm.Boss.hp)).ToString(); }
            var rook = gm.Squad.Rook;
            shieldText.text = rook.ShieldActive ? "SHIELD\nACTIVE" : rook.Cooldown > 0 ? $"SHIELD\n{Mathf.CeilToInt(rook.Cooldown)}s" : "SHIELD\nREADY";
            if (popupT > 0) { popupT -= Time.deltaTime; var c = popup.color; c.a = Mathf.Clamp01(popupT / 0.4f); popup.color = c; }
        }

        /// <summary>True when a press at this screen X/Y lands on the shield button, so it doesn't start a steer drag.</summary>
        public static bool PointerOverButton(float x)
        {
            if (shieldRect == null) return false;
            Vector2 pos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            return RectTransformUtility.RectangleContainsScreenPoint(shieldRect, pos, null);
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
            r.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            r.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        }
    }
}
