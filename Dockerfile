# syntax=docker/dockerfile:1

# ---- 1. Frontend assets: Tailwind CSS build + vendored htmx ----
FROM node:22-alpine AS assets
WORKDIR /app
COPY src/Nuskha.Web/package.json ./
RUN npm install --no-audit --no-fund
COPY src/Nuskha.Web/Styles ./Styles
COPY src/Nuskha.Web/Pages ./Pages
RUN mkdir -p wwwroot/css wwwroot/lib && npm run build

# ---- 2. Build and publish the .NET app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Nuskha.Domain/*.csproj src/Nuskha.Domain/
COPY src/Nuskha.Infrastructure/*.csproj src/Nuskha.Infrastructure/
COPY src/Nuskha.Web/*.csproj src/Nuskha.Web/
RUN dotnet restore src/Nuskha.Web/Nuskha.Web.csproj
COPY src/ src/
COPY --from=assets /app/wwwroot/css/app.css src/Nuskha.Web/wwwroot/css/app.css
COPY --from=assets /app/wwwroot/lib/htmx.min.js src/Nuskha.Web/wwwroot/lib/htmx.min.js
RUN dotnet publish src/Nuskha.Web/Nuskha.Web.csproj -c Release -o /out --no-restore

# ---- 3. Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /out .
# Uploads (covers, avatars) and data-protection keys live on a volume so they survive container replacement.
ENV ASPNETCORE_URLS=http://+:8080 \
    Uploads__Path=/data/uploads \
    DataProtection__Path=/data/keys
RUN mkdir -p /data/uploads /data/keys && chown -R $APP_UID /data
VOLUME /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Nuskha.Web.dll"]
