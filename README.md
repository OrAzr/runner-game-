# Outpost Zero

A Last War-style squad runner. Steer Rook's squad up the road, shoot the numbered gates until they turn blue, grab weapon crates, and take down the boss.

**Play the browser prototype:** [https://orazr.github.io/runner-game-/](https://orazr.github.io/runner-game-/)

Drag left or right to steer. The shield button is Rook's Shield Wall. Tap **Play** to start (that also turns sound on). Add `?demo` for an auto-steer attract mode.

![Outpost Zero title screen](screenshots/01-title.png)

## Project status & workflow (Nov 28, 2026)

### Overview
- Outpost Zero: Squad Rush is a Unity 2022.3 squad runner in the style of Last War, for iOS and Android.
- Features: counter gates, a growing squad, zombies, a boss fight and an end screen.
- Heroes: Rook is the starting hero. Scout and Heavy are planned locked unlocks.
- Weapons: rifle, shotgun, flamethrower.
- Art pipeline: models from Meshy, cleaned up in Blender, animated with Mixamo.

### Current state
- main at ec8e88a = TestFlight build 23.
- Modeled: Rook, the three weapons and the squad soldiers.
- Soldiers look distinct, run as a fluid crowd, and hold their guns forward correctly.

### Open issue
- Rook's rifle is wrong: it sticks out sideways in gameplay and floats at head height on the hero select screen.
- Cause: the rifle is attached to the animated hand bone and rotated in code, so every fixed angle lands wrong on some frame.
- PR #22 was closed without merging and branch fix/rook-rifle-v2 is abandoned.
- Agreed next step (not started): bake the rifle into Rook's model in Blender (two-handed hold, pointing forward, fixed to the chest bone), approve front and side renders, then swap the model into the game in one PR.

### To do
- Models for the zombies, the boss, Scout and Heavy.

### Workflow
- Claude Code writes all game code, in small single-change runs with no self-testing.
- QA runs locally, never on the iOS build. iOS builds are never run locally.
- Batch fixes into one PR per iOS build.
- Never commit Unity noise: Packages/manifest.json, ProjectSettings, packages-lock.json, *.meta.
- Screenshots are reviewed by eye on zoomed crops, not with automated metrics.

#### Local QA at phone sizes
- Unity 2022.3.62f1 is installed on the dev box at /home/box/Unity/2022.3.62f1 (headless Linux).
- Script: /workspace/qa/run_qa.sh <branch> [WxH ...]. It fetches and checks out the branch, overlays the QA helpers from /workspace/qa/helpers (LinuxQaBuild.cs, QaShotRunner.cs), builds a StandaloneLinux64 player (never iOS), then runs it under Xvfb with software GL (Mesa llvmpipe) once per screen size.
- Screen sizes: 1170x2532 (iPhone) and 1080x1920 (Android). Default is both.
- Capture: QaShotRunner.cs drives a short QA-only level inside the player (started with -qaShots -qaOut <dir>) and saves a screenshot at fixed moments. A DONE marker file is written when it finishes.
- Output folder pattern: /workspace/qa/<branch with / replaced by _>/<size>/ (for example /workspace/qa/fix_gun-mount/1170x2532/).
- Frame names: 01-select, 02-menu, 03/04/05-tuning-*, 06-early-road, 07-gate-pass, 08-zombie-combat, 09-boss-arrival, 10-boss-fight-*, 11-end-screen, 12-burst-*, 12b-rook-side-*, 12c-rook-behind-*, 13-squad-03, 14-squad-10, 15-squad-20, 16-squad-40.
- Example:
```bash
git fetch origin <branch>:refs/remotes/origin/<branch>
env -u UNITY_LICENSE /workspace/qa/run_qa.sh <branch> 1170x2532
```
- Typecheck (fast compile check without Unity):
```bash
cd unity/OutpostZero/Tools/typecheck && dotnet build
```
  The only expected error is the UnityEditor.iOS one in IosBuild.cs.

#### Shipping
- GitHub Actions workflow "iOS TestFlight" (.github/workflows/ios-testflight.yml) runs on every push to main that touches unity/** or the workflow file (also manual workflow_dispatch). It exports the Xcode project, signs it on macOS and uploads to TestFlight, about 30 minutes.
- Docs-only changes such as README.md do not trigger a build, because the workflow only watches those paths.

## Open the Unity project

The Unity draft lives in [`unity/OutpostZero/`](unity/OutpostZero/) (editor version **2022.3.50f1**; Unity 6 should also open it). It has not been opened in the editor yet. All art is placeholder geometry built from code, and the main scene is generated on first open.

1. Install [Unity Hub](https://unity.com/download) and **Unity 2022.3 LTS**, with **Android Build Support** and/or **iOS Build Support** if you want device builds.
2. Unity Hub → **Add** → **Add project from disk** → select the `unity/OutpostZero` folder. The first import builds `Library/` and takes a few minutes.
3. The main scene is built automatically the first time the project opens. To rebuild it: menu **Outpost Zero → Build Main Scene**. That writes `Assets/Scenes/Main.unity`, wires the audio clips, adds the scene to Build Settings, and sets portrait orientation.
4. Open `Assets/Scenes/Main.unity`, set the Game view to a portrait size (for example 1080×1920), and press **Play**.

Controls in the editor: drag left/right with the mouse, or use the arrow keys / A and D. Space is Shield Wall.

Input uses the legacy Input Manager. If Unity 6 has **Project Settings → Player → Active Input Handling** set to "Input System Package (New)", switch it to **Both**.

Android and iOS build steps are in [`unity/OutpostZero/README.md`](unity/OutpostZero/README.md).

## Repo layout

| Path | Contents |
| --- | --- |
| `web/` | Playable build deployed to GitHub Pages (Three.js bundle, audio embedded). |
| `web-src/` | Prototype source. No `node_modules` in git. |
| `audio/` | Original synthesized audio: `wav/`, `ogg/`, `mp3/`. |
| `audio-src/synth.py` | Sound generator. |
| `unity/OutpostZero/` | Unity project. |
| `screenshots/` | Prototype screenshots (1080×1920). |

Rebuild the web bundle from `web-src/`:

```bash
npx esbuild src/main.js --bundle --minify --format=iife --target=es2020 --outfile=../web/game.js
```

Regenerate the wav files with `python3 audio-src/synth.py audio/wav` (needs numpy and scipy).

## Credits

Weapon models (rifle, shotgun, flamethrower) generated with [Meshy AI](https://www.meshy.ai/).
