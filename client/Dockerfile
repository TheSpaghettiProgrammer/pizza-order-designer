# Build stage: compile the Quasar SPA to static files
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
# Skip the postinstall (`quasar prepare`): it needs quasar.config.ts, and `quasar build` runs it anyway
RUN npm ci --no-audit --no-fund --ignore-scripts
COPY . .
RUN npx quasar build

# Runtime stage: nginx serves the SPA and reverse-proxies /api to the api container
FROM nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/spa /usr/share/nginx/html
EXPOSE 80
