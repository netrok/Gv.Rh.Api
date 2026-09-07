FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY . .
RUN dotnet restore ./Gv.Rh.Api/Gv.Rh.Api.csproj
RUN dotnet publish ./Gv.Rh.Api/Gv.Rh.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

RUN mkdir -p /app/wwwroot /app/storage /app/dataprotection

# Usuario no-root: UID/GID fijos (1000) para poder alinear permisos
# con los volumenes del host en Rocky (ver RUNBOOK.md, seccion DataProtection).
RUN groupadd -g 1000 gvrh \
    && useradd -u 1000 -g gvrh -M -s /usr/sbin/nologin gvrh \
    && chown -R gvrh:gvrh /app

USER gvrh

EXPOSE 8080

ENTRYPOINT ["dotnet", "Gv.Rh.Api.dll"]