FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Dnd.slnx Directory.Build.props ./
COPY src/Dnd.Domain/Dnd.Domain.csproj src/Dnd.Domain/
COPY src/Dnd.Application/Dnd.Application.csproj src/Dnd.Application/
COPY src/Dnd.Infrastructure/Dnd.Infrastructure.csproj src/Dnd.Infrastructure/
COPY src/Dnd.Api/Dnd.Api.csproj src/Dnd.Api/
RUN dotnet restore src/Dnd.Api/Dnd.Api.csproj

# The SRD dataset is embedded into Dnd.Infrastructure from ../../seed/srd.
COPY seed/ seed/
COPY src/ src/
# Version reported by GET /api/v1/app/info and the admin stats (release builds pass the release version).
ARG VERSION=0.1.0
RUN dotnet publish src/Dnd.Api/Dnd.Api.csproj -c Release -o /app/publish --no-restore \
    -p:AssemblyVersion=${VERSION}.0 -p:FileVersion=${VERSION}.0

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is used by the compose healthcheck (the base image ships neither curl nor wget).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /data/files && chown -R "$APP_UID" /data
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    FileStorage__RootPath=/data/files
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Dnd.Api.dll"]
