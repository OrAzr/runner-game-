#!/usr/bin/env bash
# Install Unity 2022.3.62f1 Linux editor + Linux IL2CPP + iOS Build Support
# (iOS module is installed only so Assets/Editor/IosBuild.cs compiles; this
# script never runs an iOS build). Idempotent.
set -euo pipefail

HASH=4af31df58517
VER=2022.3.62f1
EDITOR_ROOT="${EDITOR_ROOT:-/home/box/Unity/${VER}}"
DL="${DOWNLOAD_DIR:-/home/box/Unity/downloads}"
BASE="https://download.unity3d.com/download_unity/${HASH}"

EDITOR_TAR="${DL}/Unity.tar.xz"
EDITOR_MD5=a91f970ef7dc8272af657bc3aeb5364a
IL2CPP_TAR="${DL}/UnitySetup-Linux-IL2CPP-Support-for-Editor-${VER}.tar.xz"
IL2CPP_MD5=a58b6749a47129e1422869fc9b7adac5
IOS_TAR="${DL}/UnitySetup-iOS-Support-for-Editor-${VER}.tar.xz"
IOS_MD5=145fb5b66e4d569b372b36a32c7d4a0f

mkdir -p "$DL" "$EDITOR_ROOT"

need() {
  sudo apt-get install -y \
    libgtk-3-0 libgdk-pixbuf-2.0-0 libglib2.0-0t64 \
    libxss1 libgbm1 libasound2 libpulse0 \
    libnss3 libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 libxkbcommon0 libxcomposite1 \
    libxdamage1 libxfixes3 libxrandr2 libxrender1 \
    libxi6 libxtst6 libxcursor1 \
    libglu1-mesa mesa-utils \
    xvfb x11-utils \
    clang lld make cmake \
    cpio zip unzip ca-certificates
}

fetch() {
  local url="$1" dest="$2" md5="$3"
  if [ -f "$dest" ]; then
    local got
    got=$(md5sum "$dest" | awk '{print $1}')
    if [ "$got" = "$md5" ]; then
      echo "ok $dest"
      return
    fi
    echo "md5 mismatch for $dest (got $got, expected $md5); re-downloading"
    rm -f "$dest"
  fi
  echo "downloading $url"
  curl -L --retry 5 --retry-delay 5 -o "$dest" "$url"
  local got
  got=$(md5sum "$dest" | awk '{print $1}')
  if [ "$got" != "$md5" ]; then
    echo "download md5 mismatch for $dest (got $got, expected $md5)" >&2
    exit 1
  fi
}

if [ "${1:-}" = "--deps-only" ]; then
  sudo apt-get update -y
  need
  exit 0
fi

sudo apt-get update -y
need

fetch "${BASE}/LinuxEditorInstaller/Unity.tar.xz" "$EDITOR_TAR" "$EDITOR_MD5"
fetch "${BASE}/LinuxEditorTargetInstaller/UnitySetup-Linux-IL2CPP-Support-for-Editor-${VER}.tar.xz" "$IL2CPP_TAR" "$IL2CPP_MD5"
fetch "${BASE}/LinuxEditorTargetInstaller/UnitySetup-iOS-Support-for-Editor-${VER}.tar.xz" "$IOS_TAR" "$IOS_MD5"

if [ ! -x "${EDITOR_ROOT}/Editor/Unity" ]; then
  echo "extracting editor to ${EDITOR_ROOT}"
  rm -rf "${EDITOR_ROOT}"
  mkdir -p "${EDITOR_ROOT}"
  tar -xJf "$EDITOR_TAR" -C "${EDITOR_ROOT}"
fi

# Module tarballs unpack an Editor/Data/... tree that merges into the editor root.
echo "extracting Linux IL2CPP support"
tar -xJf "$IL2CPP_TAR" -C "${EDITOR_ROOT}"
echo "extracting iOS build support (compile-only; not used to build iOS here)"
tar -xJf "$IOS_TAR" -C "${EDITOR_ROOT}"

if [ ! -x "${EDITOR_ROOT}/Editor/Unity" ]; then
  echo "Unity binary missing after extract: ${EDITOR_ROOT}/Editor/Unity" >&2
  ls -la "${EDITOR_ROOT}" >&2 || true
  exit 1
fi

echo "Unity ${VER} installed at ${EDITOR_ROOT}/Editor/Unity"
"${EDITOR_ROOT}/Editor/Unity" -version || true
ls -d "${EDITOR_ROOT}/Editor/Data/PlaybackEngines/"* 2>/dev/null || true
