# Yoga Marketplace Client

React SPA for the Yoga Marketplace API. Separate from `YogaMarketplace.Web`.

India product: first live city is Mumbai, then other Indian cities in phases. Do not lock UI copy to Mumbai.

## Stack

Vite, React, TypeScript, MUI, Tailwind, React Router, TanStack Query, Zustand, Axios, React Hook Form, Zod.

## Run locally

Start the API (`dotnet run --project src/YogaMarketplace.Api`), then:

```bash
cd src/YogaMarketplace.Client
npm install
npm run dev
```

App: `http://localhost:5173/login`

Vite proxies `/api` to `http://localhost:5080`. `VITE_API_BASE_URL` is empty in development so the proxy is used.

Development OTP is `123456`. Seeded instructor `9876543210`, admin `9000000001`.
