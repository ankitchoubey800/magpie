# Build from the REPOSITORY ROOT:
#   docker build -f src/gateway/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/shared/Magpie.Defaults/Magpie.Defaults.csproj src/shared/Magpie.Defaults/
COPY src/shared/Magpie.Mediator/Magpie.Mediator.csproj src/shared/Magpie.Mediator/
COPY src/gateway/Magpie.Gateway/Magpie.Gateway.csproj src/gateway/Magpie.Gateway/
RUN dotnet restore src/gateway/Magpie.Gateway/Magpie.Gateway.csproj

COPY src/shared/ src/shared/
COPY src/gateway/ src/gateway/
RUN dotnet publish src/gateway/Magpie.Gateway/Magpie.Gateway.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Magpie.Gateway.dll"]
