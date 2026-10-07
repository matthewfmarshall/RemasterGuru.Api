#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ROOT}/openapi/v1.json"
API_URL="${API_URL:-http://localhost:5055}"
mkdir -p "$(dirname "$OUT")"

if curl -fsS "${API_URL}/swagger/v1/swagger.json" -o "$OUT"; then
  echo "Wrote ${OUT} from running API at ${API_URL}"
  exit 0
fi

echo "API not reachable at ${API_URL}; building and using Swashbuckle CLI..." >&2
dotnet build "${ROOT}/src/RemasterGuru.Api/RemasterGuru.Api.csproj" -c Debug --no-restore 2>/dev/null \
  || dotnet build "${ROOT}/src/RemasterGuru.Api/RemasterGuru.Api.csproj" -c Debug

DLL="${ROOT}/src/RemasterGuru.Api/bin/Debug/net10.0/RemasterGuru.Api.dll"
SWAGGER="${HOME}/.dotnet/tools/swagger"
if [[ ! -x "$SWAGGER" ]]; then
  dotnet tool install --global Swashbuckle.AspNetCore.Cli
fi

"$SWAGGER" tofile "$DLL" v1 --output "$OUT"
echo "Wrote ${OUT} via swagger tofile"
