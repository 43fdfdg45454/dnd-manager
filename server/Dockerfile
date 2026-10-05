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
RUN dotnet publish src/Dnd.Api/Dnd.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /data/files && chown -R "$APP_UID" /data
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    FileStorage__RootPath=/data/files
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Dnd.Api.dll"]
