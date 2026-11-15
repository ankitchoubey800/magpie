# Build from src/web:
#   docker build -t magpie-web src/web

# ---- Stage 1: build the static files with Node.js ----
FROM node:26-alpine AS build
WORKDIR /app

# Same caching trick as the .NET images: dependencies first, source second.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ---- Stage 2: serve the static files with nginx (no Node.js in the final image) ----
FROM nginx:stable-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
