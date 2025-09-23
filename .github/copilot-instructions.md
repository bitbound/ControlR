# Project Overview
ControlR is a cross-platform solution for remote access and remote control of devices. It primarily uses the latest .NET version for backend web, frontend web (via Blazor), and the desktop applications.

## Project Structure

### Core Components
- **ControlR.Web.Server** - ASP.NET Core web server with API endpoints and SignalR hubs
- **ControlR.Web.Client** - Blazor WebAssembly frontend using MudBlazor components
- **ControlR.Web.AppHost** - .NET Aspire orchestration host for development
- **ControlR.Web.ServiceDefaults** - Shared service configuration and defaults
- **ControlR.Web.WebSocketRelay** - WebSocket relay service for real-time communication
- **ControlR.Agent** - Background service/daemon that runs on controlled devices
- **ControlR.DesktopClient** - Cross-platform Avalonia UI desktop application

### Platform-Specific Components
- **ControlR.DesktopClient.Windows** - Windows-specific desktop client implementations
- **ControlR.DesktopClient.Linux** - Linux-specific desktop client implementations  
- **ControlR.DesktopClient.Mac** - macOS-specific desktop client implementations
- **ControlR.DesktopClient.Common** - Shared desktop client functionality
- **ControlR.Agent.Common** - Shared agent functionality across platforms

### Shared Libraries
- **ControlR.Libraries.Shared** - Core shared models, DTOs, and utilities
- **ControlR.Libraries.DevicesCommon** - Device-related common functionality
- **ControlR.Libraries.DevicesNative** - Native device interaction libraries
- **ControlR.Libraries.Clients** - Client-side shared functionality
- **ControlR.Libraries.Ipc** - Inter-process communication utilities
- **ControlR.Libraries.Signalr.Client** - SignalR client abstractions
- **ControlR.Libraries.WebSocketRelay.Common** - WebSocket relay shared components
- **ControlR.Libraries.NativeInterop.Windows** - Windows native interop
- **ControlR.Libraries.NativeInterop.Unix** - Unix/Linux native interop

## Communication Architecture

### SignalR Hub Pattern
- **AgentHub** - Receives device heartbeats and forwards data to ViewerHub groups
- **ViewerHub** - Handles web client connections and remote control requests
- Agents send `DeviceDto` heartbeats via `UpdateDevice()` which triggers real-time UI updates
- Hub groups organize connections by tenant, device tags, and user roles for targeted messaging

### Agent-DesktopClient IPC
- Agent runs as system service/daemon, DesktopClient runs in user sessions
- Communication via named pipes using `IIpcConnection` abstractions
- Agent forwards remote control requests to appropriate DesktopClient via `RemoteControlRequestIpcDto`
- DesktopClient reports back to Agent, which relays to web server via SignalR

## Technology Stack

### Backend
- **.NET 9** - Latest .NET framework
- **ASP.NET Core** - Web framework for APIs and web hosting
- **SignalR** - Real-time communication
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database (with InMemory option for testing)

### Frontend  
- **Blazor WebAssembly** - Client-side web UI framework
- **MudBlazor** - Material Design component library
- **JavaScript Interop** - For browser-specific functionality

### Desktop Applications
- **Avalonia UI** - Cross-platform .NET UI framework
- **Multi-targeting** - Supports Windows, Linux, and macOS
- **Native Interop** - Platform-specific functionality via P/Invoke

## Architecture Patterns

### Web Architecture
- **Clean Architecture** - Separation of concerns with clear dependencies
- **Dependency Injection** - Built-in .NET DI container

### Desktop Architecture  
- **MVVM Pattern** - Model-View-ViewModel for UI separation
- **Localization** - `Localization.cs` will pull region-specific strings from `/Resources/Strings/{locale}.json`
  - All text in the UI should be bound to localization keys using `x:Static`
- **Command Pattern** - For user actions and operations
- **IMessenger** - Cross-component communication
- **Service Layer** - Business logic abstraction

### Cross-Platform Strategy
- **Shared Libraries** - Common functionality across platforms
- **Platform Abstraction** - Interface-based platform-specific implementations
- **Conditional Compilation** - Platform-specific code paths
- **Device Data Generation** - Platform-specific `DeviceDataGenerator` implementations (Windows uses Win32 APIs, Mac/Linux use shell commands)

## Key Development Workflows

