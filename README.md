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

**Local HTTP URL:** `http://localhost:5055` (`launchSettings.json` profile `http`). On macOS, port **5000** is often used by AirPlay Receiver; if the Web app shows “Failed to fetch”, ensure `NEXT_PUBLIC_API_URL` in the Web repo matches this port (not `5000`).

From this repository root:

```bash
# API (http://localhost:5055)
dotnet run --project src/RemasterGuru.Api

# Worker (processes remaster jobs; same SQL Server database)
dotnet run --project src/RemasterGuru.Worker
```

- Health: `GET http://localhost:5055/health`
- Swagger UI (Development): `http://localhost:5055/swagger`
- OpenAPI 3 (Swashbuckle): `http://localhost:5055/swagger/v1/swagger.json`
- Committed contract: `openapi/v1.json` (regenerate when endpoints change)

### Export OpenAPI

With the API running:

```bash
curl -fsS http://localhost:5055/swagger/v1/swagger.json -o openapi/v1.json
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

`http://localhost:5055/api/v1/internal/upload/{sessionId}`

Upload bytes with `PUT` (no `X-User-Id` required). Then register the asset:

`POST /api/v1/albums/{albumId}/assets` with `{ "sessionId": "..." }`.

## API v1 summary

| Area | Endpoints |
|------|-----------|
| Albums | `GET/POST /api/v1/albums`, `GET/PATCH/DELETE /api/v1/albums/{id}` |
| Assets | `POST /api/v1/assets/upload-sessions`, `GET/POST /api/v1/albums/{id}/assets`, `GET/DELETE /api/v1/assets/{id}`, `GET /api/v1/assets/{id}/original`, `GET /api/v1/assets/{id}/restored` |
| Remaster | `POST /api/v1/assets/{id}/remaster-jobs`, `GET /api/v1/remaster-jobs/{id}`, `GET /api/v1/assets/{id}/remaster-jobs` |
| Credits | `GET /api/v1/credits/balance`, `GET /api/v1/credits/ledger`, `POST /api/v1/credits/grants` (Development only) |
| Orders | `POST /api/v1/albums/{id}/orders`, `GET /api/v1/orders`, `GET /api/v1/orders/{id}`, `POST /api/v1/orders/{id}/submit-to-lab` |
| Checkout (Stripe) | `GET /api/v1/checkout/products`, `POST /api/v1/checkout/sessions`, `POST /api/v1/webhooks/stripe` |
| Print (RPI stub) | `POST /api/v1/webhooks/rpi` |

Full contract shapes: Project Context doc `docs/api-v1.md`.

### Example: create album and enqueue remaster

```bash
export USER_ID="00000000-0000-4000-8000-000000000001"

# Album
ALBUM=$(curl -sS -X POST http://localhost:5055/api/v1/albums \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d '{"title":"Mom & Dad","templateId":"hardcover-24"}')
echo "$ALBUM"
ALBUM_ID=$(echo "$ALBUM" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")

# Upload session + register (optional PUT upload to uploadUrl)
SESSION=$(curl -sS -X POST http://localhost:5055/api/v1/assets/upload-sessions \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d "{\"albumId\":\"$ALBUM_ID\",\"fileName\":\"scan.jpg\",\"contentType\":\"image/jpeg\",\"byteSize\":1234}")
ASSET_ID=$(echo "$SESSION" | python3 -c "import sys,json; print(json.load(sys.stdin)['assetId'])")
SESSION_ID=$(echo "$SESSION" | python3 -c "import sys,json; print(json.load(sys.stdin)['sessionId'])")
curl -sS -X POST "http://localhost:5055/api/v1/albums/$ALBUM_ID/assets" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d "{\"sessionId\":\"$SESSION_ID\"}"

# First 1k remaster is free (free taste); run Worker to complete job
curl -sS -X POST "http://localhost:5055/api/v1/assets/$ASSET_ID/remaster-jobs" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: $USER_ID" \
  -d '{"preset":"damage","targetResolution":"1k"}'
```

## Configuration

| Key | Default |
|-----|---------|
| `ConnectionStrings__Default` | SQL Server on `localhost,1433` (see above) |
| `Api__PublicBaseUrl` | `http://localhost:5055` |
| `Storage__BlobRoot` | `data/blobs` |
| `XAI_API_KEY` | xAI API key for real remasters in the Worker (see below) |
| `Stripe__SecretKey` | Stripe test secret key (`sk_test_…`) on the **API** |
| `Stripe__WebhookSecret` | Stripe webhook signing secret (`whsec_…`) on the **API** |
| `Stripe__PublishableKey` | Optional; checkout is server-hosted (Web does not need this for v1) |
| `App__WebBaseUrl` | Web app origin for Stripe success/cancel URLs (default `http://localhost:3000`) |
| `Rpi__ApiKey` | RPI API key (optional; without key + base URL the provider runs in **dev stub** mode) |
| `Rpi__BaseUrl` | RPI API base URL (optional) |
| `Rpi__WebhookSecret` | When set, `POST /api/v1/webhooks/rpi` requires header `X-Rpi-Webhook-Secret` |
| `Print__AutoSubmitInDevelopment` | `true` to poll `paid` / `awaiting_fulfillment` orders and auto-submit to lab (Development only) |
| `Print__AutoSubmitIntervalSeconds` | Poll interval for auto-submit (default `30`) |

