# Base runtime image. /data is the canonical mount point for the SQLite DB,
# uploads/, logs/ and optional backups/ — Program.cs honours LUMINA_DATA_DIR.
# We pre-create it and chown to APP_UID so a fresh named volume is writable.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
USER $APP_UID
EXPOSE 8080

# Build stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LuminaVault-Backend.csproj", "."]
RUN dotnet restore "./LuminaVault-Backend.csproj"
COPY . .
RUN dotnet build "./LuminaVault-Backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LuminaVault-Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LuminaVault-Backend.dll"]