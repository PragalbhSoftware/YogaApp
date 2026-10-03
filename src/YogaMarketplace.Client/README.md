# Yoga Marketplace Client

React SPA for the Yoga Marketplace API, and the project's only front end. See the root `README.md` for the API, configuration, and production routing.

Cities and areas come from the API. Never hardcode a city in copy, titles, or defaults; the product tagline lives in `src/constants/site.ts`.

## Stack

Vite, React, TypeScript, MUI, Tailwind, React Router, TanStack Query, Zustand, Axios, React Hook Form, Zod, Vitest, Testing Library.

## Run locally

Start the API (`dotnet run --project src/YogaMarketplace.Api --launch-profile http`), then:

```bash
cd src/YogaMarketplace.Client
npm ci
npm run dev
```

App: `http://localhost:5173/login`

`VITE_API_BASE_URL` is empty in development, so requests go to the same origin and Vite proxies `/api` to `http://localhost:5080`. For a build that talks to a separate API origin, set `VITE_API_BASE_URL` (see `.env.example`) and add the SPA origin to the API's `Cors:AllowedOrigins`.

Development OTP is `123456`. Seeded provider `9876543210`, admin `9000000001`.

## Checks

```bash
npm run typecheck
npm run lint
npm test
npm run build
```
