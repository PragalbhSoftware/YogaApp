# Yoga Marketplace

A service-booking marketplace. Customers sign in with a phone OTP, pick a city and neighbourhood, browse verified providers, and pay at booking (Razorpay). Providers register as **Pending** until an admin verifies them, then publish Home, Studio, or Online slots and accept or decline requests. Admins oversee providers, users, bookings, payments, payouts, and the city/area catalog.

Yoga is the first `Category`. The model (Provider, Service, Category, Booking) is generic so another category can follow. The product opens city by city across India. **Cities and areas are data** (admin Settings), never part of titles, headings, or code defaults. Session times are India time (`Asia/Kolkata`).

## Solution

| Project | Role |
| --- | --- |
| `src/YogaMarketplace.Client` | React SPA (Vite, TypeScript, MUI, Tailwind, TanStack Query, Zustand). The only front end. |
| `src/YogaMarketplace.Api` | ASP.NET Core 8 Web API: OTP/JWT, browse, slots, book and pay, provider handshake, admin |
| `src/YogaMarketplace.Domain` | Entities and rules (booking, cancellation, provider approval, user blocking, catalog) |
| `src/YogaMarketplace.Infrastructure` | EF Core, SQL Server, migrations, seed |
| `tests/YogaMarketplace.Api.Tests` | Domain rules and API tests (SQLite) |

Flow is controllers to services to EF Core. No CQRS and no message bus.

## Run locally

Requires the .NET 8 SDK and Node 20.19+ (CI uses Node 22).

### 1. SQL Server

Docker:

```bash
docker compose up -d
```

That publishes `localhost:1433` with the SA password in `appsettings.Development.json`.

Or a local SQL Server / LocalDB instance, overriding the connection string with an environment variable:

```powershell
$env:ConnectionStrings__Default = "Server=(localdb)\mssqllocaldb;Database=YogaMarketplace;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

### 2. API (port 5080)

Development applies migrations and seeds on startup (`Database:AutoMigrate`, `Seed:DemoData`).

```bash
dotnet tool restore
dotnet run --project src/YogaMarketplace.Api --launch-profile http
```

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- Liveness / database: `http://localhost:5080/health`, `/health/ready`

### 3. Client (port 5173)

```bash
cd src/YogaMarketplace.Client
npm ci
npm run dev
```

Open `http://localhost:5173/login`.

In development `VITE_API_BASE_URL` is empty, so the client calls its own origin and the **Vite dev proxy** (`vite.config.ts`) forwards `/api` and `/health` to `http://localhost:5080`. No CORS is involved.

### Dev sign-in

SMS is a log stub. In Development the OTP is always `123456` and the API returns it as `devCode`.

| Who | Phone | Lands on |
| --- | --- | --- |
| Seeded provider (Ananya Desai) | `+919876543210` | `/instructor` |
| Seeded admin | `+919000000001` | `/admin` |
| Any new number | sign up with name and gender | `/` |

## Configuration

### Client (`src/YogaMarketplace.Client`)

Only public values belong here; everything prefixed `VITE_` ends up in the browser bundle.

| Variable | Purpose |
| --- | --- |
| `VITE_API_BASE_URL` | API origin, e.g. `https://api.example.com`. Leave empty to call the same origin (dev proxy, or a production reverse proxy). Read at **build** time. |

See `.env.example`. Vite loads `.env.development`, `.env.staging` (`vite build --mode staging`), and `.env.production` when they exist. The single-site publish needs none of them: it builds with `VITE_API_BASE_URL` empty.

### API

Set in `appsettings.*.json`, user secrets, or environment variables (`Section__Key`).

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Default` | SQL Server |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | Token signing. The app refuses to start without `Jwt:Key`. |
| `Jwt:ExpiresMinutes` | Access token lifetime (default 15). The client renews it silently with the refresh cookie. |
| `RefreshToken:LifetimeDays`, `RefreshToken:ReuseGraceSeconds` | Refresh cookie lifetime (default 30 days) and how long a just-rotated token gets `409` (a parallel tab) before it counts as reuse (default 30 s) |
| `RefreshToken:CookieSecure`, `RefreshToken:CookieSameSite` | Cookie flags. Defaults are `true` and `Strict`; Development and Testing turn `CookieSecure` off for plain HTTP. Use `None` (with HTTPS) only if the SPA and API are on different sites. |
| `Otp:Pepper`, `Otp:ExposeCode`, `Otp:UseFixedCode` | OTP hashing and the dev shortcuts (forced off in Production) |
| `Otp:DemoPhones`, `Otp:DemoCode` | Demo site sign-in: the listed phones (comma-separated) accept the 6-digit demo code; other phones get a normal OTP. For Staging and Development only: **Production refuses to start when either is set.** |
| `Cors:AllowedOrigins` | Array of browser origins allowed to call the API cross-origin. Development allows `http://localhost:5173`. **Production refuses to start with an empty list.** |
| `Razorpay:KeyId`, `Razorpay:KeySecret`, `Razorpay:WebhookSecret` | Payment gateway. Never commit live keys. |
| `Razorpay:UseFakeGateway` | Development and tests issue `order_fake_…` ids and capture locally (`/api/bookings/local-confirm`). Production refuses to start when this is on. |
| `Database:AutoMigrate`, `Seed:DemoData` | Off in Production |
| `Jobs:Enabled`, `Jobs:PollSeconds`, `Jobs:BatchSize`, `Jobs:LockMinutes` | Background job runner (defaults: on, 15 s, 10 jobs per poll, 5 min lock). Off in Testing. |
| `DataProtection:KeysPath` | Folder for the data protection key ring, so keys survive app pool recycles and redeploys. Full path outside the site folder. **Required in Production.** |
| `Storage:RootPath` | Folder for uploaded photos and certificates (default `App_Data/uploads`). Production should use a full path outside the site folder. |

