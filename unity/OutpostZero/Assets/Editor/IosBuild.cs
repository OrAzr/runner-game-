#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
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

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("iOS player build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
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
