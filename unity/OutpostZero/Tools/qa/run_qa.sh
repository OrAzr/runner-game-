#!/usr/bin/env bash
# Build a StandaloneLinux64 player for a repo branch and capture phone-sized
# screenshots. Never builds iOS.
#
# Usage:
#   /workspace/qa/run_qa.sh <branch> [WxH ...]
#   /workspace/qa/run_qa.sh fix/tuning-values
#   /workspace/qa/run_qa.sh fix/tuning-values 1170x2532 1080x1920
set -euo pipefail

UNITY_EDITOR="${UNITY_EDITOR:-/home/box/Unity/2022.3.62f1/Editor/Unity}"
REPO="${REPO:-/workspace/rg-main}"
PROJECT="${PROJECT:-${REPO}/unity/OutpostZero}"
HELPERS="${HELPERS:-/workspace/qa/helpers}"
QA_ROOT="${QA_ROOT:-/workspace/qa}"
PLAYER="${PLAYER_PATH:-${QA_ROOT}/build/OutpostZero.x86_64}"

if [ "${1:-}" = "" ] || [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
  sed -n '2,12p' "$0"
  exit 1
fi

BRANCH="$1"
shift || true
if [ "$#" -eq 0 ]; then
  SIZES=(1170x2532 1080x1920)
else
  SIZES=("$@")
fi

SAFE_BRANCH=$(printf '%s' "$BRANCH" | tr '/ ' '__')
OUT="${QA_ROOT}/${SAFE_BRANCH}"
mkdir -p "$OUT" "${QA_ROOT}/licenses" "${QA_ROOT}/build" "$HOME/.local/share/unity3d/Unity"

die() { echo "error: $*" >&2; exit 1; }

[ -x "$UNITY_EDITOR" ] || die "Unity editor not found at $UNITY_EDITOR. Run /workspace/qa/install_editor.sh first."
[ -d "$PROJECT" ] || die "Unity project not found at $PROJECT"
[ -d "$HELPERS/Assets" ] || die "QA helpers not found at $HELPERS"

# ---------- license ----------
# Never print license, serial, email, or password values.
LICENSE_DEST="${HOME}/.local/share/unity3d/Unity/Unity_lic.ulf"
ensure_license() {
  if [ -n "${UNITY_LICENSE-}" ]; then
    mkdir -p "$(dirname "$LICENSE_DEST")"
    printf '%s' "$UNITY_LICENSE" > "$LICENSE_DEST"
    chmod 600 "$LICENSE_DEST"
    echo "license: wrote UNITY_LICENSE env to the standard Unity license path"
  elif [ -n "${UNITY_LICENSE_FILE-}" ] && [ -f "$UNITY_LICENSE_FILE" ]; then
    mkdir -p "$(dirname "$LICENSE_DEST")"
    cp "$UNITY_LICENSE_FILE" "$LICENSE_DEST"
    chmod 600 "$LICENSE_DEST"
    echo "license: copied UNITY_LICENSE_FILE into the standard Unity license path"
  elif [ -f /home/box/.unity/Unity_lic.ulf ]; then
    mkdir -p "$(dirname "$LICENSE_DEST")"
    cp /home/box/.unity/Unity_lic.ulf "$LICENSE_DEST"
    chmod 600 "$LICENSE_DEST"
    echo "license: copied /home/box/.unity/Unity_lic.ulf into the standard Unity license path"
  fi

  if [ -n "${UNITY_SERIAL-}" ] && [ -n "${UNITY_EMAIL-}" ] && [ -n "${UNITY_PASSWORD-}" ]; then
    echo "license: activating with UNITY_SERIAL (log: ${QA_ROOT}/licenses/serial.log)"
    xvfb-run -a -s "-screen 0 1024x768x24" \
      "$UNITY_EDITOR" -batchmode -nographics -quit \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -serial "$UNITY_SERIAL" \
      -logfile "${QA_ROOT}/licenses/serial.log" || true
    return 0
  fi

  if [ -f "$LICENSE_DEST" ]; then
    echo "license: activating .ulf via -manualLicenseFile (log: ${QA_ROOT}/licenses/manual.log)"
    xvfb-run -a -s "-screen 0 1024x768x24" \
      "$UNITY_EDITOR" -batchmode -nographics -quit \
      -manualLicenseFile "$LICENSE_DEST" \
      -logfile "${QA_ROOT}/licenses/manual.log" || true
    return 0
  fi

  echo "license: none found. Generating a manual activation file."
  local alf_dir
  alf_dir=$(mktemp -d /tmp/unity-alf.XXXXXX)
  ( cd "$alf_dir" && xvfb-run -a -s "-screen 0 1024x768x24" \
      "$UNITY_EDITOR" -batchmode -nographics -quit \
      -createManualActivationFile \
      -logfile "${QA_ROOT}/licenses/alf.log" ) || true
  local alf
  alf=$(find "$alf_dir" -name '*.alf' | head -1)
  if [ -n "$alf" ]; then
    cp "$alf" "${QA_ROOT}/licenses/Unity_v2022.3.62f1.alf"
    echo "license: wrote ${QA_ROOT}/licenses/Unity_v2022.3.62f1.alf"
  else
    echo "license: could not generate a .alf; see ${QA_ROOT}/licenses/alf.log" >&2
  fi
  cat >&2 <<'HINT'

Unity is installed but not licensed, so the player cannot be built yet.

Do one of the following. Do not paste the license or a password into chat.

  1. Preferred: copy the same Personal Unity_lic.ulf already stored as the
     GitHub secret UNITY_LICENSE onto this machine at:

         /home/box/.unity/Unity_lic.ulf

     chmod 600 that file, then re-run this script.

  2. Or, with a computer browser (the box desktop works): open
     https://license.unity3d.com/manual , sign in with the Unity ID that owns
     the license, upload /workspace/qa/licenses/Unity_v2022.3.62f1.alf, then
     put the downloaded .ulf at /home/box/.unity/Unity_lic.ulf and re-run.

  3. Or, for a Pro/Plus serial, export UNITY_EMAIL, UNITY_PASSWORD, and
     UNITY_SERIAL in the environment and re-run.

Do not stay signed into Unity Hub on this machine. A held Personal seat can
break the TestFlight CI job.

HINT
  exit 2
}

ensure_license

# ---------- repo ----------
echo "repo: checking out $BRANCH in $REPO"
git -C "$REPO" fetch origin --prune
if git -C "$REPO" show-ref --verify --quiet "refs/heads/${BRANCH}"; then
  git -C "$REPO" checkout "$BRANCH"
  git -C "$REPO" pull --ff-only origin "$BRANCH" 2>/dev/null || true
elif git -C "$REPO" show-ref --verify --quiet "refs/remotes/origin/${BRANCH}"; then
  git -C "$REPO" checkout -B "$BRANCH" "origin/${BRANCH}"
else
  die "branch not found: $BRANCH"
fi

# Overlay QA helpers so the branch does not have to already contain them.
mkdir -p "${PROJECT}/Assets/Editor/QA" "${PROJECT}/Assets/Scripts/QA" "${PROJECT}/Tools/qa"
cp -a "${HELPERS}/Assets/Editor/QA/LinuxQaBuild.cs" "${PROJECT}/Assets/Editor/QA/"
cp -a "${HELPERS}/Assets/Scripts/QA/QaShotRunner.cs" "${PROJECT}/Assets/Scripts/QA/"
if [ -f "${HELPERS}/Tools/qa/README.md" ]; then
  cp -a "${HELPERS}/Tools/qa/README.md" "${PROJECT}/Tools/qa/"
fi
echo "repo: overlaid QA helpers onto $BRANCH"

# ---------- build ----------
BUILD_LOG="${OUT}/editor-build.log"
echo "build: StandaloneLinux64 -> $PLAYER"
echo "build: log $BUILD_LOG"
START=$(date +%s)
set +e
xvfb-run -a -s "-screen 0 1080x1920x24" \
  "$UNITY_EDITOR" -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -executeMethod OutpostZero.EditorTools.LinuxQaBuild.Build \
  -qaBuildPath "$PLAYER" \
  -logFile "$BUILD_LOG"
BUILD_RC=$?
set -e
BUILD_SEC=$(( $(date +%s) - START ))
echo "build: exit $BUILD_RC in ${BUILD_SEC}s"

if [ "$BUILD_RC" -ne 0 ] || [ ! -x "$PLAYER" ]; then
  echo "build failed. Last 80 lines of $BUILD_LOG:" >&2
  tail -n 80 "$BUILD_LOG" >&2 || true
  if grep -qiE "license|serial|activation|ULF" "$BUILD_LOG" 2>/dev/null; then
    echo "The log mentions licensing. See the license instructions above." >&2
  fi
  exit 1
fi

# ---------- shots ----------
run_size() {
  local size="$1"
  local w="${size%x*}"
  local h="${size#*x}"
  local dest="${OUT}/${size}"
  rm -rf "$dest"
  mkdir -p "$dest"
  echo "shots: ${size} -> $dest"
  local start
  start=$(date +%s)
  # Software GL: this box has no discrete GPU. Mesa llvmpipe via Xvfb.
  set +e
  timeout 180 env LIBGL_ALWAYS_SOFTWARE=1 \
    xvfb-run -a -s "-screen 0 ${w}x${h}x24" \
    "$PLAYER" \
      -screen-width "$w" -screen-height "$h" -screen-fullscreen 0 \
      -popupwindow \
      -qaShots -qaOut "$dest" \
      -logfile "${dest}/player.log"
  local rc=$?
  set -e
  local sec=$(( $(date +%s) - start ))
  echo "shots: ${size} exit $rc in ${sec}s"
  if [ ! -f "${dest}/DONE" ]; then
    echo "shots: ${size} did not write a DONE marker; see ${dest}/player.log and ${dest}/FAILED" >&2
    return 1
  fi
  ls -l "$dest"/*.png 2>/dev/null || true
  return 0
}

SHOT_FAIL=0
for size in "${SIZES[@]}"; do
  run_size "$size" || SHOT_FAIL=1
done

END_SEC=$(( $(date +%s) - START ))
echo "done: outputs in $OUT (total ${END_SEC}s, build ${BUILD_SEC}s)"
ls -R "$OUT" || true
exit $SHOT_FAIL