### xAI remaster (Worker)

The Worker calls xAI **JSON** image edits (`POST https://api.x.ai/v1/images/edits`, model `grok-imagine-image-2.0`). Without a key it logs a one-time warning and copies the original bytes as a stub restored version so the UI flow still works.

Set the key via environment variable or .NET user secrets on the **Worker** project:

```bash
export XAI_API_KEY="xai-…"

# or
dotnet user-secrets set XAI_API_KEY "xai-…" \
  --project src/RemasterGuru.Worker
```

Restart the Worker after changing the key.

### Stripe checkout (API, test mode)

Book checkout uses [Stripe Checkout](https://stripe.com/docs/checkout) in **payment** mode. Products: `book-restore-bundle` (~$89, includes 8 remaster credits) and `book-album-only` (~$59). US shipping only.

1. In the [Stripe Dashboard](https://dashboard.stripe.com/test/apikeys), copy the **test** secret key and (optionally) publishable key.
2. Configure the API (never commit keys):

```bash
dotnet user-secrets set Stripe:SecretKey "sk_test_…" \
  --project src/RemasterGuru.Api
dotnet user-secrets set Stripe:WebhookSecret "whsec_…" \
  --project src/RemasterGuru.Api
```

Or export `Stripe__SecretKey` and `Stripe__WebhookSecret` in your shell.

3. **Local webhooks** (optional but needed to mark orders paid and grant credits):

```bash
stripe listen --forward-to http://localhost:5055/api/v1/webhooks/stripe
```

Use the signing secret printed by `stripe listen` as `Stripe:WebhookSecret` while testing locally.

4. Create a session (album must be `ready_for_print`, `shippingCountry` must be `US`):

```bash
curl -sS -X POST http://localhost:5055/api/v1/checkout/sessions \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 84AD0816-39F0-480F-93F9-2D370D27CA7C" \
  -d '{"albumId":"<album-uuid>","productSku":"book-restore-bundle","shippingCountry":"US"}'
```

Open the returned `url` in a browser; use test card `4242 4242 4242 4242`.

### RPI print fulfillment (B9 stub)

v1 targets **[RPI](https://www.rpiprint.com/)** for the 24-page US hardcover SKU. B9 does **not** call the real RPI HTTP API yet — it logs a structured payload and assigns a fake `labOrderId` like `rpi-stub-{guid}` unless you later wire production credentials.

**Stub vs production**

| Mode | When | Behavior |
|------|------|----------|
| Dev stub | `Rpi:ApiKey` or `Rpi:BaseUrl` missing | Log payload + `rpi-stub-*` lab id |
| Credentials present | Both set | Still stub in B9; logs that real API is not wired |

**Manual test sequence** (after SQL Server + API are running):

1. Create album, mark `ready_for_print`, run Stripe checkout + `stripe listen` webhook → order becomes `awaiting_fulfillment`.
2. Submit to lab (requires `X-User-Id`):

```bash
curl -sS -X POST "http://localhost:5055/api/v1/orders/<order-id>/submit-to-lab" \
  -H "X-User-Id: <user-guid>"
```

3. Simulate RPI status webhook (optional secret):

```bash
curl -sS -X POST http://localhost:5055/api/v1/webhooks/rpi \
  -H "Content-Type: application/json" \
  -H "X-Rpi-Webhook-Secret: <same-as-Rpi__WebhookSecret-if-set>" \
  -d '{"labOrderId":"<labOrderId-from-step-2>","status":"in_production"}'

curl -sS -X POST http://localhost:5055/api/v1/webhooks/rpi \
  -H "Content-Type: application/json" \
  -d '{"labOrderId":"<labOrderId>","status":"shipped","trackingUrl":"https://example.com/track/1"}'
```

4. Poll `GET /api/v1/orders/{orderId}` for `submitted_to_lab`, `in_production`, or `shipped`.

Optional: set `Print__AutoSubmitInDevelopment=true` in Development to auto-submit eligible orders on a timer (no Worker required).

## VS Code

Use `.vscode/launch.json` to start the API and Worker.
