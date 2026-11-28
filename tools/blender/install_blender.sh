#!/usr/bin/env bash
# Fetches the exact Blender build normalize_weapon.py is tested against.
# No sudo: Blender's linux tarball is self-contained and runs unprivileged.
set -euo pipefail

BLENDER_DIR="/workspace/tools/blender-4.2.3-linux-x64"
BLENDER_BIN="$BLENDER_DIR/blender"
TARBALL_URL="https://download.blender.org/release/Blender4.2/blender-4.2.3-linux-x64.tar.xz"
TARBALL_PATH="/workspace/tools/blender-4.2.3-linux-x64.tar.xz"

if [ -x "$BLENDER_BIN" ]; then
  echo "Blender already installed at $BLENDER_BIN"
  exit 0
fi

mkdir -p /workspace/tools
curl -L --fail -o "$TARBALL_PATH" "$TARBALL_URL"
tar -xf "$TARBALL_PATH" -C /workspace/tools
rm -f "$TARBALL_PATH"

echo "Installed Blender at $BLENDER_BIN"
