# Yoga Marketplace

Mumbai-first yoga instructor marketplace. The API covers OTP auth, the domain model, EF Core, SQL Server, verified browse, and **pay-at-book** (Razorpay). Customers authenticate with a one-time passcode, instructors can register (they stay pending until a later admin slice), and verified instructors can be browsed with **mode-specific** slots (Home, Studio, Online). A captured payment creates a booking in `PendingAccept`.

Yoga is the first `Category`. The model is generic enough for another category later. The customer web app is `src/YogaMarketplace.Web`: phone OTP, Mumbai area, verified instructors, then book and pay. The browser never calls the Razorpay webhook.

## Solution

| Project | Role |
| --- | --- |
| `src/YogaMarketplace.Api` | Controllers, OTP/JWT, browse, slots, book and pay |
| `src/YogaMarketplace.Domain` | Entities and booking rules |
| `src/YogaMarketplace.Infrastructure` | EF Core, SQL Server, seed |
| `src/YogaMarketplace.Web` | Razor Pages customer app (OTP, area, browse, book and pay) |
| `tests/YogaMarketplace.Api.Tests` | Domain rules and API tests (SQLite) |
| `tests/YogaMarketplace.Web.Tests` | Customer shell against the API test host |

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

## Customer web

`src/YogaMarketplace.Web` is a mobile-first Razor Pages shell. It does not open SQL Server. It calls the API with `HttpClient` (`Api:BaseUrl`, default `http://localhost:5080`).

Start SQL Server and the API (sections above), then in a second terminal:

```bash
dotnet run --project src/YogaMarketplace.Web
```

Web: `http://localhost:5081`

1. **Account.** New customers send name, gender (Female, Male, or Other), and phone. Existing customers send phone only. Verify the code.
2. **Area.** Pick a Mumbai neighbourhood from `GET /api/areas`.
3. **Instructors.** Filter by area and Home / Studio / Online. The list is verified instructors for the `yoga` category (`Api:CategorySlug`). Open a profile to see this week's slots.
4. **Book and pay.** Choose a slot. Home asks for an address and a landmark before the order is created. Studio and Online go straight to checkout. Development uses a local stand-in for Razorpay Checkout (`Payments:UseFakeCheckout`). Pay calls `POST /api/bookings/confirm` and the booking is `PendingAccept`. Payment failed and Cancel payment do not confirm, so no booking is created.
5. **My bookings.** `GET /api/bookings/me` for the signed-in customer. The page is `/bookings`.

The API JWT from `POST /api/auth/otp/verify` is stored in the encrypted `ym.session` cookie and sent as `Authorization: Bearer` on later API calls. The chosen area is the `ym.area` cookie.

In Development the API code is `123456` and the response includes `devCode`. The verify step shows that code (and fills it in) when the API returns it. `Api:ShowDevOtpHint` is true only in `appsettings.Development.json`, which is not published. Seeded instructor Ananya Desai (`+919876543210`, Bandra) can sign in with her phone.

If the API is stopped, pages show an error and empty lists. The web app does not keep a second catalog or a fake OTP store.

Labels live in `src/YogaMarketplace.Web/Copy/UiCopy.cs` so the first category and city can be renamed later without changing the flow.

## Dev OTP and seed

SMS is a log stub (`LoggingOtpSender`). In Development the code is fixed at `123456` and the request response includes `devCode`.

| Who | Phone | Notes |
| --- | --- | --- |
| Ananya Desai | `+919876543210` | Verified, Bandra, Home ₹899 / Studio ₹749 / Online ₹599, Google Meet link on her own profile |
| Marketplace admin | `+919000000001` | Seeded user only. Admin APIs are a later slice |

New customer: `name` + `gender` + `phone`, then OTP. Existing customer: `phone`, then OTP. Slots are Mumbai local time (`Asia/Kolkata`), separate per Home / Studio / Online. Bookings are not seeded.

`Seed:DemoData` is off in Production. `appsettings.Development.json` is excluded from `dotnet publish`.

## HTTP API

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

Booking states: `PendingAccept` → `Upcoming` or `Declined` → `Completed`, `NoShow`, or `Cancelled`. A captured Razorpay payment creates `PendingAccept`. Decline (refund) and complete (review + payout) stay domain rules without HTTP in this slice. Complete would record a pending payout (`gross − fee%`). The fee is **not** copied onto the payment at book time.

## Book and pay (local)

