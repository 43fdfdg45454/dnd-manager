FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenTrpg.slnx Directory.Build.props ./
COPY src/Core/OpenTrpg.Core.Domain/OpenTrpg.Core.Domain.csproj src/Core/OpenTrpg.Core.Domain/
COPY src/Core/OpenTrpg.Core.Application/OpenTrpg.Core.Application.csproj src/Core/OpenTrpg.Core.Application/
COPY src/Core/OpenTrpg.Core.Infrastructure/OpenTrpg.Core.Infrastructure.csproj src/Core/OpenTrpg.Core.Infrastructure/
COPY src/Core/OpenTrpg.Core.Api.Abstractions/OpenTrpg.Core.Api.Abstractions.csproj src/Core/OpenTrpg.Core.Api.Abstractions/
COPY src/Core/OpenTrpg.Core.Api/OpenTrpg.Core.Api.csproj src/Core/OpenTrpg.Core.Api/
COPY src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Domain/OpenTrpg.Systems.Dnd5e.Domain.csproj src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Domain/
COPY src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Application/OpenTrpg.Systems.Dnd5e.Application.csproj src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Application/
COPY src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Infrastructure/OpenTrpg.Systems.Dnd5e.Infrastructure.csproj src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Infrastructure/
COPY src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Api/OpenTrpg.Systems.Dnd5e.Api.csproj src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Api/
RUN dotnet restore src/Core/OpenTrpg.Core.Api/OpenTrpg.Core.Api.csproj

# The SRD dataset is embedded into OpenTrpg.Core.Infrastructure from ../../../seed/srd.
COPY seed/ seed/
COPY src/ src/
# Version reported by GET /api/v1/app/info and the admin stats (release builds pass the release version).
ARG VERSION=0.1.0
RUN dotnet publish src/Core/OpenTrpg.Core.Api/OpenTrpg.Core.Api.csproj -c Release -o /app/publish --no-restore \
    -p:AssemblyVersion=${VERSION}.0 -p:FileVersion=${VERSION}.0

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is used by the compose healthcheck (the base image ships neither curl nor wget).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /data/files && chown -R "$APP_UID" /data
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    FileStorage__RootPath=/data/files
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenTrpg.Core.Api.dll"]
