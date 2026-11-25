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

            // squadCapMult stays fixed at 4x (level 1's base squadCap=12, so cap=48) for the rest of this
            // run - big enough to hold even the largest squad-size shot below - so only startingSquadSize
            // changes from here on.
            Tuning.Current.squadCapMult = 4f;

            // Close-up burst: a small squad, zoomed camera, four frames ~0.15s apart while running/shooting
            // so QA can check the rifle pose (HeroAnimator.LockAimPose) stays pointed forward throughout the
            // run cycle instead of just at whichever single instant an earlier one-shot screenshot landed on.
            Tuning.Current.startingSquadSize = 4;
            GameManager.I.BuildRun();
            GameManager.I.StartRun();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(1f);
            var camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Vector3 savedOffset = default; float savedLookAhead = 0f;
            if (camFollow != null)
            {
                savedOffset = camFollow.offset; savedLookAhead = camFollow.lookAhead;
                camFollow.offset = new Vector3(0f, 4.2f, -5.4f);
                camFollow.lookAhead = 1.4f;
            }
            yield return new WaitForSecondsRealtime(0.4f); // let the camera's own smoothing settle into the closer framing
            for (int b = 0; b < 4; b++)
            {
                if (b > 0) yield return new WaitForSecondsRealtime(0.15f);
                yield return Capture(outDir, "08-burst-" + b);
            }
            if (camFollow != null) { camFollow.offset = savedOffset; camFollow.lookAhead = savedLookAhead; }

            // Restart at a spread of squad sizes (well above the default level-1 squadCap at the big end)
            // so QA can compare Formation's dynamic spacing across the whole range in one pass: 3 and 10
            // should read loose/airy, 20 and 40 progressively tighter but still organic, never a grid.
            int[] squadShotSizes = { 3, 10, 20, 40 };
            string[] squadShotNames = { "09-squad-03", "10-squad-10", "11-squad-20", "12-squad-40" };
            for (int i = 0; i < squadShotSizes.Length; i++)
            {
                Tuning.Current.startingSquadSize = squadShotSizes[i];
                GameManager.I.BuildRun();
                GameManager.I.StartRun();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForSecondsRealtime(2f);
                yield return Capture(outDir, squadShotNames[i]);
            }

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
            // ImageConversion module isn't in the project, so write a binary PPM; run_qa.sh converts it to PNG.
            var px = tex.GetPixels32();
            var header = System.Text.Encoding.ASCII.GetBytes("P6\n" + w + " " + h + "\n255\n");
            var bytes = new byte[header.Length + w * h * 3];
            System.Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            int k = header.Length;
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++) { var c = px[y * w + x]; bytes[k++] = c.r; bytes[k++] = c.g; bytes[k++] = c.b; }
            var path = Path.Combine(dir, name + ".ppm");
            File.WriteAllBytes(path, bytes);
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
