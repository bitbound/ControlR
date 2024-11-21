# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt update
RUN apt -y install curl
RUN mkdir -p /app/AppData
RUN chown app:app -R /app/AppData
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["ControlR.Web.Server/ControlR.Web.Server.csproj", "ControlR.Web.Server/"]
COPY ["ControlR.Web.Client/ControlR.Web.Client.csproj", "ControlR.Web.Client/"]
COPY ["Libraries/ControlR.Libraries.Clients/ControlR.Libraries.Clients.csproj", "Libraries/ControlR.Libraries.Clients/"]
COPY ["Libraries/ControlR.Libraries.Shared/ControlR.Libraries.Shared.csproj", "Libraries/ControlR.Libraries.Shared/"]
COPY ["Libraries/ControlR.Libraries.Signalr.Client/ControlR.Libraries.Signalr.Client.csproj", "Libraries/ControlR.Libraries.Signalr.Client/"]
COPY ["ControlR.Web.ServiceDefaults/ControlR.Web.ServiceDefaults.csproj", "ControlR.Web.ServiceDefaults/"]
RUN dotnet restore "./ControlR.Web.Server/ControlR.Web.Server.csproj"
COPY . .
WORKDIR "/src/ControlR.Web.Server"
RUN dotnet build "./ControlR.Web.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG CURRENT_VERSION
RUN dotnet publish "./ControlR.Web.Server.csproj" -c $BUILD_CONFIGURATION -p:Version=$CURRENT_VERSION -p:FileVersion=$CURRENT_VERSION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ControlR.Web.Server.dll"]

HEALTHCHECK \
  CMD curl -f http://localhost:8080/health || exit 1