# Build from the REPOSITORY ROOT:
#   docker build -f src/services/collector/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/shared/Magpie.Defaults/Magpie.Defaults.csproj src/shared/Magpie.Defaults/
COPY src/shared/Magpie.Mediator/Magpie.Mediator.csproj src/shared/Magpie.Mediator/
COPY src/services/collector/Magpie.Collector.Domain/Magpie.Collector.Domain.csproj src/services/collector/Magpie.Collector.Domain/
COPY src/services/collector/Magpie.Collector.Application/Magpie.Collector.Application.csproj src/services/collector/Magpie.Collector.Application/
COPY src/services/collector/Magpie.Collector.Infrastructure/Magpie.Collector.Infrastructure.csproj src/services/collector/Magpie.Collector.Infrastructure/
COPY src/services/collector/Magpie.Collector.Worker/Magpie.Collector.Worker.csproj src/services/collector/Magpie.Collector.Worker/
RUN dotnet restore src/services/collector/Magpie.Collector.Worker/Magpie.Collector.Worker.csproj

COPY src/shared/ src/shared/
COPY src/services/collector/ src/services/collector/
RUN dotnet publish src/services/collector/Magpie.Collector.Worker/Magpie.Collector.Worker.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Magpie.Collector.Worker.dll"]
