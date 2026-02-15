FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:1.27-alpine
RUN apk add --no-cache gettext
COPY nginx.conf /etc/nginx/default.conf.template
COPY docker-entrypoint.d/30-envsubst-backend-url.sh /docker-entrypoint.d/30-envsubst-backend-url.sh
RUN chmod +x /docker-entrypoint.d/30-envsubst-backend-url.sh
COPY --from=build /app/dist/lumirise-ui/browser /usr/share/nginx/html

EXPOSE 80
