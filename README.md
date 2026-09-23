# Yoga Marketplace

Mumbai-first yoga instructor marketplace. This repository is **slice 1**: ASP.NET Core Web API, domain model, EF Core, and SQL Server. Customers can authenticate with a one-time passcode, instructors can register (they stay pending until a later admin slice), and verified instructors can be browsed with **mode-specific** slots (Home, Studio, Online).

Yoga is the first `Category`. The model is generic enough for another category later. There is no customer web UI in this slice.

## Solution

| Project | Role |
| --- | --- |
| `src/YogaMarketplace.Api` | Controllers, OTP/JWT, browse and slots |
| `src/YogaMarketplace.Domain` | Entities and booking rules |
| `src/YogaMarketplace.Infrastructure` | EF Core, SQL Server, seed |
| `tests/YogaMarketplace.Api.Tests` | Domain rules and API tests (SQLite) |

Flow is controllers to services to EF Core. No CQRS and no message bus.

## Run locally

Requires the .NET 8 SDK.

### SQL Server in Docker

```bash
docker compose up -d
```

The compose file publishes `localhost:1433` with SA password `YogaDev!Passw0rd`. That matches `appsettings.Development.json`.

### SQL Server LocalDB (Windows)

Put this in `src/YogaMarketplace.Api/appsettings.Development.json` (or user secrets):

```text
Server=(localdb)\mssqllocaldb;Database=YogaMarketplace;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

### Migrate and start

Development applies migrations and seeds on startup (`Database:AutoMigrate` and `Seed:DemoData`).

```bash
dotnet tool restore
dotnet run --project src/YogaMarketplace.Api
```

API: `http://localhost:5080`  
Swagger: `http://localhost:5080/swagger`  
Liveness: `http://localhost:5080/health`  
Database: `http://localhost:5080/health/ready`

To apply migrations without starting the API:

```bash
dotnet tool restore
dotnet ef database update --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api
```

Idempotent script for a host you cannot reach with the EF tool:

```bash
dotnet ef migrations script --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api --idempotent -o deploy.sql
```

### Tests

```bash
dotnet test
```

Tests build the model with SQLite. The app itself uses SQL Server.

## Dev OTP and seed

SMS is a log stub (`LoggingOtpSender`). In Development the code is fixed at `123456` and the request response includes `devCode`.

| Who | Phone | Notes |
| --- | --- | --- |
| Ananya Desai | `+919876543210` | Verified, Bandra, Home ₹899 / Studio ₹749 / Online ₹599, Google Meet link on her own profile |
| Marketplace admin | `+919000000001` | Seeded user only. Admin APIs are a later slice |

New customer: `name` + `gender` + `phone`, then OTP. Existing customer: `phone`, then OTP. Slots are Mumbai local time (`Asia/Kolkata`), separate per Home / Studio / Online. Bookings are not seeded.

`Seed:DemoData` is off in Production. `appsettings.Development.json` is excluded from `dotnet publish`.

## HTTP API (slice 1)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health`, `/health/ready` | | Process up, SQL reachable |
| POST | `/api/auth/otp/request` | | Start OTP |
| POST | `/api/auth/otp/resend` | | New code, previous code stops working |
| POST | `/api/auth/otp/verify` | | JWT |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/areas` | | Mumbai neighbourhoods |
| GET | `/api/categories` | | Includes `yoga` |
| GET | `/api/policy` | | Fee % and cancel/reschedule hours (TBD defaults) |
| GET | `/api/providers?area=&mode=&category=` | | Verified instructors only |
| GET | `/api/providers/{id}` | | Public profile. Meet link is omitted |
| GET | `/api/providers/{id}/slots?mode=Home` | | Open slots for that mode |
| POST | `/api/providers/register` | Bearer | Creates a **Pending** instructor |
| GET | `/api/providers/me` | Bearer | Own profile, including Meet link |
| POST | `/api/providers/me/slots` | Bearer | Add slots for a mode the instructor offers |

Pay-at-book, accept/decline, reviews, and payouts are domain rules in `BookingRules` (covered by tests) and are not HTTP endpoints yet.

Booking states: `PendingAccept` → `Upcoming` or `Declined` → `Completed`, `NoShow`, or `Cancelled`. A captured payment creates `PendingAccept`. Decline is the refund path and frees the slot. Complete unlocks one review and a pending payout (`gross − fee%`).

Cancel / reschedule free-window hours and the platform fee are stored on `MarketplacePolicy` (12 hours, 15% fee, 50% late-cancel fee). The note says they are TBD. The window is not enforced until ops confirms it.

## Deploy to PeoplesHost Plesk (`YogaDemo.psoftcs.com`)

Target is IIS on Plesk with SQL Server, subdomain `YogaDemo.psoftcs.com`.

1. Install the **.NET 8 Hosting Bundle** and the Plesk ASP.NET Core extension on the server.
2. Create a SQL Server database and SQL login on that host.
3. Publish on a build machine:

   ```bash
   dotnet publish src/YogaMarketplace.Api -c Release -o ./publish
   ```

4. Upload `./publish` to the site root for `YogaDemo.psoftcs.com`.
5. In Plesk, set the ASP.NET Core app to `YogaMarketplace.Api.dll` and the application pool to **No Managed Code**.
6. Set environment variables on the site (Plesk → ASP.NET Core → environment variables):

   | Name | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__Default` | `Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=true` |
   | `Jwt__Key` | Random string, at least 32 bytes |
   | `Jwt__Issuer` | `YogaMarketplace` |
   | `Jwt__Audience` | `YogaMarketplace` |
   | `Otp__Pepper` | Random string, at least 8 characters |
   | `Otp__ExposeCode` | `false` |
   | `Otp__UseFixedCode` | `false` |
   | `Database__AutoMigrate` | `false` |
   | `Seed__DemoData` | `false` |

   The app refuses to start when `Jwt:Key` is missing. Production also forces the demo OTP and demo seed off via `appsettings.Production.json`.
7. Apply the schema before the first request. From a machine that can reach the database:

   ```bash
   dotnet ef database update --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api --connection "<production connection string>"
   ```

   Or run `deploy.sql` from `dotnet ef migrations script --idempotent` in SSMS.
8. Bind `YogaDemo.psoftcs.com` and the Plesk certificate. TLS terminates at Plesk; the app listens on HTTP behind it.
9. Startup seeds Mumbai areas, the Yoga category, and the TBD policy row. It does not seed Ananya when `Seed__DemoData` is false.
10. OTP delivery is still the log stub. Wire a real SMS or WhatsApp sender before any public login. Razorpay is the next slice, not this deploy.

## Later slices

1. **This PR** — auth, domain, EF, browse/slots skeleton
2. Browse + book + pay (Razorpay). Pay-at-book creates `PendingAccept`
3. Accept / decline / complete, reviews, payout pending
4. Admin approve/reject and oversight
5. Reschedule, cancel, payout export

## Out of scope

Native apps, in-app video (Online stores a Google Meet link), multi-city launch, merch, SOS, and a Meta ads CMS.
