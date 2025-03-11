#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Install clang/zlib1g-dev dependencies for publishing to native
RUN apt-get update \
  && apt-get install -y --no-install-recommends \
  clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ControlR.Web.WebSocketRelay/ControlR.Web.WebSocketRelay.csproj", "ControlR.Web.WebSocketRelay/"]
COPY ["ControlR.Web.WebSocketRelay.Common/ControlR.Web.WebSocketRelay.Common.csproj", "ControlR.Web.WebSocketRelay.Common/"]
RUN dotnet restore "./ControlR.Web.WebSocketRelay/ControlR.Web.WebSocketRelay.csproj"
COPY . .
WORKDIR "/src/ControlR.Web.WebSocketRelay"
RUN dotnet build "./ControlR.Web.WebSocketRelay.csproj" -c $BUILD_CONFIGURATION -p:GeneratePackageOnBuild=false -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ControlR.Web.WebSocketRelay.csproj" -c $BUILD_CONFIGURATION -p:GeneratePackageOnBuild=false -o /app/publish /p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS final
RUN apt-get -y update
RUN apt-get -y install curl
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./ControlR.Web.WebSocketRelay"]

HEALTHCHECK \
  CMD curl -f http://localhost:8080/health || exit 1