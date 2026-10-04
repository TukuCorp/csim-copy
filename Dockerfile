# syntax=docker/dockerfile:1

# The CarbonSim host in a container: build once with the SDK image, run as a non-root user on the
# ASP.NET runtime image. The scenarios folder is copied in beside the published app, and /data is
# the one writable place, so the SQLite file survives a container being replaced.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# The central package versions and build settings are needed to restore, so they come first and
# stay cached while only the source changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/CarbonSim.Engine/CarbonSim.Engine.csproj src/CarbonSim.Engine/
COPY src/CarbonSim.Data/CarbonSim.Data.csproj src/CarbonSim.Data/
COPY src/CarbonSim.Web/CarbonSim.Web.csproj src/CarbonSim.Web/
RUN dotnet restore src/CarbonSim.Web/CarbonSim.Web.csproj

COPY src/ src/
RUN dotnet publish src/CarbonSim.Web/CarbonSim.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 +    DOTNET_EnableDiagnostics=0 +    CarbonSim__Provider=Sqlite +    CarbonSim__ConnectionString="Data Source=/data/carbonsim.sqlite" +    CarbonSim__ScenarioFile=/app/scenarios/vietnam-2024.json

COPY --from=build /app/publish .
COPY scenarios/ /app/scenarios/

# The image ships an unprivileged 'app' user; the data directory has to belong to it before the
# process drops to it.
RUN mkdir -p /data && chown -R $APP_UID /data

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "CarbonSim.Web.dll"]
