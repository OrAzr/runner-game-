#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS;
using UnityEngine;

namespace OutpostZero.EditorTools
{
    /// <summary>GameCI entry point. Builds the main scene, then exports an Xcode project.
    /// Invoked as OutpostZero.EditorTools.IosBuild.Build.</summary>
    public static class IosBuild
    {
        public const string BundleId = "com.outpostzero.game";
        public const string MarketingVersion = "0.2.0";

        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh();
                OutpostSceneBuilder.BuildScene();
                ApplyIosPlayerSettings();
                ApplyAppIcon();

                // GameCI passes the output location as -customBuildPath (buildsPath/target/buildName).
                var location = Arg("-customBuildPath");
                if (string.IsNullOrEmpty(location)) location = Environment.GetEnvironmentVariable("BUILD_PATH");
                if (string.IsNullOrEmpty(location)) location = "build/iOS";
                location = Path.GetFullPath(location);
                if (Directory.Exists(location)) Directory.Delete(location, true);
                Directory.CreateDirectory(location);

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                {
                    var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                    if (!switched) throw new Exception("Could not switch the active build target to iOS. The editor image needs the iOS build module.");
                }

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0) throw new Exception("No enabled scenes in Build Settings after OutpostSceneBuilder.BuildScene.");
                Debug.Log("[Outpost Zero] iOS scenes: " + string.Join(", ", scenes));
                Debug.Log("[Outpost Zero] Xcode export: " + location);

                var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("iOS player build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
                InstallExportedAppIcons(location);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void ApplyIosPlayerSettings()
        {
            PlayerSettings.companyName = "OutpostZero";
            PlayerSettings.productName = "Outpost Zero";
            PlayerSettings.bundleVersion = MarketingVersion;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            var buildNumber = Arg("-buildNumber");
            if (string.IsNullOrEmpty(buildNumber)) buildNumber = "1";
            PlayerSettings.iOS.buildNumber = buildNumber;
            var team = Arg("-appleTeamId");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            Debug.Log("[Outpost Zero] iOS " + BundleId + " version " + MarketingVersion + " (" + buildNumber + "), portrait, IL2CPP ARM64");
        }

        const string IconAssetPath = "Assets/AppIcon/AppIcon-1024.png";

        static void ApplyAppIcon()
        {
            var importer = AssetImporter.GetAtPath(IconAssetPath) as TextureImporter;
            if (importer == null) throw new Exception("App icon missing at " + IconAssetPath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (tex == null) throw new Exception("Could not load " + IconAssetPath);

            var defaultSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Unknown);
            if (defaultSizes != null && defaultSizes.Length > 0)
            {
                var defaults = new Texture2D[defaultSizes.Length];
                for (var i = 0; i < defaults.Length; i++) defaults[i] = tex;
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, defaults);
            }
            foreach (var kind in new[] { IconKind.Application, IconKind.Settings, IconKind.Notification, IconKind.Spotlight, IconKind.Store })
                SetLegacyIcons(BuildTargetGroup.iOS, tex, kind);

            foreach (var kind in new PlatformIconKind[]
            {
                iOSPlatformIconKind.Application,
                iOSPlatformIconKind.Spotlight,
                iOSPlatformIconKind.Settings,
                iOSPlatformIconKind.Notification,
                iOSPlatformIconKind.Marketing
            })
            {
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.iOS, kind);
                if (icons == null || icons.Length == 0) continue;
                foreach (var icon in icons) icon.SetTexture(tex);
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.iOS, kind, icons);
            }
            Debug.Log("[Outpost Zero] Assigned opaque 1024 app icon to iOS Player Settings, including the marketing icon");
        }

        static void SetLegacyIcons(BuildTargetGroup group, Texture2D tex, IconKind kind)
        {
            var sizes = PlayerSettings.GetIconSizesForTargetGroup(group, kind);
            if (sizes == null || sizes.Length == 0) return;
            var icons = new Texture2D[sizes.Length];
            for (var i = 0; i < icons.Length; i++) icons[i] = tex;
            PlayerSettings.SetIconsForTargetGroup(group, icons, kind);
        }

        // Unity 2022 leaves the Xcode "Any Appearance" 1024 well empty. App Store Connect rejects that (altool 91111).
        static void InstallExportedAppIcons(string xcodeRoot)
        {
            var src = Path.Combine(Application.dataPath, "AppIcon/ios");
            if (!Directory.Exists(src)) throw new Exception("iOS icon set missing at " + src);
            var catalogs = Directory.GetDirectories(xcodeRoot, "AppIcon.appiconset", SearchOption.AllDirectories);
            var catalog = catalogs.Length > 0
                ? catalogs[0]
                : Path.Combine(xcodeRoot, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset");
            Directory.CreateDirectory(catalog);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(catalog, Path.GetFileName(file)), true);
            Debug.Log("[Outpost Zero] Wrote iOS app icon catalog at " + catalog);
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
