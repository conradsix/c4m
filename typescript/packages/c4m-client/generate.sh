#!/usr/bin/env bash
set -euo pipefail

# Generates the client from the sample host's OpenAPI document, which has only the engine's endpoints. The host
# writes the document and exits, so no database is needed.
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
sample_dir="$(cd -- "$package_dir/../../../samples/ConradSix.C4M.SampleHost" && pwd)"

dotnet run --project "$sample_dir" -- --export-swagger-docs true > /dev/null
cp "$sample_dir/obj/openapi/c4m.json" "$package_dir/src/swagger.json"
yarn swagger-typescript-api generate --path "$package_dir/src/swagger.json" -o "$package_dir/src" -n index.ts
