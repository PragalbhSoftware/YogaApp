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

See `.env.example`. Vite loads `.env.development`, `.env.staging` (`vite build --mode staging`), and `.env.production`.

### API

Set in `appsettings.*.json`, user secrets, or environment variables (`Section__Key`).

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Default` | SQL Server |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | Token signing. The app refuses to start without `Jwt:Key`. |
| `Otp:Pepper`, `Otp:ExposeCode`, `Otp:UseFixedCode` | OTP hashing and the dev shortcuts (forced off in Production) |
| `Cors:AllowedOrigins` | Array of browser origins allowed to call the API cross-origin. Development allows `http://localhost:5173`. **Production refuses to start with an empty list.** |
| `Razorpay:KeyId`, `Razorpay:KeySecret`, `Razorpay:WebhookSecret` | Payment gateway. Never commit live keys. |
| `Razorpay:UseFakeGateway` | Development and tests issue `order_fake_…` ids and capture locally (`/api/bookings/local-confirm`). Production refuses to start when this is on. |
| `Database:AutoMigrate`, `Seed:DemoData` | Off in Production |

Environment variable form for an array: `Cors__AllowedOrigins__0=https://app.example.com`.

## Production API routing for the SPA

The build output is static (`npm run build` → `dist/`). Production has no Vite proxy, so choose one of:

**Option A: same origin (recommended).** Serve `dist/` and reverse-proxy `/api` to the API from the same host. Build with `VITE_API_BASE_URL` empty. No CORS needed. Example Nginx:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;

  location /api/ {
    proxy_pass http://api:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
  }

  location / {
    try_files $uri /index.html;
  }
}
```

`try_files … /index.html` keeps deep links such as `/teachers/123` working on refresh.

**Option B: separate API origin.** Build with `VITE_API_BASE_URL=https://api.example.com` and add the SPA origin to the API: `Cors__AllowedOrigins__0=https://app.example.com`. The SPA host still needs the `index.html` fallback for client routes.

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
dotnet ef migrations script --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api --idempotent -o deploy.sql
```

New columns must be nullable or defaulted, and every migration needs a working `Down()`.

## Money and concurrency rules

- Amounts are rounded to 2 decimals; Razorpay gets integer paise.
- State is saved to the database **before** calling Razorpay (refunds run inside a transaction: save, call the gateway, commit).
- `Booking.Status`, `Payment.Status`, and `User.IsBlocked` are EF concurrency tokens. A lost race returns `409`.
- Times are India time (`Asia/Kolkata`); cancellation windows are measured from the session start in that zone.

## HTTP API

Errors use `{ "error": "..." }` with the matching status code. Missing token is `401`; wrong role is `403`.

### Public and auth

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/otp/request` | | Start OTP (`isNewUser` with name and gender for sign-up). Blocked accounts get `403`. |
| POST | `/api/auth/otp/resend` | | New code; the previous one stops working |
| POST | `/api/auth/otp/verify` | | Returns the JWT |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/areas` | | Active cities and neighbourhoods |
| GET | `/api/categories` | | Active categories (`yoga`) |
| GET | `/api/policy` | | Fee % and cancel/reschedule windows |
| GET | `/api/providers?city=&area=&mode=&category=` | | Listed providers: verified and not blocked |
| GET | `/api/providers/{id}` | | Public profile (no Meet link) |
| GET | `/api/providers/{id}/slots?mode=` | | Open slots for that mode |
| GET | `/api/providers/{id}/reviews` | | Public reviews |

### Provider

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/providers/register` | Creates a **Pending** provider and returns a new token |
| GET / PATCH | `/api/providers/me` | Own profile, including the Meet link |
| PATCH | `/api/providers/me/rates` | Per-mode rates |
| GET | `/api/providers/me/payouts?status=` | Own payouts |
| GET / POST | `/api/providers/me/slots?mode=&from=&to=` | List or add slots for an offered mode |
| PUT / DELETE | `/api/providers/me/slots/{id}` | Edit or remove a future slot without a booking |
| POST | `/api/providers/me/slots/{id}/block`, `/unblock` | Close or reopen a future slot without a booking |
| GET | `/api/bookings/instructor?status=` | Own bookings |
| POST | `/api/bookings/{id}/accept`, `/decline`, `/complete`, `/noshow` | Handshake. Decline refunds; complete writes a pending payout. |

### Customer

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/bookings/orders` | Razorpay order for a slot. Home needs `homeAddress` and `landmark`. The server prices it. |
| POST | `/api/bookings/confirm` | Verify the checkout signature and create the booking (`PendingAccept`). Idempotent per payment. |
| POST | `/api/bookings/local-confirm` | Fake gateway only |
| GET / PATCH | `/api/profile` | Own account details |
| PUT | `/api/profile/visit-address` | Default home-visit address |
| GET | `/api/bookings/me` | Own bookings |
| GET | `/api/bookings/{id}/cancel-quote` | Refund preview under the cancellation policy |
| POST | `/api/bookings/{id}/cancel` | Cancel with full or partial refund per policy |
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
| GET | `/payments?status=`, `/payouts?status=`, `/payouts/csv?status=` | Transactions |
| POST | `/payouts/export`, `/payouts/{id}/paid` | Claim pending payouts into a CSV batch; mark one paid |
| GET | `/reports/summary` | Booking counts, paid GMV, pending payouts |
| GET / POST / PATCH | `/areas`, `/areas/{id}` | `{ "name", "city" }`; city is required (2–40 letters). Duplicate name in a city is `409`. |
| GET / PATCH | `/categories`, `/categories/{id}` | Rename the label; the slug never changes |
| GET / PATCH | `/policy` | Fee and window settings. Existing payouts keep their stored fee. |

Lists return at most 100 rows, newest first.

## Deploying the API (IIS / Plesk example)

1. Install the .NET 8 Hosting Bundle; create the SQL Server database and login.
2. `dotnet publish src/YogaMarketplace.Api -c Release -o ./publish`, upload, set the app pool to **No Managed Code**.
3. Set at least: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Default`, `Jwt__Key` (32+ bytes), `Otp__Pepper`, `Cors__AllowedOrigins__0` (when using option B), `Razorpay__KeyId`, `Razorpay__KeySecret`, `Razorpay__WebhookSecret`, `Razorpay__UseFakeGateway=false`, `Database__AutoMigrate=false`, `Seed__DemoData=false`.
4. Apply the schema with `dotnet ef database update --connection "<prod>"` or the idempotent `deploy.sql`.
5. Wire a real SMS/WhatsApp OTP sender before public sign-in.

Deploy the SPA `dist/` with either routing option above.

## Out of scope

Native apps, in-app video (Online uses a Google Meet link), cart, wishlist, merch.
