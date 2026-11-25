# Local Linux screenshot QA

Run the Outpost Zero Unity player on this Linux box and capture phone-sized
screenshots before a PR is shipped. This does not build iOS.

## One-time setup

1. Install the editor (idempotent):

   ```bash
   /workspace/qa/install_editor.sh
   ```

   Editor path: `/home/box/Unity/2022.3.62f1/Editor/Unity`

2. Activate a Unity license. The editor will not build without one.

   Prefer the same Personal `Unity_lic.ulf` already stored as the GitHub
   Actions `UNITY_LICENSE` secret. **Do not paste it into chat.**

   Put the file at exactly this path, mode `600`:

   ```
   /home/box/.unity/Unity_lic.ulf
   ```

   Or export `UNITY_LICENSE` (file contents) or `UNITY_LICENSE_FILE` (path)
   in the shell that runs `run_qa.sh`.

   If no license file is present, `run_qa.sh` writes a manual activation
   file to `/workspace/qa/licenses/Unity_v2022.3.62f1.alf`. Upload that at
   https://license.unity3d.com/manual (Unity ID sign-in), download the
   resulting `.ulf`, and place it at `/home/box/.unity/Unity_lic.ulf`.

   A Pro/Plus serial also works: set `UNITY_EMAIL`, `UNITY_PASSWORD`, and
   `UNITY_SERIAL` in the environment. Do not stay signed into Unity Hub on
   this machine; a held Personal seat can break the TestFlight CI job.

## Usage

```bash
/workspace/qa/run_qa.sh <branch>
/workspace/qa/run_qa.sh fix/tuning-values
/workspace/qa/run_qa.sh fix/tuning-values 1170x2532 1080x1920
```

What it does:

1. Checks out `<branch>` in `/workspace/rg-main`.
2. Overlays the QA helper scripts (so the branch does not need them committed).
3. Builds a StandaloneLinux64 player (not iOS).
4. Runs it under Xvfb at each size (default `1170x2532` and `1080x1920`).
5. Auto-walks: character select, main menu, tuning top / mid / bottom,
   gameplay at ~2s and ~5s.

Screenshots land in `/workspace/qa/<branch>/<size>/`.

A repo copy of the helpers lives on `chore/qa-shots`. Fold that branch into
the next batched PR; do not ship a dedicated QA-only PR.
