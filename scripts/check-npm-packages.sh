#!/usr/bin/env bash
set -euo pipefail

# Installs tarballs, or the exact published versions, in a project outside the workspace.
# Usage: scripts/check-npm-packages.sh <version> [tarball-directory]
version="${1:?package version required}"
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
workspace="$(mktemp -d)"
trap 'rm -rf "$workspace"' EXIT
packages=()
for package in c4m-schema c4m-client c4m-preview; do
  if [[ -n "${2:-}" ]]; then
    packages+=("$(cd -- "$2" && pwd)/conradsix-$package-$version.tgz")
  else
    packages+=("@conradsix/$package@$version")
  fi
done
cp "$repo_root"/smoke/npm/* "$workspace/"
cd "$workspace"
npm install --ignore-scripts --no-audit --no-fund "${packages[@]}"
./node_modules/.bin/tsc --noEmit --strict --types node --target es2022 --module nodenext --moduleResolution nodenext consumer.ts
./node_modules/.bin/tsc --noEmit --strict --types node --target es2022 --module esnext --moduleResolution bundler consumer.ts
C4M_PACKAGE_VERSION="$version" node consumer.ts
