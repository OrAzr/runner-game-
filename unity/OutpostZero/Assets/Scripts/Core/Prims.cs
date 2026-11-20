using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Builds low-poly placeholder geometry from Unity primitives so the project needs no art assets.</summary>
    public static class Prims
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader litShader;

        static Shader Lit()
        {
            if (litShader == null)
                { litShader = Shader.Find("Universal Render Pipeline/Lit"); if (litShader == null) litShader = Shader.Find("Standard"); }
            return litShader;
        }

        public static Material Mat(Color c, bool unique = false)
        {
            if (!unique && cache.TryGetValue(c, out var m)) return m;
            m = new Material(Lit()) { color = c };
            if (c.a < 1f) MakeTransparent(m);
            if (!unique) cache[c] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit"); if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { color = c };
            return m;
        }

        static void MakeTransparent(Material m)
        {
            // Built-in Standard shader transparent setup (ignored harmlessly by other shaders)
            m.SetFloat("_Mode", 3); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.renderQueue = 3000;
            m.SetFloat("_Surface", 1); // URP
        }

        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public static GameObject Box(Transform p, Vector3 pos, Vector3 scale, Color c) => Part(PrimitiveType.Cube, p, pos, scale, Mat(c));

        public static Font UIFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return f;
        }

        public static TextMesh Label3D(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.fontSize = 96; tm.characterSize = size * 0.1f; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center; tm.color = color; tm.fontStyle = FontStyle.Bold;
            var f = UIFont(); if (f != null) { tm.font = f; go.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
            go.AddComponent<FaceCamera>();
            return tm;
        }
    }

    /// <summary>Billboard helper for 3D labels.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; }
    }
}
