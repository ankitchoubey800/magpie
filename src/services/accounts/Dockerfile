# Build from the REPOSITORY ROOT so the shared projects are visible:
#   docker build -f src/services/accounts/Dockerfile .

# ---- Stage 1: build (large image with the full SDK; thrown away afterwards) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy ONLY the project files first and restore packages. Docker caches this
# layer, so packages are downloaded again only when a .csproj changes.
COPY global.json ./
COPY src/shared/Magpie.Defaults/Magpie.Defaults.csproj src/shared/Magpie.Defaults/
COPY src/services/accounts/Magpie.Accounts.Domain/Magpie.Accounts.Domain.csproj src/services/accounts/Magpie.Accounts.Domain/
COPY src/services/accounts/Magpie.Accounts.Application/Magpie.Accounts.Application.csproj src/services/accounts/Magpie.Accounts.Application/
COPY src/services/accounts/Magpie.Accounts.Infrastructure/Magpie.Accounts.Infrastructure.csproj src/services/accounts/Magpie.Accounts.Infrastructure/
COPY src/services/accounts/Magpie.Accounts.Api/Magpie.Accounts.Api.csproj src/services/accounts/Magpie.Accounts.Api/
RUN dotnet restore src/services/accounts/Magpie.Accounts.Api/Magpie.Accounts.Api.csproj

# Now copy the source code and publish a Release build.
COPY src/shared/ src/shared/
COPY src/services/accounts/ src/services/accounts/
RUN dotnet publish src/services/accounts/Magpie.Accounts.Api/Magpie.Accounts.Api.csproj \
    --configuration Release --no-restore --output /app/publish

# ---- Stage 2: run (small image with only the ASP.NET Core runtime) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# A folder for the data-protection keys (they encrypt the session cookie).
# Docker Compose mounts a volume here so the keys survive restarts. The folder
# must belong to the non-root user BEFORE we switch to it, or the service
# could not write its keys.
RUN mkdir /keys && chown $APP_UID /keys

# Do not run as root. APP_UID is a non-root user built into Microsoft's images.
USER $APP_UID

# .NET container images listen on port 8080 by default.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Magpie.Accounts.Api.dll"]
