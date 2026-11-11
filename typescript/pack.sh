#!/usr/bin/env bash
set -euo pipefail

# Builds the npm packages at one version and packs them into a directory, as pack-local.sh and the release workflow
# need them. The package.json files are put back afterwards.
#
# Usage: typescript/pack.sh <version> <output-dir>
version="${1:?version required}"
output="$(mkdir -p -- "${2:?output directory required}" && cd -- "$2" && pwd)"
workspace="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
packages=(c4m-schema c4m-client c4m-preview)

cd "$workspace"

backup="$(mktemp -d)"
restore() {
  for package in "${packages[@]}"; do
    cp "$backup/$package.json" "packages/$package/package.json"
  done
  rm -rf "$backup"
}
trap restore EXIT

for package in "${packages[@]}"; do
  cp "packages/$package/package.json" "$backup/$package.json"
  (cd "packages/$package" && npm pkg set version="$version")
done

yarn build
for package in "${packages[@]}"; do
  (cd "packages/$package" && yarn pack --out "$output/conradsix-$package-$version.tgz")
done
