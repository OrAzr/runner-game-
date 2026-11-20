#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OutpostZero.EditorTools
{
    /// <summary>One-click setup: Outpost Zero > Build Main Scene. Creates Assets/Scenes/Main.unity with camera, light,
    /// GameManager, AudioManager (clips auto-wired from Assets/Audio) and UI, adds it to Build Settings and applies
    /// mobile player settings (portrait, bundle id). Also runs automatically once when the project first opens.</summary>
    [InitializeOnLoad]
    public static class OutpostSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static OutpostSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !SessionState.GetBool("oz_built", false))
                { SessionState.SetBool("oz_built", true); BuildScene(); }
            };
        }

        [MenuItem("Outpost Zero/Build Main Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.87f, 0.91f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.29f, 0.29f, 0.24f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Sky; RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 110;

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.Sky; cam.fieldOfView = 52; cam.farClipPlane = 200;
            camGo.transform.position = new Vector3(0, 15, -15.5f); camGo.transform.LookAt(new Vector3(0, 0, 5));

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(50, 30, 0);

            new GameObject("GameManager", typeof(GameManager));
            new GameObject("UI", typeof(GameUI));

            var audioGo = new GameObject("AudioManager", typeof(AudioManager));
            var am = audioGo.GetComponent<AudioManager>();
            WireAudio(am);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ApplyPlayerSettings();
            Debug.Log("[Outpost Zero] Main scene built at " + ScenePath + ". Press Play.");
        }

        static void WireAudio(AudioManager am)
        {
            (Sfx id, string file)[] table = {
                (Sfx.Rifle, "sfx_rifle"), (Sfx.Shotgun, "sfx_shotgun"), (Sfx.FlameLoop, "sfx_flamethrower_loop"),
                (Sfx.GateGood, "sfx_gate_good"), (Sfx.GateBad, "sfx_gate_bad"), (Sfx.ZombieGroan, "sfx_zombie_groan"),
                (Sfx.ZombieHit, "sfx_zombie_hit"), (Sfx.SoldierLost, "sfx_soldier_lost"), (Sfx.CrateOpen, "sfx_crate_open"),
                (Sfx.BossRoar, "sfx_boss_roar"), (Sfx.Victory, "sting_victory"), (Sfx.Defeat, "sting_defeat"), (Sfx.UIClick, "sfx_ui_click"),
            };
            am.clips.Clear();
            foreach (var (id, file) in table)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{file}.wav");
                if (clip == null) Debug.LogWarning("[Outpost Zero] Missing audio: " + file);
                am.clips.Add(new AudioManager.Entry { id = id, clip = clip });
            }
            am.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music_battle_loop.wav");
            // Small SFX: decompress on load for zero latency; music streams.
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is AudioImporter imp)
                {
                    var s = imp.defaultSampleSettings;
                    bool isMusic = path.Contains("music");
                    s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = isMusic ? 0.6f : 0.8f;
                    imp.defaultSampleSettings = s; imp.SaveAndReimport();
                }
            }
            EditorUtility.SetDirty(am);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "OutpostZero";
            PlayerSettings.productName = "Outpost Zero";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.outpostzero.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.outpostzero.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        }
    }
}
#endif
