using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Parsed gate operation: "+5", "-10", "x2", "/2", or a shootable counter "#-18".</summary>
    public struct GateOp
    {
        public char op; public float n; public string Text; public bool Counter;
        public bool Good => Counter ? n >= 0 : op == '+' || op == 'x';
        public static string Format(float v) { int i = Mathf.RoundToInt(v); return i > 0 ? "+" + i : i.ToString(); }
        public static GateOp Parse(string s)
        {
            s = s.Trim();
            if (s.Length > 0 && s[0] == '#')
            {
                float.TryParse(s.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                return new GateOp { op = '#', n = v, Counter = true, Text = Format(v) };
            }
            char c = s[0];
            if (c == '*' || c == 'X') c = 'x'; if (c == '÷') c = '/';
            float.TryParse(s.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n);
            return new GateOp { op = c, n = n, Text = c == '/' ? "÷" + s.Substring(1) : s };
        }
        public int Apply(int count)
        {
            switch (op)
            {
                case '+': return count + Mathf.RoundToInt(n);
                case '-': return count - Mathf.RoundToInt(n);
                case 'x': return Mathf.RoundToInt(count * n);
                case '/': return n > 0 ? Mathf.FloorToInt(count / n) : count;
            }
            return count;
        }
    }

    /// <summary>A pair of gates. Plain +/-/x gates resolve on contact. Counter gates ("#-20")
    /// start negative, climb as bullets hit them, and turn from red to blue past zero.</summary>
    public class Gate : MonoBehaviour
    {
        public static readonly List<Gate> All = new List<Gate>();

        class Side
        {
            public GateOp op;
            public float value;
            public float cx;
            public Renderer panel;
            public Renderer[] frame;
            public TextMesh label;
            public float bump, flash;
            public int shown;
            public bool dirty;
            public float labelAt;
            public Renderer labelRenderer;
        }

        Side left, right; bool used; float halfWidth;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Gate Create(Transform parent, GateDef def, float halfWidth)
        {
            var go = new GameObject($"Gate_{def.distance}"); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, 0, def.distance);
            var g = go.AddComponent<Gate>();
            g.halfWidth = halfWidth;
            g.left = g.BuildSide(-1, GateOp.Parse(def.left), halfWidth);
            g.right = g.BuildSide(1, GateOp.Parse(def.right), halfWidth);
            return g;
        }

        Side BuildSide(int side, GateOp op, float hw)
        {
            float cx = side * hw / 2f;
            var s = new Side { op = op, value = op.n, cx = cx, shown = Mathf.RoundToInt(op.n) };
            Color col = op.Counter ? CounterColor(op.n) : (op.Good ? Palette.GateGood : Palette.GateBad);
            var panel = Prims.Part(PrimitiveType.Cube, transform, new Vector3(cx, 1.45f, 0), new Vector3(hw - 0.3f, 2.4f, 0.05f), Prims.Mat(col, true));
            s.panel = panel.GetComponent<Renderer>();
            var postM = Prims.Unlit(new Color(col.r, col.g, col.b, 1f));
            var a = Prims.Part(PrimitiveType.Cube, transform, new Vector3(side * 0.15f, 1.45f, 0), new Vector3(0.18f, 2.9f, 0.18f), postM);
            var b = Prims.Part(PrimitiveType.Cube, transform, new Vector3(side * (hw - 0.15f), 1.45f, 0), new Vector3(0.18f, 2.9f, 0.18f), postM);
            var top = Prims.Part(PrimitiveType.Cube, transform, new Vector3(cx, 2.85f, 0), new Vector3(hw - 0.1f, 0.16f, 0.18f), postM);
            s.frame = new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>(), top.GetComponent<Renderer>() };
            string text = op.Counter ? GateOp.Format(op.n) : op.Text;
            s.label = Prims.Label3D(transform, text, new Vector3(cx, 1.55f, -0.15f), 1.45f, Color.white);
            s.labelRenderer = s.label.GetComponent<MeshRenderer>();
            return s;
        }

        public static bool TryHit(Vector3 prev, Vector3 pos, int points)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var g = All[i]; if (g == null || g.used || points == 0) continue;
                float gz = g.transform.position.z;
                bool crossed = (prev.z - gz) * (pos.z - gz) <= 0f || Mathf.Abs(pos.z - gz) < 0.35f;
                if (!crossed) continue;
                if (g.HitSide(g.left, pos.x, points, pos)) return true;
                if (g.HitSide(g.right, pos.x, points, pos)) return true;
            }
            return false;
        }

        public static void FlameTick(Vector3 squad, float range, float points)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var g = All[i]; if (g == null || g.used) continue;
                float dz = g.transform.position.z - squad.z;
                if (dz < -0.3f || dz > range) continue;
                g.FlameSide(g.left, squad.x, points);
                g.FlameSide(g.right, squad.x, points);
            }
        }

        bool HitSide(Side s, float x, int points, Vector3 pos)
        {
            if (!s.op.Counter) return false;
            if (Mathf.Abs(x - s.cx) > (halfWidth - 0.3f) * 0.5f + 0.05f) return false;
            Add(s, points, pos); return true;
        }

        void FlameSide(Side s, float squadX, float points)
        {
            if (!s.op.Counter || Mathf.Abs(squadX - s.cx) > halfWidth * 0.42f) return;
            Add(s, points, new Vector3(s.cx, 1.4f, transform.position.z));
        }

        void Add(Side s, float points, Vector3 pos)
        {
            int before = Mathf.RoundToInt(s.value);
            s.value += points;
            s.bump = Mathf.Min(1f, s.bump + 0.62f);
            s.flash = 0.1f;
            var c = CounterColor(s.value);
            SetColor(s.panel, c);
            if (Mathf.RoundToInt(s.value) != before) s.dirty = true;
            float pitch = Mathf.Clamp(1.08f + s.value * 0.02f, 0.7f, 1.9f);
            AudioManager.I?.Play(Sfx.GateTick, 0.34f, pitch, 0.042f);
            Effects.Spark(pos, s.value >= 0f ? new Color(0.62f, 0.83f, 1f) : new Color(1f, 0.69f, 0.64f));
        }

        // Text and frame/outline renderers only turn on once the gate is within this range of the camera.
        // The panel itself keeps using the shader's own built-in fog (see FogVisibility below for the label's
        // extra fade), but the frame posts/top use an Unlit material that never samples fog at all, and
        // relying on label alpha alone still left both "floating in the gray sky" long before the road under
        // them was visible - a hard distance cutoff is correct regardless of which shader a part uses.
        const float VisibleRange = 62f;

        bool IsVisible()
        {
            var cam = Camera.main;
            if (cam == null) return true;
            return Vector3.Distance(cam.transform.position, transform.position) <= VisibleRange;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool visible = IsVisible();
            TickSide(left, dt, visible); TickSide(right, dt, visible);
            var gm = GameManager.I; if (used || gm == null || !gm.IsPlaying) return;
            var sq = gm.Squad; if (sq == null) return;
            if (sq.transform.position.z >= transform.position.z)
            {
                used = true;
                bool isLeft = sq.transform.position.x < 0f;
                var side = isLeft ? left : right;
                if (side.op.Counter) sq.ApplyCounter(Mathf.RoundToInt(side.value));
                else sq.ApplyGate(side.op);
                var chosen = isLeft ? left : right; var other = isLeft ? right : left;
                var c = chosen.panel.material.color; c.a = 0.85f; chosen.panel.material.color = c;
                var o = other.panel.material.color; o.a = 0.12f; other.panel.material.color = o;
            }
        }

        void TickSide(Side s, float dt, bool visible)
        {
            if (s.frame != null) foreach (var r in s.frame) if (r) r.enabled = visible;
            if (s.labelRenderer) s.labelRenderer.enabled = visible;
            if (s.bump > 0f)
            {
                s.bump = Mathf.Max(0f, s.bump - dt * 4.2f);
                float sc = 1f + s.bump * 0.48f;
                s.label.transform.localScale = new Vector3(sc, sc, sc);
            }
            if (s.flash > 0f)
            {
                s.flash = Mathf.Max(0f, s.flash - dt);
                var c = s.panel.material.color; c.a = Mathf.Min(0.92f, 0.4f + s.flash * 4f); s.panel.material.color = c;
            }
            if (s.dirty && Time.time - s.labelAt > 0.045f) Paint(s);
            // On top of the hard VisibleRange cutoff above, fade the label's own alpha by distance so it
            // eases in rather than popping at full opacity the instant it crosses the range.
            var lc = s.label.color; lc.a = FogVisibility(s.label.transform.position); s.label.color = lc;
        }

        static float FogVisibility(Vector3 worldPos)
        {
            if (!RenderSettings.fog) return 1f;
            var cam = Camera.main;
            if (cam == null) return 1f;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            if (end <= start) return 1f;
            float dist = Vector3.Distance(cam.transform.position, worldPos);
            return 1f - Mathf.Clamp01((dist - start) / (end - start));
        }

        void Paint(Side s)
        {
            s.dirty = false; s.labelAt = Time.time; s.shown = Mathf.RoundToInt(s.value);
            var c = CounterColor(s.value);
            SetColor(s.panel, c);
            foreach (var r in s.frame) if (r) SetColor(r, new Color(c.r, c.g, c.b, 1f));
            s.label.text = GateOp.Format(s.value);
            s.label.color = Color.white;
        }

        static void SetColor(Renderer r, Color c)
        {
            if (!r) return;
            var m = r.material; m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        static Color CounterColor(float n)
        {
            float u = Mathf.Clamp01(Mathf.InverseLerp(-4f, 6f, n));
            float t = u * u * (3f - 2f * u);
            var bad = new Color(1f, 0.23f, 0.23f, 0.4f);
            var good = new Color(0.18f, 0.55f, 1f, 0.4f);
            return Color.Lerp(bad, good, t);
        }
    }
}
