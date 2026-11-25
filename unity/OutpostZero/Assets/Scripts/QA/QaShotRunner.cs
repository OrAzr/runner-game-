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

        // Brief 14: the rifle is supposed to point down the road (world +Z) at all times, independent of
        // Rook's cosmetic body yaw/lean/flinch - this caught the ~20 deg visual yaw (brief 13) dragging the
        // whole aim-locked arm chain with it and sending the muzzle up over his shoulder. Checked every
        // frame of actual play (not just at screenshot instants) so a regression anywhere in the run - the
        // road, a gate, the boss fight - gets caught instead of just whatever instant a screenshot landed on.
        const float MinAimDot = 0.85f;

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.IsPlaying || gm.Squad == null) return;
            var squad = gm.Squad;
            Transform rookMuzzle = squad.Rook != null ? squad.Rook.Muzzle : null;
            Transform soldierMuzzle = null;
            for (int i = 1; i < squad.Soldiers.Count; i++)
            {
                if (squad.Soldiers[i] != null) { soldierMuzzle = squad.Soldiers[i].Muzzle; break; }
            }
            if (rookMuzzle == null && soldierMuzzle == null) return;
            float rookDot = rookMuzzle != null ? Vector3.Dot(rookMuzzle.forward, Vector3.forward) : 1f;
            float soldierDot = soldierMuzzle != null ? Vector3.Dot(soldierMuzzle.forward, Vector3.forward) : 1f;
            Debug.Log("QA_AIM rook=" + rookDot.ToString("F3") + " soldier=" + soldierDot.ToString("F3"));
            if (rookDot < MinAimDot || soldierDot < MinAimDot)
                Debug.LogError("QA_AIM_FAIL rook=" + rookDot.ToString("F3") + " soldier=" + soldierDot.ToString("F3"));
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

            // Full end-to-end pass: road, gates, zombies, boss fight, end screen, all in one unbroken run.
            // Levels.Build returns the short QA layout automatically while -qaShots is set (see
            // LevelConfig.cs), so this reaches the boss in ~25s instead of the shipped level's ~35s+.
            var gm = GameManager.I;
            // The default 1-soldier start (Tuning.Current.startingSquadSize) is too thin to reliably survive
            // the QA level's early zombie group once a zombie's aggroRange closes the gap faster than the
            // squad can out-walk it; rebuilding with a bigger starting squad here (rather than in
            // LevelConfig.QaLevel's startCount, which Tuning.Apply overwrites unconditionally right after
            // Levels.Build returns it) gives this E2E pass a reliable buffer without touching shipped tuning.
            Tuning.Current.startingSquadSize = 6;
            gm.BuildRun();
            gm.StartRun();
            Canvas.ForceUpdateCanvases();

            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(outDir, "06-early-road");

            float gateZ = gm.level.gates.Count > 0 ? gm.level.gates[0].distance : 20f;
            yield return WaitUntil(() => gm.State != GameState.Running || gm.Distance >= gateZ + 2f, 15f);
            yield return Capture(outDir, "07-gate-pass");

            float zombieZ = gm.level.zombies.Count > 1 ? gm.level.zombies[1].distance : gateZ;
            yield return WaitUntil(() => gm.State != GameState.Running || gm.Distance >= zombieZ + 1f, 15f);
            yield return Capture(outDir, "08-zombie-combat");

            yield return WaitUntil(() => gm.State == GameState.BossFight, 15f);
            yield return new WaitForSecondsRealtime(0.5f); // let the boss roar/entrance settle before capturing
            yield return Capture(outDir, "09-boss-arrival");

            // 3-4 frames ~0.3s apart during the fight itself, to check soldiers/Rook keep the aim-locked
            // rifle pose (and keep firing at the boss) instead of the idle/look-around clip this covers.
            for (int b = 0; b < 4; b++)
            {
                if (b > 0) yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(outDir, "10-boss-fight-" + b);
            }

            // Boss HP is tuned low for the QA level, but effective boss dps is well below a naive shooter
            // count * rate estimate (see LevelConfig.QaLevel) since only centrally-aligned shooters' bullets
            // actually connect with a single, centered target - give a generous margin.
            yield return WaitUntil(() => !gm.IsPlaying, 60f);
            yield return new WaitForSecondsRealtime(0.8f); // GameUI.ShowEnd reveals the panel after its own 0.7s delay
            yield return Capture(outDir, "11-end-screen");

            // squadCapMult stays fixed at 4x (level 1's base squadCap=12, so cap=48) for the rest of this
            // run - big enough to hold even the largest squad-size shot below - so only startingSquadSize
            // changes from here on.
            Tuning.Current.squadCapMult = 4f;

            // Close-up burst: a small squad, zoomed camera, four frames ~0.15s apart while running/shooting
            // so QA can check the rifle pose (HeroAnimator.LockAimPose) stays pointed forward throughout the
            // run cycle instead of just at whichever single instant an earlier one-shot screenshot landed on.
            Tuning.Current.startingSquadSize = 4;
            gm.BuildRun();
            gm.StartRun();
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
                yield return Capture(outDir, "12-burst-" + b);
            }

            // Brief 14: dedicated side and behind/above close-ups on Rook alone, so a crooked rifle (pointing
            // up/back over a shoulder instead of down the road) is obvious from an angle the normal chase cam
            // never shows. Runs through temporary cameras layered on top of the gameplay one (never replacing
            // it) so nothing here can leak into an actual played frame.
            yield return CaptureRookAngles(outDir);

            if (camFollow != null) { camFollow.offset = savedOffset; camFollow.lookAhead = savedLookAhead; }

            // Restart at a spread of squad sizes (well above the default level-1 squadCap at the big end)
            // so QA can compare Formation's dynamic spacing across the whole range in one pass: 3 and 10
            // should read loose/airy, 20 and 40 progressively tighter but still organic, never a grid.
            int[] squadShotSizes = { 3, 10, 20, 40 };
            string[] squadShotNames = { "13-squad-03", "14-squad-10", "15-squad-20", "16-squad-40" };
            for (int i = 0; i < squadShotSizes.Length; i++)
            {
                Tuning.Current.startingSquadSize = squadShotSizes[i];
                gm.BuildRun();
                gm.StartRun();
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

        /// <summary>Side and behind/above close-ups on Rook alone, through a temporary camera stacked above
        /// (never replacing) the gameplay one - depth puts it on top, matching clear/culling/FOV off the
        /// gameplay camera keeps the shot's look consistent with everything else. Destroyed when done, so
        /// gameplay rendering is back to just Camera.main for every shot after this.</summary>
        IEnumerator CaptureRookAngles(string dir)
        {
            var gm = GameManager.I;
            var mainCam = Camera.main;
            var rookBody = gm != null && gm.Squad != null && gm.Squad.Rook != null ? gm.Squad.Rook.Body : null;
            if (mainCam == null || rookBody == null) yield break;

            var qaCamGo = new GameObject("QaRookCam");
            var qaCam = qaCamGo.AddComponent<Camera>();
            qaCam.clearFlags = mainCam.clearFlags;
            qaCam.backgroundColor = mainCam.backgroundColor;
            qaCam.cullingMask = mainCam.cullingMask;
            qaCam.fieldOfView = mainCam.fieldOfView;
            qaCam.nearClipPlane = mainCam.nearClipPlane;
            qaCam.farClipPlane = mainCam.farClipPlane;
            qaCam.depth = mainCam.depth + 1f;

            // Side profile, level with his chest, square across the road: the clearest angle to tell
            // whether the rifle is pointing down the road or back across his own body.
            yield return CaptureFromRookAngle(dir, "12b-rook-side", qaCam, rookBody,
                new Vector3(2.6f, 1.6f, 0f), Vector3.up * 1.4f, 3, 0.25f);
            // Raised chase angle from behind/above: this is the exact framing the original bug report
            // ("sticks up/back over his right shoulder") was spotted from.
            yield return CaptureFromRookAngle(dir, "12c-rook-behind", qaCam, rookBody,
                new Vector3(0f, 2.6f, -3.2f), Vector3.up * 1.1f, 3, 0.25f);

            Destroy(qaCamGo);
        }

        static IEnumerator CaptureFromRookAngle(string dir, string prefix, Camera cam, Transform rook,
            Vector3 camOffsetFromRook, Vector3 lookOffsetFromRook, int frames, float gapSeconds)
        {
            for (int i = 0; i < frames; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(gapSeconds);
                Vector3 focus = rook.position + lookOffsetFromRook;
                cam.transform.position = rook.position + camOffsetFromRook;
                cam.transform.rotation = Quaternion.LookRotation((focus - cam.transform.position).normalized, Vector3.up);
                yield return Capture(dir, prefix + "-" + i);
            }
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

        /// <summary>Polls a condition every frame instead of guessing a fixed delay, so the capture tracks
        /// whatever the current level/run actually does (distance to a gate, state change, run ending)
        /// rather than a number tuned against one specific level layout. Gives up silently at the timeout -
        /// callers still capture whatever is on screen so a stall shows up as a wrong-looking screenshot
        /// rather than the whole QA pass hanging.</summary>
        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (!condition() && Time.unscaledTime < deadline) yield return null;
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
