using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Builds low-poly placeholder geometry from Unity primitives so the project needs no art assets.</summary>
    public static class Prims
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader litShader, unlitShader, additiveShader, errorShader;
        static Material builtinFallback, additiveMat;
        static Texture2D softCircle;
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

        /// <summary>One shared additive material. Texture is generated here, never loaded from the project.</summary>
        public static Material SharedAdditive()
        {
            if (additiveMat != null) return additiveMat;
            if (additiveShader == null)
                additiveShader = FindShader(
                    "Legacy Shaders/Particles/Additive",
                    "Particles/Additive",
                    "Mobile/Particles/Additive",
                    "Legacy Shaders/Particles/Alpha Blended",
                    "Particles/Standard Unlit",
                    "Universal Render Pipeline/Particles/Unlit",
                    "Sprites/Default",
                    "Unlit/Color");
            additiveMat = MaterialFor(additiveShader, Color.white);
            if (additiveMat == null) return builtinFallback;
            var tex = SoftCircle();
            if (tex != null) additiveMat.SetTexture("_MainTex", tex);
            additiveMat.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.8f));
            additiveMat.SetColor("_Color", Color.white);
            return additiveMat;
        }

        public static string ShaderName(Material m)
        {
            if (m == null || m.shader == null) return "null";
            return m.shader.name;
        }

        static Texture2D SoftCircle()
        {
            if (softCircle != null) return softCircle;
            const int n = 64;
            softCircle = new Texture2D(n, n, TextureFormat.RGBA32, false);
            softCircle.wrapMode = TextureWrapMode.Clamp;
            softCircle.filterMode = FilterMode.Bilinear;
            var px = new Color[n * n];
            float h = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - h) / h, dy = (y - h) / h;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a *= a;
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softCircle.SetPixels(px);
            softCircle.Apply(false, true);
            return softCircle;
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

        /// <summary>World-space bounds across every renderer under root, safe to call the instant after
        /// Instantiate. SkinnedMeshRenderer.bounds reflects the last-evaluated pose, which may still be stale
        /// (or default) before the skin has been sampled once; this instead transforms localBounds (the
        /// authored, pose-independent bind-pose box) by the renderer's current matrix.</summary>
        public static Bounds? CombinedBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            Bounds result = default;
            bool has = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds b;
                if (renderers[i] is SkinnedMeshRenderer smr)
                {
                    var lb = smr.localBounds;
                    var m = smr.transform.localToWorldMatrix;
                    Vector3 c = lb.center, e = lb.extents;
                    b = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                                b.Encapsulate(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(sx, sy, sz))));
                }
                else b = renderers[i].bounds;
                if (!has) { result = b; has = true; } else result.Encapsulate(b);
            }
            return has ? result : (Bounds?)null;
        }

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