### Device Data Collection Pattern
Device information flows from platform-specific generators:
- `DeviceDataGeneratorBase` - Shared logic (drives, MAC addresses, local IPs)
- `DeviceDataGeneratorWin` - Windows-specific (uses Win32Interop for memory/sessions)
- `DeviceDataGeneratorMac` - macOS-specific (shell commands: `sysctl`, `ps`)
- `DeviceDataGeneratorLinux` - Linux-specific (reads `/proc/meminfo`, uses `ps`)

All inherit from base and implement `CreateDevice()` → returns `DeviceModel` → converted to `DeviceDto` for transport

## Key Features
- **Remote Desktop Control** - Full desktop access and control
- **Screen Sharing** - Real-time screen streaming
- **File Transfer** - Secure file operations between devices
- **Multi-tenancy** - Support for multiple organizations
- **Self-hosting** - Can be deployed on-premises
- **Cross-platform** - Windows, Linux, macOS support
- **Authentication** - Identity Framework with optional OAuth integration (GitHub, Microsoft)
- **Real-time Communication** - SignalR and WebSocket support


## Development Guidelines

### General Coding Standards
- Use 2 spaces for indentation
- Don't append "Async" suffix to async method names, unless to specifically distinguish from an existing sync method of the same name

### Build and Task System
- Primary build: `dotnet build ControlR.sln` (VS Code default build task)
- Component builds follow dependency order: Server → Agent → DesktopClient
- Docker development via `docker-compose/docker-compose.yml` with required environment variables
- Use `.vscode/tasks.json` or `ControlR.slnLaunch` launch profiles for common workflows

### C# Coding Standards
- Use the latest C# language features and default recommendations.
- Use StyleCop conventions when ordering class members.
- Do not use null-forgiving operator (!) outside of tests.  Handle null checks appropriately. 
- Prefer var of explicit types for variables.
- Reduce indentation by returning/continuing early and inverting conditions when appropriate.
- Always prefer collection expressions to initialize collections (e.g. '[]').
- If an interface only has one implementation, keep the interface and implementation in the same file.

### Platform-Specific Development
- Use `[SupportedOSPlatform]` attributes for platform-specific code
- Conditional compilation symbols: `WINDOWS_BUILD`, `MAC_BUILD`, `LINUX_BUILD`, `UNIX_BUILD`
- Platform detection via `ISystemEnvironment.Platform` and `RuntimeInformation`

### Web UI Guidelines
- Use MudBlazor components where appropriate for the UI.
- Prefer using code-behind CS files for Razor components instead of using the `@code {}` block in Razor files.

### Project Organization
- Follow the established folder structure and naming conventions.
- Keep platform-specific code in appropriate platform projects.
- Use shared libraries for common functionality across projects.
- DTOs go under `/Libraries/ControlR.Libraries.Shared/Dtos/`, under their respective namespace.
  - `HubDtos` contain DTOs used in SignalR hubs.
  - `IpcDtos` contain DTOs used in the IPC connection between `Agent` and `DesktopClient`.
  - `ServerApi` contains DTOs used in the REST API.
  - `StreamerDtos` contain DTOs used by remote control, which get routed through the websocket relay.
- Maintain clear separation between business logic and UI code.

### SignalR Communication Patterns
- Device heartbeats flow: `Agent` → `AgentHub.UpdateDevice()` → `ViewerHub` groups
- Remote control requests: `ViewerHub` → `AgentHub` → `IPC` → `DesktopClient`
- Use hub groups for tenant isolation and role-based access control
- Group naming pattern: `HubGroupNames.GetTenantDevicesGroupName()`, `GetTagGroupName()`, etc.

### Testing Strategy
- Use xUnit for unit testing.
- Write unit tests for business logic and services.
- Maintain test coverage for shared libraries.
- For server tests, use helpers `Tests\ControlR.Web.Server.Tests\Helpers\` when appropriate.

### Security Considerations
- Always validate and sanitize user inputs.
- Use `AuthorizeAttribute` and `IAuthorizationService` for endpoint authorization.

### Performance Guidelines
- Optimize database queries with proper indexing.
- Use async/await patterns for I/O operations.
- Cache frequently accessed data appropriately.

### Error Handling
- Use structured logging with Serilog.
- Implement proper exception handling and recovery.
- Provide meaningful error messages to users.
- Log errors with appropriate context for debugging.

### Documentation
- Use XML documentation comments for public APIs.
- Maintain README files for complex components.
- Document configuration options and environment variables.
- Keep API documentation up to date.