# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:9.0-noble-chiseled-extra AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Tests/ControlR.Agent.LoadTester/ControlR.Agent.LoadTester.csproj", "Tests/ControlR.Agent.LoadTester/"]
COPY ["ControlR.Agent.Common/ControlR.Agent.Common.csproj", "ControlR.Agent.Common/"]
COPY ["ControlR.Web.ServiceDefaults/ControlR.Web.ServiceDefaults.csproj", "ControlR.Web.ServiceDefaults/"]
COPY ["Libraries/ControlR.Libraries.Clients/ControlR.Libraries.Clients.csproj", "Libraries/ControlR.Libraries.Clients/"]
COPY ["Libraries/ControlR.Libraries.Shared/ControlR.Libraries.Shared.csproj", "Libraries/ControlR.Libraries.Shared/"]
COPY ["Libraries/ControlR.Libraries.DevicesCommon/ControlR.Libraries.DevicesCommon.csproj", "Libraries/ControlR.Libraries.DevicesCommon/"]
COPY ["Libraries/ControlR.Libraries.DevicesNative/ControlR.Libraries.DevicesNative.csproj", "Libraries/ControlR.Libraries.DevicesNative/"]
COPY ["Libraries/ControlR.Libraries.Signalr.Client/ControlR.Libraries.Signalr.Client.csproj", "Libraries/ControlR.Libraries.Signalr.Client/"]
RUN dotnet restore "./Tests/ControlR.Agent.LoadTester/ControlR.Agent.LoadTester.csproj"
COPY . .
WORKDIR "/src/Tests/ControlR.Agent.LoadTester"
RUN dotnet build "./ControlR.Agent.LoadTester.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ControlR.Agent.LoadTester.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ControlR.Agent.LoadTester.dll"]