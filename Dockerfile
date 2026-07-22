# --- Stage 1: build the Angular SPA ---
FROM node:22-alpine AS web
WORKDIR /src/web/oee-shell
COPY web/oee-shell/package.json web/oee-shell/package-lock.json ./
RUN npm ci
COPY web/oee-shell/ ./
RUN npm run build

# --- Stage 2: build and publish the .NET API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OeeNew.Api/OeeNew.Api.csproj src/OeeNew.Api/
COPY src/OeeNew.Application/OeeNew.Application.csproj src/OeeNew.Application/
COPY src/OeeNew.Domain/OeeNew.Domain.csproj src/OeeNew.Domain/
COPY src/OeeNew.Infrastructure/OeeNew.Infrastructure.csproj src/OeeNew.Infrastructure/
RUN dotnet restore src/OeeNew.Api/OeeNew.Api.csproj
COPY src/ src/
RUN dotnet publish src/OeeNew.Api/OeeNew.Api.csproj -c Release -o /app/publish --no-restore

# --- Stage 3: runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# Npgsql dlopens libgssapi_krb5 for GSS encryption negotiation with Postgres; the slim base
# image doesn't ship it, which crashes every DB connection attempt at runtime.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web /src/web/oee-shell/dist/oee-shell/browser ./wwwroot
ENTRYPOINT ["dotnet", "OeeNew.Api.dll"]
