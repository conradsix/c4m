#!/usr/bin/env bash
set -euo pipefail

# Generates the client from the sample host's OpenAPI document, which has only the engine's endpoints. The host
# writes the document and exits, so no database is needed.
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
dotnet_dir="$(cd -- "$package_dir/../../../dotnet" && pwd)"

(cd "$dotnet_dir" && dotnet run --project samples/ConradSix.C4M.SampleHost -- --export-swagger-docs true > /dev/null)
cp "$dotnet_dir/samples/ConradSix.C4M.SampleHost/obj/openapi/c4m.json" "$package_dir/src/swagger.json"
yarn swagger-typescript-api generate --path "$package_dir/src/swagger.json" -o "$package_dir/src" -n index.ts
