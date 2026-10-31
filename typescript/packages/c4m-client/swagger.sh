#!/usr/local/bin/bash

# The C4M engine's own client, from the engine's OpenAPI document. Realty's CMS API serves it beside realty's own.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PORT=${1:-5100}

wget -O "${SCRIPT_DIR}/src/swagger.json" http://localhost:${PORT}/swagger/c4m/swagger.json &&
    npx --yes swagger-typescript-api@13.2.7 generate --path "${SCRIPT_DIR}/src/swagger.json" -o "${SCRIPT_DIR}/src" -n index.ts