Environment variable form for an array: `Cors__AllowedOrigins__0=https://app.example.com`.

## Production API routing for the SPA

The build output is static (`npm run build` → `dist/`). Production has no Vite proxy, so choose one of:

**Single site (IIS/Plesk).** `dotnet publish src/YogaMarketplace.Api` also builds the SPA and copies `dist/` into the API's `wwwroot`. The API then serves the app: files from `wwwroot`, `index.html` for client routes such as `/bookings/123`, and a JSON `404` for unknown `/api/*` routes. `index.html` is sent with `no-cache` and `/assets/*` (fingerprinted) with a one-year `immutable` cache. API responses keep the strict `default-src 'none'` CSP; the app's pages get a CSP that allows its own scripts and styles, Razorpay checkout and YouTube embeds. See [Deploy to Plesk (single site)](#deploy-to-plesk-single-site). Use `-p:SkipSpaBuild=true` to publish the API alone.

**Option A: same origin behind a proxy.** Serve `dist/` and reverse-proxy `/api` to the API from the same host. Build with `VITE_API_BASE_URL` empty. No CORS needed. Example Nginx:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;

  location /api/ {
    proxy_pass http://api:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }

  location / {
    try_files $uri /index.html;
  }
}
```

`try_files … /index.html` keeps deep links such as `/teachers/123` working on refresh.

**Option B: separate API origin.** Build with `VITE_API_BASE_URL=https://api.example.com` and add the SPA origin to the API: `Cors__AllowedOrigins__0=https://app.example.com`. CORS allows credentials so the refresh cookie is sent. The SPA host still needs the `index.html` fallback for client routes.

## Tests and checks

```bash
dotnet test YogaMarketplace.sln

cd src/YogaMarketplace.Client
npm run typecheck
npm run lint
npm test
npm run build
```

API tests build the model with SQLite; the app itself uses SQL Server. CI (`.github/workflows/ci.yml`) runs both sets.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api
```

New columns must be nullable or defaulted, and every migration needs a working `Down()`. Production uses the idempotent script; see **Deploy guide → Database migrations**.

## Money and concurrency rules

- Amounts are rounded to 2 decimals; Razorpay gets integer paise.
- State is saved to the database **before** calling Razorpay (refunds run inside a transaction: save, call the gateway, commit).
- `Booking.Status`, `Payment.Status`, `User.IsBlocked`, `RefreshToken.RevokedAt`, and `PlatformSettings.Version` are EF concurrency tokens. A lost race returns `409`.
- Times are India time (`Asia/Kolkata`); cancellation windows are measured from the session start in that zone.

## Platform settings

Business values live in one typed `PlatformSettings` row that admins edit under **Settings**. Nothing is hardcoded.

| Setting | Used for |
| --- | --- |
| Commission % | Platform share of each payout (completed, no-show, late cancel) |
| Convenience fee (flat ₹) | Added to the session price at checkout |
| Free cancel window (hours, 0–168) | Late cancel starts this many hours before the session. `0` means cancelling is always free. |
| Late-cancel fee (% or flat ₹) | Kept on a late cancel, computed on the session price only and never more than it |
| Free reschedule window (hours) | Reschedule is refused inside it. Kept in the API but hidden from the Settings page until reschedule rules are reviewed. |
| Payout cycle (weekly / biweekly) | Which payouts `/payouts/export` may claim |
| Banner title, subtitle, offer | Home hero text. Empty fields fall back to the built-in copy. |
| Convenience fee on credit bookings (off) | When on, a credit booking pays the fee through Razorpay |
| Instructor pay on credits (pack price ÷ sessions) | Or the instructor's normal session price; commission is taken from either |
| Late cancel on credits (credit used, instructor gets the late-cancel share) | Or credit used and nothing paid, or credit returned |
| Bookings stand after expiry (on) | Off means a credit can't book a session dated after its batch expires |
| Pack refunds (on), reference session price, refund fee | Admin partial refund of unused credits |

Credit rules sit on the **Packs** tab and apply to new purchases only: each purchase saves its own copy.

- **Snapshots.** The commission, convenience fee, cancel window and late fee are locked when checkout starts (`POST /api/bookings/orders`) and copied to the booking at payment. Cancel-quote, refunds and payouts always use the booking's copy, so a settings change mid-checkout or later only affects new checkouts.
- **Convenience fee.** The customer pays session price plus the fee. It is refunded on decline, admin cancel and free cancel, and kept on a late cancel. It is never paid out to the provider.
- **Saving.** `PATCH /api/admin/settings` needs the `version` you edited. If another admin saved first you get `409` and reload. Every changed field is written to `SettingsAudits` (who, when, old, new).
- **Cache.** Each API instance caches the settings for 60 seconds. A save clears the cache on the instance that handled it, so with several instances others can serve the old values for up to a minute.
- **Payout cycles.** Weeks run Monday to Sunday, India time. Biweekly cycles are 14-day blocks starting Monday 5 Jan 2026. Export only claims payouts created before the current cycle started; otherwise it returns `409` with the date the next export opens.

## Background jobs

`BackgroundJobs` is a small DB-backed queue that later features (payouts, credit expiry, reminders) reuse.

- Enqueue with `BackgroundJobQueue.Enqueue(type, payload, runAt)`; it is saved with the caller's own `SaveChanges`.
- Handle a type by registering `IBackgroundJobHandler` (`services.AddScoped<IBackgroundJobHandler, MyHandler>()`).
- A hosted service polls every `Jobs:PollSeconds`, claims due jobs with a conditional update (so two instances never run the same job), and runs each in its own scope.
- A failure retries after 1, 2, 4… minutes (capped at 1 hour) until `MaxAttempts`, then the job is `Failed`. An unknown type fails at once. A job whose worker died is released when its lock expires.
- `credits.expire` (`CreditExpiryJob`) runs at midnight India time. Startup queues the first run and each run queues the next. It writes one `Expire` ledger row per batch past its expiry with credits left; running it twice changes nothing (the database allows one `Expire` row per batch). Bookings already made stay.

## HTTP API

Errors use `{ "error": "..." }` with the matching status code. Missing token is `401`; wrong role is `403`. Unhandled errors return `500` with a generic `error` and a `traceId` that matches the log line; exception details never reach the client.

Browse endpoints (areas, categories, providers, slots, reviews, photos, policy pages) are public and rate limited per IP (`RateLimiting:BrowsePerMinute`).

Sessions: the access JWT is short-lived. On a `401` the client calls `/api/auth/refresh` once (shared by parallel requests), retries the original request, and signs out with a toast if the refresh fails. Blocking a user revokes all their refresh tokens, so their next request gets `401` and they are signed out.

### Public and auth

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/otp/request` | | Start OTP (`isNewUser` with name and gender for sign-up). Blocked accounts get `403`. |
| POST | `/api/auth/otp/resend` | | New code; the previous one stops working |
| POST | `/api/auth/otp/verify` | | Returns the JWT and sets the httpOnly `ym_refresh` cookie |
| POST | `/api/auth/refresh` | Cookie | Rotates the refresh cookie and returns a new JWT. `401` when missing, expired, revoked or reused (reuse revokes the whole sign-in); `403` when blocked; `409` when another tab just rotated it. |
| POST | `/api/auth/logout` | Cookie | Revokes the sign-in and clears the cookie (`204`) |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/areas` | | Active cities and neighbourhoods |
| GET | `/api/categories` | | Active categories (`yoga`) |
| GET | `/api/policy` | | Commission %, convenience fee, cancel/reschedule windows and late-cancel fee for new bookings |
| GET | `/api/site/banner` | | Home banner `{ title, subtitle, offer }`; `null` fields mean "use the default copy" |
| GET | `/api/providers?city=&area=&mode=&category=` | | Listed providers: verified, not blocked, in an active area |
| GET | `/api/providers/{id}` | | Public profile (no Meet link): photos, intro video id, approved certificate titles, `isVerified`, trial offer |
| GET | `/api/providers/{id}/slots?mode=` | | Open slots for that mode |
| GET | `/api/providers/{id}/reviews` | | Public reviews |
| GET | `/api/media/photos/{id}` | | Profile photo of a listed provider |
| GET | `/api/pages/{slug}` | | Policy page: `refund-policy`, `terms`, `privacy` |
| GET | `/api/packs` | | Active session packs |

### Provider

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/providers/register` | Creates a **Pending** provider and returns a new token |
| GET / PATCH | `/api/providers/me` | Own profile, including the Meet link |
| PATCH | `/api/providers/me/rates` | Per-mode rates |
| GET | `/api/providers/me/media` | Own photos, intro video and certificates with their review status |
| POST / DELETE | `/api/providers/me/photos`, `/photos/{id}` | Add (multipart `file`) or remove a photo |
| PUT | `/api/providers/me/intro-video` | `{ "url" }` YouTube link, or empty to remove |
| POST / DELETE | `/api/providers/me/certificates`, `/certificates/{id}` | Upload (multipart `title`, `file`) or remove a certificate |
| GET | `/api/providers/me/certificates/{id}/file` | Download own certificate (never cached) |
| GET | `/api/providers/me/services` | Own services (for the trial picker) |
| GET / PUT | `/api/providers/me/trial` | Trial offer: `{ "serviceId", "enabled", "price" }` |
| GET | `/api/providers/me/payouts?status=` | Own payouts |
| GET / POST | `/api/providers/me/slots?mode=&from=&to=` | List or add slots for an offered mode |
| PUT / DELETE | `/api/providers/me/slots/{id}` | Edit or remove a future slot without a booking |
| POST | `/api/providers/me/slots/{id}/block`, `/unblock` | Close or reopen a future slot without a booking |
| GET | `/api/bookings/instructor?status=` | Own bookings |
| POST | `/api/bookings/{id}/accept`, `/decline`, `/complete`, `/noshow` | Handshake. Decline refunds; complete writes a pending payout. |

### Customer

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/bookings/quote?slotId=&trial=` | Server price for a slot: session amount, convenience fee and total, plus a `credit` option (available or why not). The book page shows this, never its own sum. |
| POST | `/api/bookings/credit` | Book with 1 credit when no fee is due. `409` if the last credit was just used; the client refetches the quote and switches to pay. With the fee on, use `/orders` with `useCredit: true`. |
| POST | `/api/packs/{id}/orders` | Razorpay order for a pack. Blocked customers get `403`. |
| POST | `/api/packs/confirm`, `/api/packs/local-confirm` | Verify the payment and add the credits (local-confirm is fake gateway only). Idempotent per payment. |
| GET | `/api/packs/orders/{orderId}` | Purchase status; the Packs page polls it while confirming |
| GET | `/api/credits/me` | Own credit batches, earliest expiry first |
| GET | `/api/bookings/trial?providerId=` | Whether this customer can still book the provider's trial |
| POST | `/api/bookings/orders` | Razorpay order for a slot (`trial: true` for the trial price). Home needs `homeAddress` and `landmark`. The server prices it: `amount` = `sessionAmount` + `convenienceFee`. A provider in an inactive area is refused. |
| POST | `/api/bookings/confirm` | Verify the checkout signature and create the booking (`PendingAccept`). Idempotent per payment. |
| POST | `/api/bookings/local-confirm` | Fake gateway only; `404` otherwise |
| GET / PATCH | `/api/profile` | Own account details |
| PUT | `/api/profile/visit-address` | Default home-visit address |
| GET | `/api/bookings/me` | Own bookings |
| GET | `/api/bookings/{id}/cancel-quote` | Refund preview from the booking's own terms: `amount` paid, `lateCancelFee`, `convenienceFeeKept`, `refund` (never negative). For a credit booking, `creditReturned` and, when false, `creditNotReturnedReason` (`LateCancel`, `PackRefunded` or `PackExpired`) |
| POST | `/api/bookings/{id}/cancel` | Cancel with full or partial refund per the booking's terms |
| POST | `/api/bookings/{id}/reschedule` | `{ "slotId" }`, same provider and mode |
| POST | `/api/bookings/{id}/reviews` | One review after `Completed` |
| POST | `/api/webhooks/razorpay` | `payment.captured` (signed with `X-Razorpay-Signature`) |

### Admin (`/api/admin`, Admin JWT)

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/providers?status=`, `/providers/{id}` | Approval queue and review record |
| POST | `/providers/{id}/verify`, `/reject` | Verify, or reject with an optional reason (max 300) |
| GET | `/users?q=&role=`, `/users/{id}` | Directory and detail, including block status and history |
| POST | `/users/{id}/block` | `{ "reason" }` required (5–300). Signs the user out, blocks OTP sign-in, and hides a provider from browse and checkout. Existing bookings stay. Admins cannot be blocked. |
| POST | `/users/{id}/unblock` | Optional `{ "reason" }` |
| GET | `/bookings?status=&providerId=&from=&to=`, `/bookings/{id}` | Oversight |
| POST | `/bookings/{id}/cancel` | `{ "reason" }`; full refund on the customer's behalf |
| GET | `/payments?status=`, `/payouts?status=`, `/payouts/csv?status=` | Transactions. Payments include pack sales (`kind: "Pack"`, with `packName` and no booking); ₹0 rows for credit bookings are left out. |
| POST | `/payouts/export`, `/payouts/{id}/paid` | Claim pending payouts from closed payout cycles into a CSV batch; mark one paid |
| GET | `/reports/summary` | Booking counts, paid GMV (bookings paid with money, net of refunds), `packSales` (net of pack refunds), `revenue` (both), `paidBookings`, `creditBookings`, pending payouts |
| GET / POST / PATCH | `/areas`, `/areas/{id}` | `{ "name", "city" }`. The city must already exist (opening cities is not available yet). Duplicate name in a city is `409`. `isActive: false` hides the area and its providers from browse; existing bookings are untouched. |
| GET / PATCH | `/categories`, `/categories/{id}` | Rename the label; the slug never changes |
| GET / PATCH | `/settings` | Platform settings (see above). PATCH sends `version` plus only the fields to change; a stale version is `409`. |
| GET | `/settings/audit` | Last 50 settings changes, newest first |
| GET | `/certificates?status=` | Certificate queue (`Pending` by default, `Approved`, `Rejected` or `all`) |
| GET | `/certificates/{id}/file` | View the private file |
| POST | `/certificates/{id}/approve`, `/reject` | Approve, or reject with `{ "reason" }` |
| GET / POST / PATCH | `/packs`, `/packs/{id}` | Session packs. PATCH sends `version`; `isActive: false` stops new sales. Packs are never deleted. |
| GET | `/users/{id}/credits` | A customer's batches with the saved rules and `refundPreview` |
| POST | `/credit-batches/{id}/refund` | Partial refund of unused credits through Razorpay on the pack payment |
| GET | `/pages` | All policy pages |
| PUT | `/pages/{slug}` | `{ "title", "body", "version" }`; a stale version is `409` |

Lists return at most 100 rows, newest first.

## Provider trust signals

- **Photos**: up to 6, JPEG, PNG or WebP, 5 MB each. Public at `/api/media/photos/{id}` while the provider is listed.
- **Intro video**: a YouTube link. Only the 11-character video id is stored, and the SPA embeds it from `youtube-nocookie.com` in a sandboxed iframe. No other hosts.
- **Certificates**: up to 5, PDF, JPEG or PNG, 10 MB each. The file is **always private**: only the owner and admins can download it, with `Cache-Control: private, no-store`. After approval the public sees the title only.
- **Verified badge**: shown once at least one certificate is approved.
- File type is checked from the file's first bytes, not its name. Files live on local disk under `Storage:RootPath` (default `App_Data/uploads`, outside `wwwroot`), named by id, and are only ever served through the API.

## Trial class

A provider can offer one trial on their main service, free or at most their lowest regular rate. Each customer gets one trial per provider. A customer cancel or no-show uses it up; an instructor decline or admin cancel gives it back. Razorpay always takes at least ₹1, so a free trial still charges the convenience fee, and when that fee is ₹0 the trial price must be at least ₹1. If two trial payments race, the second is refunded in full.

`Bookings.TrialConsumed` stores that rule. `BookingRules` updates it with every status change, so it is saved together with the status. A unique filtered index (`IX_Bookings_OneConsumedTrial` on `CustomerId, ProviderId WHERE TrialConsumed = 1`) means the database itself rejects a second active trial, even when two payments are captured at the same moment. Capture turns that rejection into the same full refund.

## Credit packs

A pack is N credits bought upfront. 1 credit books 1 session with any instructor in any mode, up to the pack's price cap; above the cap the customer pays normally. Trials can't use credits, and blocked customers can't buy or use them.

- **Ledger.** Each purchase is a `CreditBatch` holding the pack details and the credit rules in force when it was bought. `CreditLedgerEntry` rows (purchase, use, return, expire, refund) move credits; a booking uses the batch that expires first.
- **Frozen values.** A credit booking stores the credit value (pack price ÷ sessions) and the instructor's gross on the booking, and payouts use those values.
- **Cancel and reschedule.** A free cancel, instructor decline or admin cancel returns the credit once. A late cancel follows the saved rule. Reschedule uses no extra credit. A credit returned to an expired batch can't be used.
- **Refund.** Admin only: unused credits × reference session price − refund fee, capped at what's left of the amount paid and never negative. Upcoming bookings from the pack stay booked and their credits count as used.

## Policy pages

Refund policy, Terms and Privacy are rows in `ContentPages`, seeded with starter text and edited under **Settings → Pages**. The body is plain text with three rules: a blank line starts a new paragraph, `## ` starts a heading, `- ` starts a bullet. The SPA renders it as text (no HTML), and saving needs the `version` you edited.

## Deploy guide

The API and the SPA deploy separately, or as one site where the API serves the SPA (see [Deploy to Plesk (single site)](#deploy-to-plesk-single-site)). Any host works: a VPS with Nginx, IIS/Plesk, Azure App Service, AWS, or containers.

### What Production enforces

With `ASPNETCORE_ENVIRONMENT=Production` the API **refuses to start** when:

- `Database:AutoMigrate` is true (apply migrations with the script below instead),
- `Otp:ExposeCode` or `Otp:UseFixedCode` is true,
- `Otp:DemoPhones` or `Otp:DemoCode` is set (demo sign-in is for Staging and Development),
- `Razorpay:UseFakeGateway` is true, or any of `Razorpay:KeyId`, `Razorpay:KeySecret`, `Razorpay:WebhookSecret` is empty,
- `Cors:AllowedOrigins` is empty, or `Jwt:Key` is shorter than 32 bytes,
- `DataProtection:KeysPath` is empty or not a full path.

It logs a warning (and still starts) when the key id is a `rzp_test_` key. It also turns off Swagger, returns `404` for `/api/bookings/local-confirm` and `/api/admin/demo-data/load`, and writes one-line JSON logs to the console with UTC timestamps and the request trace id.

**Staging** (`ASPNETCORE_ENVIRONMENT=Staging`, `appsettings.Staging.json`) enforces everything above, with three differences: `Razorpay:KeyId` **must** be a `rzp_test_` key, OTP demo sign-in is allowed (a malformed demo setup still stops startup), and the admin demo-data loader works.

After each deploy, run the smoke test (sign in with the demo phone, find an open slot, create a Razorpay Test order). Against the in-memory test host it also pays with the fake gateway, cancels and checks the refund; that part runs in every CI build.

```bash
SMOKE_BASE_URL=https://yogademo.psoftcs.com SMOKE_PHONE=<demo phone> SMOKE_OTP_CODE=<demo code> dotnet test tests/YogaMarketplace.Smoke
```

Or run **Actions → smoke → Run workflow** (repo secrets `SMOKE_PHONE`, `SMOKE_OTP_CODE`). Use a customer demo phone, not an admin or instructor. Paying needs Razorpay's browser checkout, so check that by hand once per release: book with a [Razorpay test card](https://razorpay.com/docs/payments/payments/test-card-details/), cancel, and see the refund in the Razorpay Test dashboard.

If a release goes wrong, follow [docs/ops/rollback.md](docs/ops/rollback.md).

### API environment variables

Use the host's secret store or environment variables. Never put live values in `appsettings*.json`.

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | SQL Server connection string |
| `Jwt__Key` | Random string, 32+ bytes |
| `Otp__Pepper` | Random string |
| `Cors__AllowedOrigins__0` | SPA origin, e.g. `https://app.example.com` (required in Production even when same-origin) |
| `Razorpay__KeyId`, `Razorpay__KeySecret` | Razorpay API keys (test keys for the QA run, then live keys) |
| `Razorpay__WebhookSecret` | The secret set on the Razorpay webhook |
| `Storage__RootPath` | Folder for uploaded photos and certificates. Use an absolute path on a persistent disk or volume that the app can write to. |
| `DataProtection__KeysPath` | Folder for the data protection key ring. Absolute path outside the site folder, writable by the app. |
| `ForwardedHeaders__KnownProxies__0` | IP of your reverse proxy, so rate limits see the real client IP |
| `RefreshToken__CookieSameSite` | Leave `Strict` for same-site; `None` only for a cross-site API (HTTPS required) |

`Database:AutoMigrate`, `Seed:DemoData`, `Otp:ExposeCode`, `Otp:UseFixedCode` and `Razorpay:UseFakeGateway` are already `false` in `appsettings.Production.json`.

### Host setup: uploads and proxies

**Persistent storage for uploads.** Photos and certificates are written to `Storage__RootPath`. The default (`App_Data/uploads` next to the app) is wiped by most redeploys and is lost when a container is replaced, so point it at storage that outlives the app:

| Host | Set `Storage__RootPath` to |
| --- | --- |
| VPS / IIS | A folder outside the publish folder, e.g. `/var/lib/yoga-marketplace/uploads` or `D:\YogaMarketplace\uploads`. Give the app's user (or IIS app pool identity) read and write access only. |
| Docker | A mounted volume, e.g. `-v yoga-uploads:/data/uploads` with `Storage__RootPath=/data/uploads` |
| Azure App Service (Linux) | A folder under `/home`, which persists and is shared across instances, e.g. `/home/data/uploads` |
| Several instances | One shared disk (NFS, Azure Files) mounted at the same path on every instance |

Never put it under the web root or a static file share; certificates must only be reachable through the API. Include it in backups (below).

**Trusted proxies, so rate limits are per visitor.** Behind a reverse proxy, every request arrives from the proxy's IP. Until that proxy is trusted, all visitors share one rate-limit bucket and one busy visitor can lock everyone out. List the IP of the hop directly in front of the API:

| Setup | Setting |
| --- | --- |
| Nginx or IIS ARR on the same machine | `ForwardedHeaders__KnownProxies__0=127.0.0.1` (add `__1=::1` if it connects over IPv6) |
| Nginx in another container or VM | That proxy's private IP, e.g. `ForwardedHeaders__KnownProxies__0=10.0.0.4` |
| A load balancer with changing IPs (Azure, AWS) | Its subnet, e.g. `ForwardedHeaders__KnownNetworks__0=10.0.0.0/24` |
| No proxy (the API faces the internet directly) | Leave both empty |

The proxy must set `X-Forwarded-For` and `X-Forwarded-Proto`, as the Nginx example above does. Only one hop is read. If a CDN sits in front of your proxy, have the proxy pass the CDN's real-client header (for example Cloudflare's `CF-Connecting-IP`) as `X-Forwarded-For`. Never trust a public range. To check: from two different networks, make enough browse requests to hit the limit on one; the other should still get `200`.

### Build and run the API

```bash
dotnet publish src/YogaMarketplace.Api -c Release -o ./publish
```

This also builds the SPA into `./publish/wwwroot` (Node 22 required). When the SPA is hosted elsewhere, add `-p:SkipSpaBuild=true`.

Run `./publish/YogaMarketplace.Api.dll` behind HTTPS (Nginx, IIS with the .NET 8 Hosting Bundle and an app pool set to **No Managed Code**, or a platform service). Health checks:

- `GET /health`: the process is up (use for liveness)
- `GET /health/ready`: the database answers (use for readiness or the load balancer)

### Database migrations

Production never migrates on startup. Every CI run publishes an idempotent script as the `migrations-sql` artifact; it only applies migrations the database is missing, so it is safe to re-run. To build it locally:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Infrastructure -o artifacts/migrations.sql
```

Release order: back up the database, run the script, then deploy the new API build. With `sqlcmd`, pass `-I` (some indexes need `QUOTED_IDENTIFIER ON`) and `-b` to stop on the first error:

```bash
sqlcmd -S <server> -d <db> -I -b -i migrations.sql
```

### Backups

- **Database**: turn on automated full backups with point-in-time restore (Azure SQL and RDS do this by default; on your own SQL Server, schedule nightly full plus frequent log backups). Keep at least 7 days, store copies off the server, and test a restore before launch.
- **Uploads**: back up `Storage__RootPath` on the same schedule. Rows in the database point at these files, so restore both from the same point in time.

### Build the SPA

`VITE_API_BASE_URL` is read when the bundle is **built**, not at runtime, so set it in the build environment (CI variable or `.env.production`) and rebuild to change it.

```bash
cd src/YogaMarketplace.Client
npm ci
VITE_API_BASE_URL=https://api.example.com npm run build   # or leave empty for same-origin
```

Upload `dist/` to any static host or CDN. Every unknown path must fall back to `index.html` so deep links like `/teachers/123` work on refresh: `try_files $uri /index.html;` in Nginx (see above), a rewrite rule on IIS, or the "single-page app" / 404-to-`index.html` option on static hosts. The SPA shows its own 404 page for routes it does not know.

### Go-live checklist

1. Deploy with Razorpay **test** keys and set the webhook in the Razorpay dashboard to `https://<api>/api/webhooks/razorpay` (event `payment.captured`) with the same secret as `Razorpay__WebhookSecret`.
2. QA does one full run: sign in, browse signed out, book, pay with a Razorpay test card, instructor accepts, cancel (free and late), admin refund.
3. Switch to live keys and the live webhook secret, restart, and check the log has no test-key warning.
4. Wire a real SMS/WhatsApp OTP sender before public sign-in.
5. Review the policy pages under **Settings → Pages**.

## Deploy to Plesk (single site)

One IIS site on `https://yogademo.psoftcs.com` runs the API and serves the SPA. yogademo is the **Staging** site: Razorpay Test keys, OTP demo sign-in and the demo-data loader. A live site uses the same steps with `Production`, live keys and no demo variables. The server needs the **.NET 8 Hosting Bundle** (ask the host if the Plesk "ASP.NET Core" component is not listed).

### 1. Build

```bash
dotnet publish src/YogaMarketplace.Api -c Release -o publish
```

This needs the .NET 8 SDK and Node 22. It builds the SPA with `VITE_API_BASE_URL` empty (the app calls `/api` on its own origin) and puts it in `publish/wwwroot`. Every CI run also uploads the same folder as the `site-publish` artifact, and the database script as `migrations-sql`. `publish/web.config` runs the app in-process with `AspNetCoreModuleV2` and the stdout log off. Its comments show how to turn the log on for a startup failure.

### 2. Subdomain and SSL

1. **Websites & Domains → Add Subdomain**: `yogademo` on `psoftcs.com`. Note the document root (for example `yogademo.psoftcs.com`).
2. **SSL/TLS Certificates**: issue a Let's Encrypt certificate for `yogademo.psoftcs.com`.
3. **Hosting Settings**: turn on **Permanent SEO-safe 301 redirect from HTTP to HTTPS**. The app sends HSTS in Production, so HTTPS must work first.

### 3. Folders outside the site

Create two folders **outside** the document root, so deploys never overwrite them. For example, under the subscription's `private` folder:

- `...\private\yogademo\keys` → `DataProtection__KeysPath`
- `...\private\yogademo\uploads` → `Storage__RootPath`

In File Manager, give the site's application pool user **Modify** rights on both. Use the full Windows path (for example `C:\Inetpub\vhosts\psoftcs.com\private\yogademo\keys`); Plesk shows the real root under Hosting Settings. Never put either folder under `wwwroot`. Certificates must only be reachable through the API.

### 4. Application pool

Under **Dedicated IIS Application Pool** (or ask the host):

| Setting | Value |
| --- | --- |
| .NET CLR version | No Managed Code |
| Load user profile | **On** |
| Idle timeout | `0` (never sleep). If the plan does not allow it, have an uptime monitor call `https://yogademo.psoftcs.com/health` every 5 minutes. Background jobs, such as credit expiry, only run while the app is awake. |

### 5. Database

Create an MS SQL database and user in Plesk. Production never migrates on startup, so run the `migrations-sql` script yourself, from SSMS or `sqlcmd -I -b -i migrations.sql`. The script is idempotent, so you can run it on every release.

### 6. Environment variables

Set these in Plesk, if your plan shows environment variables for the site. Otherwise put them **only in the server's copy of `web.config`**, inside `<environmentVariables>`. They must never be in the repo or in the `web.config` you upload from your machine. **Never re-upload `web.config` after the first deploy**: it would wipe the variables and the app would not start. In `web.config` values, escape XML: `&` becomes `&amp;`, `<` becomes `&lt;`, and `"` becomes `&quot;`.

| Variable | Required | Value |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Staging` on yogademo (edit the server copy of `web.config`; the uploaded one says `Production`) |
| `ConnectionStrings__Default` | Yes | `Server=<host>;Database=<db>;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=true` |
| `Jwt__Key` | Yes | Random string, 32+ bytes |
| `Otp__Pepper` | Yes | Random string, 8+ characters. Changing it invalidates codes already sent. |
| `Cors__AllowedOrigins__0` | Yes | `https://yogademo.psoftcs.com` |
| `Razorpay__KeyId` | Yes | `rzp_test_…` (Staging refuses any other key) |
| `Razorpay__KeySecret` | Yes | Matching key secret |
| `Razorpay__WebhookSecret` | Yes | The secret set on the Razorpay webhook (below) |
| `DataProtection__KeysPath` | Yes | Full path of the `keys` folder from step 3. The app refuses to start without it. |
| `Storage__RootPath` | Yes, for this deploy | Full path of the `uploads` folder from step 3. The default (`App_Data/uploads`) lives in the site folder and is lost on redeploy. |
| `Otp__DemoPhones` | No (Staging only; Production refuses it) | Comma-separated phones that sign in with the demo code, e.g. `+919000000001,+919000000002`. Every other phone gets a normal OTP. Empty or missing turns demo mode off. |
| `Otp__DemoCode` | No (Staging only; Production refuses it) | The 6-digit code for `Otp__DemoPhones`. Startup fails if it is set without `Otp__DemoPhones`. Attempt lockout and rate limits still apply, and the log shows a warning (with masked phones) while demo mode is on. |

Leave `ForwardedHeaders__*` empty (IIS in-process has no proxy in front of the app) and `RefreshToken__CookieSameSite` at `Strict` (one site). The app refuses to start if a required value is missing or unsafe; see [What Production enforces](#what-production-enforces).

### 7. Upload (FTP)

**First deploy:** upload everything in `publish/` to the document root, then set the variables above in Plesk or in the server's `web.config`.

**Every later deploy** (keep each `site-publish` zip; see [docs/ops/rollback.md](docs/ops/rollback.md)):

1. Upload `deploy/plesk/app_offline.htm` to the document root. IIS stops the app, unlocks its files and shows the maintenance page.
2. Back up the database, then run the `migrations-sql` script if this release has new migrations.
3. Upload the new `publish/` files, overwriting old ones, **except `web.config`**. Never re-upload it after the first deploy, because the server copy holds your variables. If a release really changes `web.config`, edit the server copy by hand. Never delete or overwrite the `keys` or `uploads` folders.
4. Delete `app_offline.htm`. The app starts on the next request. Check that `https://yogademo.psoftcs.com/health/ready` returns `200`.
5. Run the smoke test (**Actions → smoke**). If it fails, roll back.

Old files in `wwwroot/assets` are harmless (every build uses new file names), and they keep already-open tabs working. Clear them now and then.

### 8. Razorpay webhook

In the Razorpay dashboard, add a webhook:

- **URL**: `https://yogademo.psoftcs.com/api/webhooks/razorpay`
- **Event**: `payment.captured`
- **Secret**: the same value as `Razorpay__WebhookSecret`

Use the test-mode dashboard with test keys, and add the webhook again in live mode when you switch to live keys. Then follow the [Go-live checklist](#go-live-checklist).

## Out of scope

Native apps, in-app video (Online uses a Google Meet link), cart, wishlist, merch.
