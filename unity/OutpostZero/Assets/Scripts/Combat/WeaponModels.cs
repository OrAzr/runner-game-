using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>Loads the Meshy weapon FBX models from Resources, wiring a runtime material (Standard shader,
    /// never the FBX-imported one) so the look does not depend on import settings. Callers fall back to the
    /// code-built primitive guns when a model is missing, so a partial Resources folder never breaks a weapon.</summary>
    public static class WeaponModels
    {
        const string ResourceRoot = "Models/Weapons/";
        static Shader standardShader;
        static bool shaderMissingLogged;

        /// <summary>Instantiates the named model under parent, scaled so its long axis equals targetLength, with
        /// its texture applied via a freshly made Standard material (never the FBX-imported one). tipLocal comes
        /// back as the muzzle-end position in parent's local space, for placing extras like a nozzle glow and
        /// the Muzzle socket itself. Returns null (and destroys anything it created) if the model, its
        /// renderers, or its vertex data are missing, so a bad import never breaks a weapon.</summary>
        public static GameObject TryBuild(string resourceName, Transform parent, float targetLength, out Vector3 tipLocal)
        {
            tipLocal = new Vector3(0f, 0f, targetLength * 0.5f);
            var prefab = Resources.Load<GameObject>(ResourceRoot + resourceName);
            if (prefab == null)
            {
                Debug.LogWarning("[Outpost Zero] Weapon model resource missing: " + ResourceRoot + resourceName + "; falling back to the primitive gun.");
                return null;
            }

            var go = Object.Instantiate(prefab);
            go.name = resourceName;
            go.transform.SetParent(null, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Outpost Zero] Weapon model " + resourceName + " has no renderers; falling back to the primitive gun.");
                Object.Destroy(go);
                return null;
            }

            var mat = MakeMaterial(resourceName);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = mat;

            // Measured from the real instantiated mesh every time, not assumed: the exported FBX's own axis
            // convention (which raw axis the barrel runs along, which end the muzzle is on, which way the
            // magazine hangs) is specific to whatever tool produced it and isn't safe to hardcode - guessing
            // wrong once already sent Rook's rifle backwards over his shoulder (brief 14) and then pointing
            // straight down (brief 16: the Muzzle empty's rotation was locked correctly, but the visible mesh
            // under it was anchored with a wrong fixed axis guess, so the rendered barrel didn't follow it).
            var points = CollectWorldVertices(go);
            if (points.Count == 0)
            {
                Debug.LogWarning("[Outpost Zero] Weapon model " + resourceName + " has no readable vertex data; falling back to the primitive gun.");
                Object.Destroy(go);
                return null;
            }

            Vector3 lo = points[0], hi = points[0];
            for (int i = 1; i < points.Count; i++) { lo = Vector3.Min(lo, points[i]); hi = Vector3.Max(hi, points[i]); }
            Vector3 size = hi - lo;

            // The barrel is overwhelmingly the model's longest dimension; of the remaining two axes, the
            // taller one (receiver body + magazine/sight bulk) is "height" - a rifle's side-to-side width is
            // reliably its smallest dimension, so whichever axis is left over is never used directionally.
            int barrelAxis = LongestAxis(size);
            int heightAxis = -1;
            for (int a = 0; a < 3; a++)
            {
                if (a == barrelAxis) continue;
                if (heightAxis < 0 || size[a] > size[heightAxis]) heightAxis = a;
            }

            // Along each axis, the end whose cross-section (in the other two axes) is thinner is the "sharp"
            // end: the muzzle tip for the barrel axis (a bore is narrower than the stock/receiver/magazine
            // bulk behind it), and the magazine for the height axis (it's a thin strip hanging below the far
            // bulkier receiver/sight on top).
            int barrelSign = EndSign(points, barrelAxis, lo, hi);
            int magazineSign = EndSign(points, heightAxis, lo, hi);
            int upSign = -magazineSign;

            // Anchor carries the orientation/scale; go is nudged (while still unrotated) so the mesh's own
            // center sits on the anchor's origin along the barrel and left-right axes, and its magazine-side
            // edge sits on the anchor's origin along the height axis (so the hand grips near the gun's own
            // bottom), all expressed in the mesh's own raw axes rather than the rotated ones.
            var anchor = new GameObject(resourceName + "Anchor");
            anchor.transform.SetParent(parent, false);
            go.transform.SetParent(anchor.transform, false);
            Vector3 shift = Vector3.zero;
            for (int a = 0; a < 3; a++)
                shift[a] = a == heightAxis ? -(magazineSign > 0 ? hi[a] : lo[a]) : -(lo[a] + hi[a]) * 0.5f;
            go.transform.localPosition = shift;

            float rawLength = Mathf.Max(0.001f, size[barrelAxis]);
            float scale = targetLength / rawLength;
            Debug.Log("WM_DEBUG " + resourceName + " points=" + points.Count + " lo=" + lo + " hi=" + hi + " size=" + size
                + " barrelAxis=" + barrelAxis + " heightAxis=" + heightAxis + " barrelSign=" + barrelSign + " magazineSign=" + magazineSign
                + " rawLength=" + rawLength + " scale=" + scale);
            Vector3 forwardDir = AxisUnit(barrelAxis, barrelSign);
            Vector3 upDir = AxisUnit(heightAxis, upSign);
            // The rotation that carries the mesh's own measured forward/up axes onto this project's +Z
            // forward / +Y up convention is the inverse of the one LookRotation builds (which goes the
            // other way: standard forward/up -> the given axes).
            anchor.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forwardDir, upDir));
            anchor.transform.localScale = Vector3.one * scale;

            float tipHeight = size[heightAxis] * 0.5f * scale;
            tipLocal = new Vector3(0f, tipHeight, targetLength * 0.5f);
            return anchor;
        }

        static List<Vector3> CollectWorldVertices(GameObject go)
        {
            var points = new List<Vector3>();
            var filters = go.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var verts = mesh.vertices;
                var m = filters[i].transform.localToWorldMatrix;
                for (int v = 0; v < verts.Length; v++) points.Add(m.MultiplyPoint3x4(verts[v]));
            }
            var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < skins.Length; i++)
            {
                var mesh = skins[i].sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var verts = mesh.vertices;
                var m = skins[i].transform.localToWorldMatrix;
                for (int v = 0; v < verts.Length; v++) points.Add(m.MultiplyPoint3x4(verts[v]));
            }
            return points;
        }

        static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            return size.y >= size.z ? 1 : 2;
        }

        /// <summary>+1 if the thinner (smaller cross-section) end of this axis is at the max extreme, -1 if
        /// it's at the min extreme - the rest of TryBuild reads this as "which end is the sharp/thin
        /// feature" (muzzle tip for the barrel axis, magazine for the height axis).</summary>
        static int EndSign(List<Vector3> points, int axis, Vector3 lo, Vector3 hi)
        {
            int o1 = (axis + 1) % 3, o2 = (axis + 2) % 3;
            float span = Mathf.Max(0.0001f, hi[axis] - lo[axis]);
            const float sliceFrac = 0.12f;
            float footprintMin = SliceFootprint(points, axis, lo[axis], lo[axis] + span * sliceFrac, o1, o2);
            float footprintMax = SliceFootprint(points, axis, hi[axis] - span * sliceFrac, hi[axis], o1, o2);
            return footprintMax < footprintMin ? 1 : -1;
        }

        /// <summary>Cross-sectional area (in the o1/o2 axes) of whatever vertices fall within [sliceMin,
        /// sliceMax] along axis - a thin end (muzzle, magazine) has a small footprint; the bulkier opposite
        /// end (stock/receiver, scope/rail) has a large one.</summary>
        static float SliceFootprint(List<Vector3> points, int axis, float sliceMin, float sliceMax, int o1, int o2)
        {
            float o1Min = float.MaxValue, o1Max = float.MinValue, o2Min = float.MaxValue, o2Max = float.MinValue;
            bool any = false;
            for (int i = 0; i < points.Count; i++)
            {
                float v = points[i][axis];
                if (v < sliceMin || v > sliceMax) continue;
                any = true;
                float a1 = points[i][o1], a2 = points[i][o2];
                if (a1 < o1Min) o1Min = a1;
                if (a1 > o1Max) o1Max = a1;
                if (a2 < o2Min) o2Min = a2;
                if (a2 > o2Max) o2Max = a2;
            }
            return any ? (o1Max - o1Min) * (o2Max - o2Min) : float.MaxValue;
        }

        static Vector3 AxisUnit(int axis, int sign)
        {
            var v = Vector3.zero;
            v[axis] = sign;
            return v;
        }

        static Material MakeMaterial(string resourceName)
        {
            if (standardShader == null) standardShader = Shader.Find("Standard");
            if (standardShader == null)
            {
                if (!shaderMissingLogged) { shaderMissingLogged = true; Debug.LogError("[Outpost Zero] Standard shader missing; weapon models will use the Prims fallback material."); }
                return Prims.Mat(Color.white, true);
            }
            var mat = new Material(standardShader);
            var tex = Resources.Load<Texture2D>(ResourceRoot + resourceName);
            if (tex != null) mat.mainTexture = tex;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            return mat;
        }
    }
}
