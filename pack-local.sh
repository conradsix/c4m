#!/usr/bin/env bash
set -euo pipefail

# Packs the NuGet and npm packages for a host to try before a release. The version is the patch after the latest
# release tag, so it sorts above every release so far. Prints only the version.
#
# Usage: ./pack-local.sh [version]
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
nuget_feed="${C4M_LOCAL_NUGET_FEED:-"$HOME/.nuget/local-c4m"}"
npm_dir="${C4M_LOCAL_NPM_DIR:-"$HOME/.npm-local/c4m"}"
configuration="${CONFIGURATION:-Release}"

if [[ $# -gt 0 ]]; then
  version="$1"
else
  latest="$(git -C "$repo_root" describe --tags --abbrev=0 --match 'v*.*.*' 2> /dev/null || echo v0.0.0)"
  core="${latest#v}"
  core="${core%%-*}"
  IFS=. read -r major minor patch <<< "$core"
  version="$major.$minor.$((patch + 1))-local.$(date -u +%Y%m%d%H%M%S)"
fi

projects=(
  "src/ConradSix.C4M/ConradSix.C4M.csproj"
  "src/ConradSix.C4M.Testing/ConradSix.C4M.Testing.csproj"
)

mkdir -p "$nuget_feed" "$npm_dir"

{
  printf 'Packing C4M local packages\n'
  printf '  Version: %s\n' "$version"
  printf '  NuGet feed: %s\n' "$nuget_feed"
  printf '  npm tarballs: %s\n' "$npm_dir"
  printf '  Configuration: %s\n' "$configuration"
} >&2

cd "$repo_root"

for project in "${projects[@]}"; do
  printf 'Packing %s\n' "$project" >&2
  dotnet pack "$project" \
    --configuration "$configuration" \
    --output "$nuget_feed" \
    -p:Version="$version" >&2
done

printf 'Packing the npm packages\n' >&2
typescript/pack.sh "$version" "$npm_dir" >&2

printf '%s\n' "$version"
