using System.Collections.Generic;
using UnityEngine;

namespace OutpostZero
{
    /// <summary>The mount points every normalized weapon FBX carries as named child empties: Grip_R is the
    /// support point at the asset's own origin, Grip_L is the support-hand point, Muzzle is the barrel tip.
    /// Any transform here can be a synthesized fallback (see WeaponDefinition) if its empty is missing.</summary>
    public struct WeaponMountPoints
    {
        public Transform GripR;
        public Transform GripL;
        public Transform Muzzle;
    }

    /// <summary>Loads the normalized Meshy weapon FBX models from Resources, wiring a runtime material
    /// (Standard shader, never the FBX-imported one) so the look does not depend on import settings. Callers
    /// fall back to the code-built primitive guns when a model is missing, so a partial Resources folder
    /// never breaks a weapon.</summary>
    public static class WeaponModels
    {
        const string ResourceRoot = "Models/Weapons/";
        static Shader standardShader;
        static bool shaderMissingLogged;
        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        /// <summary>Instantiates def's model under parent with no extra rotation or recentring - the asset
        /// is already built in the +Z-muzzle/+Y-up/origin-at-grip frame, so none is needed. Scale is uniform,
        /// targetLength / def.lengthMeters, since the asset's own length along +Z is already known rather
        /// than measured from mesh bounds. Returns null (and destroys anything it created) if the model or
        /// its renderers are missing.</summary>
        public static GameObject TryBuild(WeaponDefinition def, Transform parent, float targetLength, out WeaponMountPoints mounts)
        {
            mounts = default;
            var prefab = Resources.Load<GameObject>(ResourceRoot + def.resourceName);
            if (prefab == null)
            {
                Debug.LogWarning("[Outpost Zero] Weapon model resource missing: " + ResourceRoot + def.resourceName + "; falling back to the primitive gun.");
                return null;
            }

            var go = Object.Instantiate(prefab);
            go.name = def.resourceName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (targetLength / Mathf.Max(0.001f, def.lengthMeters));

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Outpost Zero] Weapon model " + def.resourceName + " has no renderers; falling back to the primitive gun.");
                Object.Destroy(go);
                return null;
            }

            var mat = MaterialFor(def.resourceName);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = mat;

            mounts.GripR = RookAvatar.FindDeep(go.transform, "Grip_R") ?? go.transform;
            mounts.GripL = RookAvatar.FindDeep(go.transform, "Grip_L") ?? FallbackPoint(go.transform, "Grip_L", def.fallbackGripL);
            mounts.Muzzle = RookAvatar.FindDeep(go.transform, "Muzzle") ?? FallbackPoint(go.transform, "Muzzle", def.fallbackMuzzle);
            return go;
        }

        static Transform FallbackPoint(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name + "Fallback");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        /// <summary>One Standard material per weapon resource, reused across every instance (hero, soldiers,
        /// preview stage) instead of allocating a new Material each time this model is built.</summary>
        static Material MaterialFor(string resourceName)
        {
            if (materialCache.TryGetValue(resourceName, out var cached) && cached != null) return cached;

            Material mat;
            if (standardShader == null) standardShader = Shader.Find("Standard");
            if (standardShader == null)
            {
                if (!shaderMissingLogged) { shaderMissingLogged = true; Debug.LogError("[Outpost Zero] Standard shader missing; weapon models will use the Prims fallback material."); }
                mat = Prims.Mat(Color.white, true);
            }
            else
            {
                mat = new Material(standardShader);
                var tex = Resources.Load<Texture2D>(ResourceRoot + resourceName);
                if (tex != null) mat.mainTexture = tex;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            }
            materialCache[resourceName] = mat;
            return mat;
        }
    }
}
