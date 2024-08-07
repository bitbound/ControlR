#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt update
RUN apt -y install curl
RUN mkdir -p /app/AppData
RUN chown app:app -R /app/AppData
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ControlR.Server/ControlR.Server.csproj", "ControlR.Server/"]
COPY ["ControlR.Shared/ControlR.Shared.csproj", "ControlR.Shared/"]
RUN dotnet restore "./ControlR.Server/./ControlR.Server.csproj"
COPY . .
WORKDIR "/src/ControlR.Server"
RUN dotnet build "./ControlR.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ControlR.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ControlR.Server.dll"]

HEALTHCHECK \
  CMD curl -f http://localhost:8080/api/health || exit 1