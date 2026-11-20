#!/usr/bin/env bash
# Fail the iOS workflow with a readable list when a GitHub Actions secret is unset.
# Secrets are passed in as environment variables. This script never prints their values.
set -u

missing=()
need() {
  local name="$1"
  local value="${!name-}"
  if [ -z "$value" ]; then
    missing+=("$name")
  fi
}

need UNITY_EMAIL
need UNITY_PASSWORD
if [ -n "${UNITY_LICENSE-}" ] && [ -n "${UNITY_SERIAL-}" ]; then
  echo "::error::Both UNITY_LICENSE and UNITY_SERIAL are set. Use UNITY_LICENSE for a Personal license, or UNITY_SERIAL for Unity Pro/Plus, not both. Remove the one you are not using."
  exit 1
fi
if [ -z "${UNITY_LICENSE-}" ] && [ -z "${UNITY_SERIAL-}" ]; then
  missing+=("UNITY_LICENSE (Personal .ulf) or UNITY_SERIAL (Pro/Plus serial)")
fi

need APP_STORE_CONNECT_KEY_ID
need APP_STORE_CONNECT_ISSUER_ID
need APP_STORE_CONNECT_KEY_BASE64
need APPLE_TEAM_ID
need MATCH_GIT_URL
need MATCH_GIT_TOKEN
need MATCH_PASSWORD

if [ "${#missing[@]}" -gt 0 ]; then
  echo "::error::Missing GitHub Actions secrets for the iOS TestFlight workflow."
  echo "Add these under Settings → Secrets and variables → Actions (see docs/IOS_BUILD.md):"
  for name in "${missing[@]}"; do
    echo "  - $name"
  done
  exit 1
fi

echo "All iOS TestFlight secrets are present."
