# syntax=docker/dockerfile:1

# Build the SPA. Only public VITE_ settings belong here; they ship in the browser bundle.
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# Serve it with Nginx as a non-root user on port 8080.
FROM nginxinc/nginx-unprivileged:1.27-alpine
# API_UPSTREAM: where /api is proxied when the app and API share an origin.
# API_ORIGIN: set only when VITE_API_BASE_URL points at another origin, so the browser may call it.
ENV API_UPSTREAM=http://api:8080 \
    API_ORIGIN=""
COPY nginx/templates/ /etc/nginx/templates/
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -q --spider http://127.0.0.1:8080/index.html || exit 1
