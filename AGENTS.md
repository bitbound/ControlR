---
applyTo: '**'
---

# ControlR

Cross-platform remote access and control. .NET 10 backend (ASP.NET Core), Blazor WebAssembly frontend, Avalonia desktop apps.

## Build & Run

- Build: `dotnet build ControlR.slnx --verbosity quiet` (no output = success)
- Run: Use IDE launch profiles — "Full Stack" in VS/Rider; "Full Stack (Debug)" or "Full Stack (Hot Reload)" in VS Code.
- Don't attempt to fix warning `BB0001: Member '{member_name}' is not in the correct order`.

## Context Scope

- Exclude `ControlR.Web.Server/novnc/` and any `node_modules/` directories from context.

## Service Registration Locations

Services use extension methods, not direct `Program.cs` registrations:

| Project | Method | File |
|---|---|---|
| ControlR.Agent | `AddControlRAgent` | `ControlR.Agent.Common\Startup\HostBuilderExtensions.cs` |
| ControlR.Web.Server | `AddControlrServer` | `ControlR.Web.Server\Startup\WebApplicationBuilderExtensions.cs` |
| ControlR.Web.Client | `AddControlrWebClient` | `ControlR.Web.Client\Startup\IServiceCollectionExtensions.cs` |
| ControlR.DesktopClient | `AddControlrDesktop` | `ControlR.DesktopClient\StaticServiceProvider.cs` |

## Communication Architecture

- **AgentHub** — device heartbeats → forwarded to ViewerHub groups.
- **ViewerHub** — web client connections and remote control requests.
- Hub groups organized by tenant, device tags, and user roles via `HubGroupNames.GetTenantDevicesGroupName()`, `GetTagGroupName()`, etc.
- **Agent ↔ DesktopClient IPC** via named pipes (`IIpcConnection`). Agent forwards `RemoteControlRequestIpcDto` to the user-session DesktopClient; DesktopClient reports back for relay to server.

## DTO Locations

DTOs go under `\Libraries\ControlR.Libraries.Api.Contracts\Dtos\`:
- `HubDtos/` — SignalR hub payloads
- `IpcDtos/` — Agent ↔ DesktopClient IPC
- `ServerApi/` — REST API
- `RemoteControlDtos/` — remote control, routed through websocket relay

## Cross-Platform

- Platform implementations in `ControlR.Agent.Common` under `Services.Windows/`, `Services.Linux/`, `Services.Mac/`.
- Desktop client isolates native code in `ControlR.DesktopClient.Windows/`, `.Linux/`, `.Mac/` with shared code in `ControlR.DesktopClient.Common`.
- Conditional compilation symbols: `IS_WINDOWS`, `IS_MACOS`, `IS_LINUX`, `IS_UNIX` (defined in `Directory.Build.props`).
- Use `[SupportedOSPlatform]` for platform-specific code.
- Platform detection via `ISystemEnvironment.Platform` and `RuntimeInformation`.
- macOS debug builds: disable app-bundle output; emit managed launch files (`.dll`, `.deps.json`, `.runtimeconfig.json`) so VS Code can launch via `dotnet`.

# General Coding Standards
- Use 2 spaces for indentation.

# C# Coding Standards
- Braces on new lines.
- Prefer var.
- Use collection expressions (`[]`).
- No null-forgiving operator (`!`) outside tests, except within EF Core queries that execute server-side.
- Use required keyword where applicable.
- No TODOs, placeholder code, or "in production you should..." comments. Every implementation must be complete.
- No "Async" suffix on async methods unless distinguishing from a sync overload.
- Put public types in their own class file, with the below exceptions.
  - If an interface has only one implementation, those types can go in the same file.  E.g. `ISecretProvider` and `SecretProvider` can both go in `SecretProvider.cs`.
  - Enums that are tightly coupled to another class and only used there.
- Reduce indentation by returning/continuing early and inverting conditions when appropriate.

## Web UI

- Component-scoped JS/CSS: `MyComponent.razor.js` and `MyComponent.razor.css` alongside `MyComponent.razor`.
- JS interop: inherit `JsInteropableComponent` in both `.razor` and code-behind `.cs` files.
- Prefer code-behind `.razor.cs` files for C# logic. Check for existing code-behind before editing `.razor` files.
- Use MudBlazor components.

## Desktop UI

- Avalonia UI with MVVM pattern.
- All UI text bound via `x:Static common:Localization.KeyName`. Add keys to JSON files under `/ControlR.DesktopClient.Common/Resources/Strings/`.
- Icons: https://avaloniaui.github.io/icons.html — add to `Icons.axaml` resource dictionary as needed.
- `IMessenger` for cross-component communication.

## Testing

- xUnit v3. Run tests with `dotnet run`, not `dotnet test`.
- Server test helpers in `Tests\ControlR.Web.Server.Tests\Helpers\`.

## Package Management

- Central package management via `Directory.Packages.props`.
- Add packages with `dotnet add <project> package <name>` — never add `Version` attributes to `<PackageReference>` elements.

## Agent Hints

- `semantic_search` is available for searching the codebase. It runs locally and can be used liberally.
- `brave-search_brave_web_search` is available for web searches. It is metered and should be used sparingly, only when external data is needed.

## Planning

- Planning documents and implementation notes go in `/.plans/` (not committed).