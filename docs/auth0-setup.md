# Auth0 setup from scratch (Remaster Guru)

This guide assumes you **do not** have an Auth0 account yet. You will create a **free development tenant**, then register the **API** and **Web application** Remaster Guru needs.

**Behavior in code**

| `Auth0:Domain` (API) / Web Auth0 env | API auth | Web `/app/*` |
|--------------------------------------|----------|----------------|
| Unset / incomplete | `X-User-Id` header (local dev) | Open (dev client) |
| Configured | JWT Bearer (Auth0) | Login required |

---

## Part A — Create your Auth0 account and tenant

1. Open [https://auth0.com/signup](https://auth0.com/signup) and create an account (email or social).
2. When prompted to create a tenant:
   - **Tenant domain:** pick a short name (e.g. `remaster-guru-dev`). Your domain becomes something like `remaster-guru-dev.us.auth0.com`. This is **`Auth0:Domain`** / **`AUTH0_DOMAIN`** (hostname only, no `https://`).
   - **Region:** choose the closest region (US/EU/AU). This cannot be changed later without a new tenant.
   - **Account type:** **Development** (free tier) is enough for local dev and Hugging Face staging.
3. Finish onboarding until you land in the [Auth0 Dashboard](https://manage.auth0.com/).

Write down your tenant domain; you will use it in every env var below.

> **Not using an existing company tenant.** If you already use Auth0 elsewhere, still create a **new** dev tenant for Remaster Guru so callbacks, secrets, and test users stay isolated.

---

## Part B — Create the API (Resource Server)

The .NET API validates **access tokens** whose **audience** matches this API.

1. In the Dashboard left nav: **Applications → APIs**.
2. Click **+ Create API**.
3. Fill in:
   - **Name:** `Remaster Guru API`
   - **Identifier:** a stable URI string (not a real URL you must host). Example: `https://api.remasterguru.com`  
     This exact string is:
     - **`Auth0:Audience`** on the API
     - **`AUTH0_AUDIENCE`** on the Web app
   - **Signing Algorithm:** **RS256** (default).
4. Click **Create**.

> **Required before Web login works.** The Web SDK sends `AUTH0_AUDIENCE` on `/auth/login`. If this API does not exist in the tenant (or the **Identifier** does not match `AUTH0_AUDIENCE` / `Auth0:Audience` exactly), Auth0 fails immediately with `access_denied` and `Service not found: https://api.remasterguru.com` (or whatever audience you configured). The Next.js page may only show *An error occurred during the authorization flow.* — check the browser network tab on `/auth/callback` for the `error_description`. This is an Auth0 Dashboard gap, not a problem with your local Kestrel API process.

Optional: open the new API → **Permissions** and add scopes if you want custom ones later (v1 works with default `openid profile email` plus API authorization).

---

## Part C — Create the Web application (Regular Web)

Next.js uses the Auth0 SDK as a **server-side** (Regular Web) app, not a SPA.

1. **Applications → Applications → Create Application**.
2. **Name:** `Remaster Guru Web`.
3. **Application type:** **Regular Web Applications** → **Create**.
4. Open the app → **Settings** tab.

### C.1 — Note credentials

Copy these into a password manager (never commit them):

| Dashboard field | Environment variable |
|-----------------|----------------------|
| Domain | `AUTH0_DOMAIN` (same as tenant domain, e.g. `remaster-guru-dev.us.auth0.com`) |
| Client ID | `AUTH0_CLIENT_ID` |
| Client Secret | `AUTH0_CLIENT_SECRET` |

`AUTH0_ISSUER_BASE_URL` is optional if you set `AUTH0_DOMAIN`; if you use it, set `https://YOUR-TENANT.us.auth0.com` (with `https://`).

### C.2 — Application URIs (localhost + Hugging Face)

Auth0 accepts **multiple** URLs in each field, separated by commas. Configure **both** local and staging up front so you do not have to revisit the Dashboard when you deploy a Space.

Replace `YOUR-ORG-YOUR-SPACE` with your Hugging Face Space id (e.g. `matthewfmarshall-remasterguru` → origin `https://matthewfmarshall-remasterguru.hf.space`).

| Setting | Value (comma-separated) |
|---------|------------------------|
| **Allowed Callback URLs** | `http://localhost:3000/auth/callback`, `https://YOUR-ORG-YOUR-SPACE.hf.space/auth/callback` |
| **Allowed Logout URLs** | `http://localhost:3000`, `https://YOUR-ORG-YOUR-SPACE.hf.space` |
| **Allowed Web Origins** | `http://localhost:3000`, `https://YOUR-ORG-YOUR-SPACE.hf.space` |

Click **Save Changes** at the bottom of Settings.

> **HF Space URL:** After you create the Space on Hugging Face, the public URL is `https://<space-name>.hf.space`. Use that exact origin (no trailing slash) in all three fields and in `AUTH0_BASE_URL` / `APP_BASE_URL` when running on the Space.

### C.3 — Authorize the Web app to call the API

1. Still on **Remaster Guru Web** → **APIs** tab (or **Applications → APIs** → **Remaster Guru API** → **Machine To Machine** / **Application** tabs depending on UI version).
2. Ensure **Remaster Guru Web** is authorized to use **Remaster Guru API** (toggle on if shown).
3. Under **User Access** / scopes, enable at least what you need for login (defaults are usually fine for v1).

For **Regular Web** apps, the access token for your API is requested via the SDK using `AUTH0_AUDIENCE` (see Web env below), not only M2M grants.

---

## Part D — Test user (Dashboard)

External-user dev tenants often require explicit test users before email/password login works.

1. **User Management → Users → Create User**.
2. Set email and password (or use passwordless if you configure it later).
3. Use this account to log in at `http://localhost:3000/auth/login` once the Web app is configured.

---

## Part E — Configure RemasterGuru.Api

Set via user secrets, environment variables, or `appsettings` (do not commit secrets).

| appsettings / env | Example | Notes |
|-------------------|---------|--------|
| `Auth0:Domain` | `remaster-guru-dev.us.auth0.com` | Required to enable JWT auth |
| `Auth0:Audience` | `https://api.remasterguru.com` | Must match API **Identifier** |
| `App:WebBaseUrl` | `http://localhost:3000` or HF Space URL | CORS allowlist |
| `HuggingFace:SpaceUrl` | `https://YOUR-ORG-YOUR-SPACE.hf.space` | Optional exact HF origin |
| `Cors:AllowHuggingFaceSpaceHosts` | `true` | Allow any `*.hf.space` host (staging convenience) |

Double underscore for environment variables, e.g. `Auth0__Domain`, `Auth0__Audience`.

When `Auth0:Domain` is set, the API requires `Authorization: Bearer <access_token>` and upserts `Users` by Auth0 `sub` (`Auth0Subject` column).

**Local without Auth0:** leave `Auth0:Domain` empty; keep using `X-User-Id` (see Api README).

---

## Part F — Configure RemasterGuru.Web

Copy `.env.local.example` to `.env.local` and set:

| Variable | Purpose |
|----------|---------|
| `AUTH0_SECRET` | Session cookie encryption — generate once: `openssl rand -hex 32` |
| `AUTH0_DOMAIN` | Tenant domain (no scheme) |
| `AUTH0_CLIENT_ID` | From Web application settings |
| `AUTH0_CLIENT_SECRET` | From Web application settings |
| `AUTH0_AUDIENCE` | Same as API Identifier |
| `AUTH0_BASE_URL` or `APP_BASE_URL` | `http://localhost:3000` locally; HF Space URL on staging |

Legacy aliases still work: `AUTH0_ISSUER_BASE_URL` (`https://…`) if you prefer it over `AUTH0_DOMAIN`.

**With Auth0 configured**

- `/app/*` redirects unauthenticated users to `/auth/login`.
- Browser API traffic uses the Next.js BFF at `/api/v1/*` with a server-side access token.
- Set `API_INTERNAL_URL=http://localhost:5055` (or `http://127.0.0.1:5055`) so the BFF reaches Kestrel while the browser stays same-origin.

**Without Auth0 (local dev)**

- Omit Auth0 variables; keep `NEXT_PUBLIC_API_URL` and `NEXT_PUBLIC_DEV_USER_ID`.

---

## Part G — Quick verification

1. Start API and Web (see each repo README).
2. Open `http://localhost:3000/auth/login` → sign in with your test user.
3. Open `http://localhost:3000/app/albums` → should load albums (empty list is OK).
4. Optional: call `GET http://localhost:5055/api/v1/credits/balance` with a Bearer token from the Auth0 Debugger or your session (advanced).

---

## Part H — Hugging Face Space checklist

When the Space is live:

1. Add the Space origin to **Allowed Callback / Logout / Web Origins** (if you did not already in Part C).
2. Set Space secrets (Hub UI or CLI): same Auth0 vars as Web, with `AUTH0_BASE_URL` / `APP_BASE_URL` = `https://<space>.hf.space`.
3. Set API secrets on the API process in the container: `Auth0__Domain`, `Auth0__Audience`, `App__WebBaseUrl`, `Cors__AllowHuggingFaceSpaceHosts=true`.
4. Set `API_INTERNAL_URL=http://127.0.0.1:5055` (or whatever port Kestrel uses inside the container).

See `RemasterGuru.Web/deploy/huggingface/README.md` for container deploy steps.

---

## Reference — Auth routes (Web)

| Route | Purpose |
|-------|---------|
| `/auth/login` | Start login (optional `?returnTo=/app/albums`) |
| `/auth/logout` | Log out |
| `/auth/callback` | OAuth callback (must match Dashboard) |
