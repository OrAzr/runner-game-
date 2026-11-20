using UnityEngine;

namespace OutpostZero
{
    /// <summary>Parsed gate operation: "+5", "-10", "x2", "/2" (also accepts "÷2" and "*2").</summary>
    public struct GateOp
    {
        public char op; public float n; public string Text;
        public bool Good => op == '+' || op == 'x';
        public static GateOp Parse(string s)
        {
            s = s.Trim(); char c = s[0];
            if (c == '*' || c == 'X') c = 'x'; if (c == '÷') c = '/';
            float.TryParse(s.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
            return new GateOp { op = c, n = v, Text = c == '/' ? "÷" + s.Substring(1) : s };
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

    /// <summary>A pair of gates spanning the road. The squad gets the side its centre is on when it crosses.</summary>
    public class Gate : MonoBehaviour
    {
        GateOp left, right; bool used; Renderer leftPanel, rightPanel;

        public static Gate Create(Transform parent, GateDef def, float halfWidth)
        {
            var go = new GameObject($"Gate_{def.distance}"); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, 0, def.distance);
            var g = go.AddComponent<Gate>();
            g.left = GateOp.Parse(def.left); g.right = GateOp.Parse(def.right);
            g.leftPanel = g.BuildSide(-1, g.left, halfWidth); g.rightPanel = g.BuildSide(1, g.right, halfWidth);
            return g;
        }

        Renderer BuildSide(int side, GateOp op, float hw)
        {
            var t = transform; float cx = side * hw / 2f;
            Color panelC = op.Good ? Palette.GateGood : Palette.GateBad;
            Color postC = op.Good ? new Color(0.5f, 0.77f, 1f) : new Color(1f, 0.54f, 0.54f);
            var panel = Prims.Part(PrimitiveType.Cube, t, new Vector3(cx, 1.4f, 0), new Vector3(hw - 0.3f, 2.2f, 0.05f), Prims.Mat(panelC, true));
            var postM = Prims.Unlit(postC);
            Prims.Part(PrimitiveType.Cube, t, new Vector3(side * 0.15f, 1.4f, 0), new Vector3(0.22f, 2.8f, 0.22f), postM);
            Prims.Part(PrimitiveType.Cube, t, new Vector3(side * (hw - 0.15f), 1.4f, 0), new Vector3(0.22f, 2.8f, 0.22f), postM);
            Prims.Part(PrimitiveType.Cube, t, new Vector3(cx, 2.75f, 0), new Vector3(hw - 0.1f, 0.18f, 0.22f), postM);
            Prims.Label3D(t, op.Text, new Vector3(cx, 1.5f, -0.1f), 1.3f, Color.white);
            return panel.GetComponent<Renderer>();
        }

        void Update()
        {
            var gm = GameManager.I; if (used || gm == null || !gm.IsPlaying) return;
            var sq = gm.Squad;
            if (sq.transform.position.z >= transform.position.z)
            {
                used = true;
                bool isLeft = sq.transform.position.x < 0;
                sq.ApplyGate(isLeft ? left : right);
                var chosen = isLeft ? leftPanel : rightPanel; var other = isLeft ? rightPanel : leftPanel;
                var c = chosen.material.color; c.a = 0.8f; chosen.material.color = c;
                var o = other.material.color; o.a = 0.12f; other.material.color = o;
            }
        }
    }
}
