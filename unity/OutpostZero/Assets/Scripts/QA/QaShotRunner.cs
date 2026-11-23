using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace OutpostZero
{
    /// <summary>
    /// Offline screenshot walker. Completely idle unless the player was launched with -qaShots,
    /// so it is safe to leave in iOS builds: that flag is never passed there.
    /// </summary>
    public class QaShotRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!HasFlag("-qaShots")) return;
            var go = new GameObject("QaShotRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<QaShotRunner>();
        }

        IEnumerator Start()
        {
            var outDir = Arg("-qaOut") ?? Path.Combine(Application.persistentDataPath, "qa");
            Directory.CreateDirectory(outDir);
            AudioListener.volume = 0f;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            var deadline = Time.unscaledTime + 20f;
            while (GameUI.I == null && Time.unscaledTime < deadline)
                yield return null;
            if (GameUI.I == null)
            {
                Fail(outDir, "GameUI never appeared");
                yield break;
            }

            // Character select is the default landing screen.
            yield return WaitFrames(8);
            yield return Capture(outDir, "01-select");

            GameUI.I.ShowMenu();
            Canvas.ForceUpdateCanvases();
            yield return WaitFrames(4);
            yield return Capture(outDir, "02-menu");

            GameUI.I.ShowTuning();
            Canvas.ForceUpdateCanvases();
            yield return WaitFrames(6);
            yield return Capture(outDir, "03-tuning-top");

            var scroll = FindTuningScroll();
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 0.5f;
                Canvas.ForceUpdateCanvases();
                yield return WaitFrames(4);
                yield return Capture(outDir, "04-tuning-mid");

                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                yield return WaitFrames(4);
                yield return Capture(outDir, "05-tuning-bottom");
            }
            else
            {
                File.WriteAllText(Path.Combine(outDir, "WARN-no-scroll.txt"),
                    "Tuning ScrollRect was not found; mid/bottom shots skipped.");
            }

            if (GameManager.I == null)
            {
                Fail(outDir, "GameManager missing; cannot start a run");
                yield break;
            }

            GameManager.I.StartRun();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Capture(outDir, "06-gameplay-2s");
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture(outDir, "07-gameplay-5s");

            File.WriteAllText(Path.Combine(outDir, "DONE"),
                DateTime.UtcNow.ToString("o") + "\n" + Screen.width + "x" + Screen.height + "\n");
            Debug.Log("[QA] shots written to " + outDir);
            Application.Quit();
        }

        static ScrollRect FindTuningScroll()
        {
            var all = FindObjectsOfType<ScrollRect>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                if (s.gameObject.name == "Viewport") return s;
                var t = s.transform;
                while (t != null)
                {
                    if (t.name == "Tuning") return s;
                    t = t.parent;
                }
            }
            return all.Length > 0 ? all[0] : null;
        }

        static IEnumerator Capture(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0)
            {
                File.WriteAllText(Path.Combine(dir, name + ".error.txt"), "Screen size was " + w + "x" + h);
                yield break;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[QA] wrote " + path + " (" + w + "x" + h + ")");
        }

        static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void Fail(string dir, string message)
        {
            Debug.LogError("[QA] " + message);
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "FAILED"), message + "\n");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Application.Quit();
        }

        static bool HasFlag(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == flag) return true;
            return false;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
