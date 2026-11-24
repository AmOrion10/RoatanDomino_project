# syntax=docker/dockerfile:1

# Stage 1: Build & publish DominoServer (Release)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for optimal Docker layer caching
COPY DominoCommon/DominoCommon.csproj DominoCommon/
COPY DominoServer/src/Domain/Domain.csproj DominoServer/src/Domain/
COPY DominoServer/src/Application/Application.csproj DominoServer/src/Application/
COPY DominoServer/src/Infrastructure/Infrastructure.csproj DominoServer/src/Infrastructure/
COPY DominoServer/src/Web/Web.csproj DominoServer/src/Web/

RUN dotnet restore DominoServer/src/Web/Web.csproj

# Copy source trees and publish Release binary
COPY DominoCommon/ DominoCommon/
COPY DominoServer/src/ DominoServer/src/

RUN dotnet publish DominoServer/src/Web/Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 2: Minimal ASP.NET Core 8.0 Linux runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    DOTNET_ENVIRONMENT=Production \
    PORT=5000 \
    DOMINO_MAX_CLIENTS=200

COPY --from=build /app/publish .

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/5000 && printf "GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3 && grep -q "200 OK" <&3'

USER app
ENTRYPOINT ["dotnet", "Web.dll"]
