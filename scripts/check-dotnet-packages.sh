#!/usr/bin/env bash
set -euo pipefail

# Runs a third-party host outside the repo, using packages from a local feed or nuget.org.
# Usage: scripts/check-dotnet-packages.sh <version> <net10.0|net11.0> [local-feed]
version="${1:?package version required}"
framework="${2:?target framework required}"
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
workspace="$(mktemp -d)"
container=""
cleanup() {
  if [[ -n "$container" ]]; then docker rm -f "$container" > /dev/null 2>&1 || true; fi
  rm -rf "$workspace"
}
trap cleanup EXIT

case "$framework" in
  net10.0) sdk=10.0.400; testhost=10.0.14 ;;
  net11.0) sdk=11.0.100; testhost=11.0.0 ;;
  *) echo "Unsupported framework: $framework" >&2; exit 1 ;;
esac
sources=(--source https://api.nuget.org/v3/index.json)
if [[ -n "${3:-}" ]]; then sources+=(--source "$(cd -- "$3" && pwd)"); fi
cp "$repo_root"/smoke/dotnet/* "$workspace/"
cat > "$workspace/global.json" <<EOF
{"sdk":{"version":"$sdk","rollForward":"latestFeature"}}
EOF
cd "$workspace"
properties=(-p:C4MPackageVersion="$version" -p:ConsumerTargetFramework="$framework" -p:TestHostPackageVersion="$testhost")
# A separate package cache prevents an earlier build at the same CI version masking a broken package.
dotnet restore "${properties[@]}" "${sources[@]}" --packages "$workspace/packages"
dotnet build --configuration Release --no-restore "${properties[@]}"

container="$(docker run --detach --rm -e POSTGRES_PASSWORD=c4m -e POSTGRES_USER=c4m -e POSTGRES_DB=c4m \
  -p 127.0.0.1::5432 "${C4M_POSTGRES_IMAGE:-postgres:18}")"
ready=false
for ((attempt=0; attempt<60; attempt++)); do
  if docker exec "$container" pg_isready -U c4m -d c4m > /dev/null 2>&1; then ready=true; break; fi
  sleep 1
done
if [[ "$ready" != true ]]; then docker logs "$container"; exit 1; fi
port="$(docker port "$container" 5432/tcp)"
export C4M_CONNECTION_STRING="Host=127.0.0.1;Port=${port##*:};Database=c4m;Username=c4m;Password=c4m"
export C4M_PACKAGE_VERSION="$version"
dotnet run --configuration Release --no-build "${properties[@]}"
