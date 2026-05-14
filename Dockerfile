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

RUN mkdir -p /app/wwwroot /app/storage

EXPOSE 8080

ENTRYPOINT ["dotnet", "Gv.Rh.Api.dll"]