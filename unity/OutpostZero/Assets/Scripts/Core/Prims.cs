using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Builds low-poly placeholder geometry from Unity primitives so the project needs no art assets.</summary>
    public static class Prims
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader litShader, unlitShader, errorShader;
        static Material builtinFallback;
        static bool missingShaderLogged;

        /// <summary>First shader that exists. Never returns a shader that would make new Material throw; logs once if every name misses.</summary>
        public static Shader FindShader(params string[] names)
        {
            if (names != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.IsNullOrEmpty(names[i])) continue;
                    var found = Shader.Find(names[i]);
                    if (found != null) return found;
                }
            }
            if (!missingShaderLogged)
            {
                missingShaderLogged = true;
                Debug.LogError("[Outpost Zero] Shader.Find missed every requested shader. Using Hidden/InternalErrorShader.");
            }
            if (errorShader == null) errorShader = Shader.Find("Hidden/InternalErrorShader");
            return errorShader;
        }

        static Shader Lit()
        {
            if (litShader == null)
                litShader = FindShader("Universal Render Pipeline/Lit", "Standard", "Legacy Shaders/Diffuse", "Unlit/Color", "Sprites/Default");
            return litShader;
        }

        static Material MaterialFor(Shader shader, Color c)
        {
            if (shader != null) return new Material(shader) { color = c };
            if (builtinFallback == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var src = temp.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(temp);
                if (src != null) builtinFallback = new Material(src);
            }
            if (builtinFallback == null) return null;
            return new Material(builtinFallback) { color = c };
        }

        public static Material Mat(Color c, bool unique = false)
        {
            if (!unique && cache.TryGetValue(c, out var m) && m != null) return m;
            m = MaterialFor(Lit(), c);
            if (m == null) return builtinFallback;
            if (c.a < 1f) MakeTransparent(m);
            if (!unique) cache[c] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            if (unlitShader == null)
                unlitShader = FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default", "Legacy Shaders/Diffuse", "Standard");
            return MaterialFor(unlitShader, c) ?? builtinFallback;
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