Customer JWT only. Create a Razorpay order for a free slot, then confirm from the checkout callback or from the `payment.captured` webhook. Nothing is written to `Bookings` until the payment is captured. An unpaid checkout is a `CheckoutIntent` row only.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/bookings/orders` | Customer Bearer | Razorpay order for one slot. Home requires `homeAddress` and `landmark`. |
| POST | `/api/bookings/confirm` | Customer Bearer | Verify `orderId`, `paymentId`, and HMAC signature. Idempotent for the same payment id. |
| GET | `/api/bookings/me` | Customer Bearer | That customer's bookings, including the Meet link snapshot after an online booking. |
| POST | `/api/webhooks/razorpay` | `X-Razorpay-Signature` | `payment.captured` creates the same booking. Other events are acknowledged and do not book. |

Rules enforced here:

- Verified instructors only. The slot's mode (Home, Studio, Online) must be one they offer, and the slot must be free under `BookingRules.OccupiesSlot`.
- Price is the instructor's rate for that mode. The client does not send an amount.
- Online stores the instructor's Google Meet link on the booking at capture. Public browse still omits it.
- Studio stores the studio address on the booking.
- The same Razorpay payment id cannot create a second booking. A second captured payment for a slot that was just taken is rejected and does not insert a booking. Refund of that losing payment is a later slice.
- Platform fee percent stays on `MarketplacePolicy`. Payout rows are still created only when a booking is completed.

### Razorpay configuration

Do not commit live keys. `appsettings.json` leaves them empty. Development uses a **fake gateway** (`Razorpay:UseFakeGateway` true) and placeholder secrets that are not Razorpay credentials. Production refuses to start when the fake gateway is on.

| Configuration | Environment variable | Purpose |
| --- | --- | --- |
| `Razorpay:KeyId` | `Razorpay__KeyId` | Key id. Returned to the customer when an order is created. |
| `Razorpay:KeySecret` | `Razorpay__KeySecret` | HMAC secret for the checkout signature (`orderId\|paymentId`). |
| `Razorpay:WebhookSecret` | `Razorpay__WebhookSecret` | HMAC secret for the raw webhook body. |
| `Razorpay:UseFakeGateway` | `Razorpay__UseFakeGateway` | `true` skips Razorpay HTTP and issues `order_fake_…` ids. Signatures are still checked. |

Fake mode is on in `appsettings.Development.json` and in the API tests. To call Razorpay's test API from this machine, put real **test** keys in user secrets or the environment and turn the fake gateway off:

```bash
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_..." --project src/YogaMarketplace.Api
dotnet user-secrets set "Razorpay:KeySecret" "..." --project src/YogaMarketplace.Api
dotnet user-secrets set "Razorpay:WebhookSecret" "..." --project src/YogaMarketplace.Api
dotnet user-secrets set "Razorpay:UseFakeGateway" "false" --project src/YogaMarketplace.Api
```

The checkout signature is hex HMAC-SHA256 of `{orderId}|{paymentId}` with `Razorpay:KeySecret`. The webhook signature is hex HMAC-SHA256 of the raw body with `Razorpay:WebhookSecret`, sent in `X-Razorpay-Signature`. Subscribe the webhook to `payment.captured`. The web app does not call the webhook.

In Development the web pay page does not load `checkout.razorpay.com`. `Payments:UseFakeCheckout` is true only in `appsettings.Development.json` (not published). The server signs with `Payments:KeySecret`, the same Development placeholder as `Razorpay:KeySecret` (`dev-only-not-a-live-key-secret`). That value is not a live credential and is not sent to the browser. Pay now, Payment failed, and Cancel payment are the local checkout. When `Payments:UseFakeCheckout` is false, the pay page opens Razorpay Checkout.js and posts the returned order id, payment id, and signature to confirm. Production refuses to start if the fake checkout is on.

Local fake example, after OTP verify and `GET /api/providers/{id}/slots?mode=Home`:

```bash
curl -s -X POST http://localhost:5080/api/bookings/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"slotId":"<slot>","homeAddress":"14th Road, Bandra West","landmark":"Near the station"}'
```

Sign `orderId|paymentId` with `dev-only-not-a-live-key-secret` (the Development placeholder) and `POST /api/bookings/confirm`.

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
   | `Razorpay__KeyId` | Razorpay key id. Required before any live checkout. |
   | `Razorpay__KeySecret` | Razorpay key secret. |
   | `Razorpay__WebhookSecret` | Razorpay webhook signing secret. |
   | `Razorpay__UseFakeGateway` | `false` |

   The app refuses to start when `Jwt:Key` is missing. Production also forces the demo OTP and demo seed off via `appsettings.Production.json`.
7. Apply the schema before the first request. From a machine that can reach the database:

   ```bash
   dotnet ef database update --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Api --connection "<production connection string>"
   ```

   Or run `deploy.sql` from `dotnet ef migrations script --idempotent` in SSMS.
8. Bind `YogaDemo.psoftcs.com` and the Plesk certificate. TLS terminates at Plesk; the app listens on HTTP behind it.
9. Startup seeds Mumbai areas, the Yoga category, and the TBD policy row. It does not seed Ananya when `Seed__DemoData` is false.
10. OTP delivery is still the log stub. Wire a real SMS or WhatsApp sender before any public login. Book and pay is in the API; set the Razorpay variables above before taking a live payment. `Razorpay__UseFakeGateway` must stay `false` on this host.

## Later slices

1. Auth, domain, EF, browse/slots — already in the repo
2. Customer web (OTP, area, verified browse, book and pay) and book + pay HTTP — already in the repo. Pay-at-book creates `PendingAccept`
3. Accept / decline / complete, reviews, payout pending, and refund of a captured payment whose slot was lost
4. Admin approve/reject and oversight
5. Reschedule, cancel, payout export

## Out of scope

Native apps, in-app video (Online stores a Google Meet link), multi-city launch, merch, SOS, and a Meta ads CMS.
