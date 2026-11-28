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
        /// back as the muzzle-end position in parent's local space, for placing extras like a nozzle glow.
        /// Returns null (and destroys anything it created) if the model or its renderers are missing.</summary>
        public static GameObject TryBuild(string resourceName, Transform parent, float targetLength, out Vector3 tipLocal, bool heroKit = false)
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

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var mat = MakeMaterial(resourceName);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = mat;

            // Anchor carries the orientation/scale; go is nudged (while still unrotated) so the mesh's own
            // center/base sit on the anchor's origin, so that offset is always expressed in the mesh's own
            // axes rather than the rotated ones.
            var anchor = new GameObject(resourceName + "Anchor");
            anchor.transform.SetParent(parent, false);
            go.transform.SetParent(anchor.transform, false);
            go.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            // The long side runs along local X, used below to scale the mesh to targetLength.
            float rawLength = Mathf.Max(0.001f, bounds.size.x);
            float scale = targetLength / rawLength;
            // Found empirically, not derived from the FBX's documented axes: with no rotation at all the
            // imported mesh's muzzle hangs roughly straight down and off to one side rather than along any
            // single clean axis, so this isn't a simple one-axis flip. Verified by comparing the held rifle
            // against the (always-world-forward) bullet tracers from three angles in QA screenshots -
            // profile, directly from behind, and the character-select turntable - rather than by reasoning
            // about the import convention. +90 about X (applied first, in the mesh's own unrotated frame)
            // swings the muzzle from hanging down to forward and level; -90 about Y (applied after, in the
            // hand's frame) then turns that barrel in from off the character's side to match the character's
            // own forward, magazine down and scope up at every yaw. This is the correct mount for the
            // soldiers in every tested view - leave it alone for them.
            var mount = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            // Rook's hero-kit rifle reads backward (muzzle behind him, stock ahead) under that same mount -
            // same resource, same socket convention, but his rig still ends up 180 degrees off from the
            // soldiers' once everything else lines up, so the hero kit alone gets an extra yaw to turn the
            // barrel back around to his facing side. A pure yaw only swaps forward/backward: it leaves the
            // already-correct level pitch and magazine-down roll untouched.
            if (heroKit) mount = Quaternion.Euler(0f, 180f, 0f) * mount;
            anchor.transform.localRotation = mount;
            anchor.transform.localScale = Vector3.one * scale;

            float tipHeight = bounds.size.y * 0.5f * scale;
            tipLocal = new Vector3(0f, tipHeight, targetLength * 0.5f);
            return anchor;
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
