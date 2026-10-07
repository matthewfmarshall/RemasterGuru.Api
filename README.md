# Remaster Guru API

ASP.NET Core minimal API plus a background worker for remaster jobs (SQL Server + EF Core).

## Projects

| Project | Role |
|---------|------|
| `src/RemasterGuru.Api` | HTTP API (`/api/v1`) |
| `src/RemasterGuru.Worker` | Polls queued remaster jobs every 5s |
| `src/RemasterGuru.Domain` | Entities and enums |
| `src/RemasterGuru.Infrastructure` | EF Core SQL Server, repositories, local blob storage |

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 10.x
- [Docker](https://www.docker.com/) (for local SQL Server)

## Database (Docker SQL Server)

```bash
cp .env.example .env   # optional; compose defaults match .env.example
docker compose up -d
docker compose ps    # wait until sqlserver is healthy
```

Default SA password (dev only): `RemasterGuru_Dev1!` — set `SA_PASSWORD` in `.env`.

**Connection string** (also in `appsettings.Development.json`):

```
Server=localhost,1433;Database=RemasterGuru;User Id=sa;Password=RemasterGuru_Dev1!;TrustServerCertificate=True;Encrypt=False
```

Apply EF Core migrations:

```bash
dotnet ef database update \
  --project src/RemasterGuru.Infrastructure \
  --startup-project src/RemasterGuru.Api
```

The API and Worker call `Migrate` on startup, so a fresh database is created automatically when SQL Server is up.

Create a new migration after model changes:

```bash
dotnet ef migrations add <Name> \
  --project src/RemasterGuru.Infrastructure \
  --startup-project src/RemasterGuru.Api
```

Install the EF CLI once if needed: `dotnet tool install --global dotnet-ef`

## Run locally

From this repository root:

```bash
# API (http://localhost:5000)
dotnet run --project src/RemasterGuru.Api

# Worker (processes remaster jobs; same SQL Server database)
dotnet run --project src/RemasterGuru.Worker
```

- Health: `GET http://localhost:5000/health`
- Swagger UI (Development): `http://localhost:5000/swagger`
- OpenAPI 3 (Swashbuckle): `http://localhost:5000/swagger/v1/swagger.json`
- Committed contract: `openapi/v1.json` (regenerate when endpoints change)

### Export OpenAPI

With the API running:

```bash
curl -fsS http://localhost:5000/swagger/v1/swagger.json -o openapi/v1.json
```

Or use the helper script (curl first, then `swagger tofile` fallback):

```bash
chmod +x scripts/export-openapi.sh
./scripts/export-openapi.sh
```

Alternative without a running server:

```bash
dotnet build src/RemasterGuru.Api
dotnet tool install --global Swashbuckle.AspNetCore.Cli
swagger tofile src/RemasterGuru.Api/bin/Debug/net10.0/RemasterGuru.Api.dll v1 -o openapi/v1.json
```

## Dev authentication

Send a GUID on every `/api/v1/*` request:

```http
X-User-Id: 00000000-0000-4000-8000-000000000001
```

The API auto-creates a `User` row on first request. Replace with JWT/cookies before production.

## Data files (gitignored)

| Path | Purpose |
|------|---------|
| `data/blobs/` | Local upload blobs (v1) |
| `.env` | Docker `SA_PASSWORD` (copy from `.env.example`) |

## Uploads (v1)

`POST /api/v1/assets/upload-sessions` returns an `uploadUrl` like:

`http://localhost:5000/api/v1/internal/upload/{sessionId}`

Upload bytes with `PUT` (no `X-User-Id` required). Then register the asset:

`POST /api/v1/albums/{albumId}/assets` with `{ "sessionId": "..." }`.

## API v1 summary

| Area | Endpoints |
|------|-----------|
| Albums | `GET/POST /api/v1/albums`, `GET/PATCH/DELETE /api/v1/albums/{id}` |
| Assets | `POST /api/v1/assets/upload-sessions`, `GET/POST /api/v1/albums/{id}/assets`, `GET/DELETE /api/v1/assets/{id}` |
| Remaster | `POST /api/v1/assets/{id}/remaster-jobs`, `GET /api/v1/remaster-jobs/{id}`, `GET /api/v1/assets/{id}/remaster-jobs` |
| Credits | `GET /api/v1/credits/balance`, `GET /api/v1/credits/ledger`, `POST /api/v1/credits/grants` (Development only) |
| Orders | `POST /api/v1/albums/{id}/orders`, `GET /api/v1/orders`, `GET /api/v1/orders/{id}` |

Full contract shapes: Project Context doc `docs/api-v1.md`.

### Example: create album and enqueue remaster

```bash
export USER_ID="00000000-0000-4000-8000-000000000001"

# Album
ALBUM=$(curl -sS -X POST http://localhost:5000/api/v1/albums \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d '{"title":"Mom & Dad","templateId":"hardcover-24"}')
echo "$ALBUM"
ALBUM_ID=$(echo "$ALBUM" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")

# Upload session + register (optional PUT upload to uploadUrl)
SESSION=$(curl -sS -X POST http://localhost:5000/api/v1/assets/upload-sessions \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d "{\"albumId\":\"$ALBUM_ID\",\"fileName\":\"scan.jpg\",\"contentType\":\"image/jpeg\",\"byteSize\":1234}")
ASSET_ID=$(echo "$SESSION" | python3 -c "import sys,json; print(json.load(sys.stdin)['assetId'])")
SESSION_ID=$(echo "$SESSION" | python3 -c "import sys,json; print(json.load(sys.stdin)['sessionId'])")
curl -sS -X POST "http://localhost:5000/api/v1/albums/$ALBUM_ID/assets" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d "{\"sessionId\":\"$SESSION_ID\"}"

# First 1k remaster is free (free taste); run Worker to complete job
curl -sS -X POST "http://localhost:5000/api/v1/assets/$ASSET_ID/remaster-jobs" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d '{"preset":"damage","targetResolution":"1k"}'
```

## Configuration

| Key | Default |
|-----|---------|
| `ConnectionStrings__Default` | SQL Server on `localhost,1433` (see above) |
| `Api__PublicBaseUrl` | `http://localhost:5000` |
| `Storage__BlobRoot` | `data/blobs` |
| `XAI_API_KEY` | Reserved for future xAI integration in the worker |

## VS Code

Use `.vscode/launch.json` to start the API and Worker.
