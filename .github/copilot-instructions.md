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
- **ControlR.Streamer** - Screen capture and streaming component

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

## Technology Stack

### Backend
- **.NET 9** - Latest .NET framework
- **ASP.NET Core** - Web framework for APIs and web hosting
- **SignalR** - Real-time communication
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database (with InMemory option for testing)
- **.NET Aspire** - Cloud-native orchestration for development

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
- **Localization** - Localization.cs will pull region-specific strings from `/Resources/Strings/{locale}.json`
- **Command Pattern** - For user actions and operations
- **IMessenger** - Cross-component communication
- **Service Layer** - Business logic abstraction

### Cross-Platform Strategy
- **Shared Libraries** - Common functionality across platforms
- **Platform Abstraction** - Interface-based platform-specific implementations
- **Conditional Compilation** - Platform-specific code paths

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

### C# Coding Standards
- Use the latest C# language features and default recommendations.
- Use StyleCop conventions when ordering class members.
- Prefer var of explicit types for variables.
- Reduce indentation by returning/continuing early and inverting conditions when appropriate.
- Prefer simplified collection initializers where appropriate (e.g. '[]').

### Web UI Guidelines
- Use MudBlazor components where appropriate for the UI.
- Prefer using code-behind CS files for Razor components instead of using the `@code {}` block in Razor files.

### Project Organization
- Follow the established folder structure and naming conventions
- Keep platform-specific code in appropriate platform projects
- Use shared libraries for common functionality across projects
- Maintain clear separation between business logic and UI code

### Testing Strategy
- Write unit tests for business logic and services
- Use integration tests for API endpoints and database operations
- Include load testing for performance-critical components
- Maintain test coverage for shared libraries

### Security Considerations
- Always validate and sanitize user inputs
- Use proper authentication and authorization patterns
- Implement secure communication protocols
- Follow OWASP guidelines for web security
- Properly handle sensitive data and credentials

### Performance Guidelines
- Optimize database queries with proper indexing
- Use async/await patterns for I/O operations
- Cache frequently accessed data appropriately

### Error Handling
- Use structured logging with Serilog
- Implement proper exception handling and recovery
- Provide meaningful error messages to users
- Log errors with appropriate context for debugging

### Documentation
- Use XML documentation comments for public APIs
- Maintain README files for complex components
- Document configuration options and environment variables
- Keep API documentation up to date