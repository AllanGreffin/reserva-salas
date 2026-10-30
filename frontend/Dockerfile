FROM node:24-alpine AS build
WORKDIR /src

# Dependências em camada própria: só é refeita quando package*.json muda.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production


# nginx sem root: escuta na 8080 e roda como usuário sem privilégios.
FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/reserva-salas-web/browser /usr/share/nginx/html
EXPOSE 8080
