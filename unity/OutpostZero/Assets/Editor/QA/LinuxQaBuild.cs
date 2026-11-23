#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OutpostZero.EditorTools
{
    /// <summary>
    /// Batchmode entry that builds a StandaloneLinux64 player for local screenshot QA.
    /// Invoked as OutpostZero.EditorTools.LinuxQaBuild.Build. Never builds iOS.
    /// </summary>
    public static class LinuxQaBuild
    {
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh();
                OutpostSceneBuilder.BuildScene();
                ApplyLinuxPlayerSettings();
                IncludeRuntimeShaders();

                var location = Arg("-qaBuildPath");
                if (string.IsNullOrEmpty(location)) location = Arg("-customBuildPath");
                if (string.IsNullOrEmpty(location)) location = "build/Linux/OutpostZero.x86_64";
                location = Path.GetFullPath(location);
                var dir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                {
                    var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
                    if (!switched)
                        throw new Exception("Could not switch the active build target to StandaloneLinux64. The editor needs Linux Build Support.");
                }

                var backend = ChooseBackend();
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, backend);
                Debug.Log("[QA] StandaloneLinux64 scripting backend: " + backend);

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0)
                    throw new Exception("No enabled scenes in Build Settings after OutpostSceneBuilder.BuildScene.");
                Debug.Log("[QA] Linux scenes: " + string.Join(", ", scenes));
                Debug.Log("[QA] Player output: " + location);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = BuildTarget.StandaloneLinux64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("Linux player build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");

                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static ScriptingImplementation ChooseBackend()
        {
            var forced = Arg("-qaBackend");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.Equals("il2cpp", StringComparison.OrdinalIgnoreCase))
                    return ScriptingImplementation.IL2CPP;
                if (forced.Equals("mono", StringComparison.OrdinalIgnoreCase))
                    return ScriptingImplementation.Mono2x;
            }

            var variations = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "LinuxStandaloneSupport", "Variations");
            if (Directory.Exists(variations))
            {
                var names = Directory.GetDirectories(variations);
                bool hasMono = names.Any(n => Path.GetFileName(n).IndexOf("mono", StringComparison.OrdinalIgnoreCase) >= 0);
                bool hasIl2cpp = names.Any(n => Path.GetFileName(n).IndexOf("il2cpp", StringComparison.OrdinalIgnoreCase) >= 0);
                if (hasMono) return ScriptingImplementation.Mono2x;
                if (hasIl2cpp) return ScriptingImplementation.IL2CPP;
            }
            return ScriptingImplementation.Mono2x;
        }

        static void ApplyLinuxPlayerSettings()
        {
            PlayerSettings.companyName = "OutpostZero";
            // Keep the shipping product name. The binary file name comes from -qaBuildPath.
            PlayerSettings.bundleVersion = IosBuild.MarketingVersion;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, IosBuild.BundleId);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Low);
        }

        // Same always-included shader list as IosBuild, copied so this file can be overlaid
        // onto any branch without editing IosBuild.cs.
        static readonly string[] RuntimeShaders =
        {
            "Sprites/Default",
            "Legacy Shaders/Diffuse",
            "Legacy Shaders/Particles/Additive",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Additive",
            "Mobile/Particles/Additive",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit"
        };

        static void IncludeRuntimeShaders()
        {
            var graphics = LoadGraphicsSettings();
            if (graphics == null)
            {
                Debug.LogError("[QA] GraphicsSettings is unavailable. Runtime shaders may be stripped.");
                return;
            }
            var so = new SerializedObject(graphics);
            var included = so.FindProperty("m_AlwaysIncludedShaders");
            if (included == null || !included.isArray)
            {
                Debug.LogError("[QA] GraphicsSettings has no m_AlwaysIncludedShaders array.");
                return;
            }
            for (int n = 0; n < RuntimeShaders.Length; n++)
            {
                if (RuntimeShaders[n].StartsWith("Hidden/")) continue;
                var shader = Shader.Find(RuntimeShaders[n]);
                if (shader != null && shader.name.StartsWith("Hidden/")) continue;
                if (shader == null)
                {
                    Debug.LogWarning("[QA] Shader not found in the editor: " + RuntimeShaders[n]);
                    continue;
                }
                bool already = false;
                for (int i = 0; i < included.arraySize; i++)
                {
                    var slot = included.GetArrayElementAtIndex(i);
                    if (slot == null || slot.objectReferenceValue == null) continue;
                    if (slot.objectReferenceValue == shader) { already = true; break; }
                }
                if (already) continue;
                included.arraySize++;
                included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
                Debug.Log("[QA] Always included shader: " + RuntimeShaders[n]);
            }
            so.ApplyModifiedProperties();
        }

        static UnityEngine.Object LoadGraphicsSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] != null && assets[i].GetType().Name == "GraphicsSettings") return assets[i];
                }
            }
            var unsupported = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Unsupported");
            var getter = unsupported != null
                ? unsupported.GetMethod("GetSerializedAssetInterfaceSingleton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                : null;
            if (getter == null) return null;
            return getter.Invoke(null, new object[] { "GraphicsSettings" }) as UnityEngine.Object;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
