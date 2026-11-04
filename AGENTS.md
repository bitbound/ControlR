---
applyTo: '**'
---

# ControlR

Cross-platform remote access and control. .NET 10 backend (ASP.NET Core), Blazor WebAssembly frontend, Avalonia desktop apps.

Detailed conventions (C# standards, API surface and DTOs, permissions, hub contracts, outbound URLs, platform layout, web/desktop UI, test running, packaging) live in path-gated rule files that load automatically when a touched path matches. Keep this file to repository-wide facts only. New detail belongs in a gated file, not here. Target under 6,000 characters.

## Build & Run

- Build: `dotnet build ControlR.slnx --verbosity quiet` (no output = success)
- Run: Use IDE launch profiles — "Full Stack" in VS/Rider; "Full Stack (Debug)" or "Full Stack (Hot Reload)" in VS Code.

# Running Tests
- When using xUnit v3 or higher, run tests with `dotnet run`, not `dotnet test`.
  - Build the test project(s) before running tests: `dotnet build {project_path} --verbosity quiet`
  - Whole test project: `dotnet run --project {project_path}`
  - Specific class/method: `dotnet run --project {project_path} -- -filter /{assembly-name}/{namespace}/{class}/{method}`
  - **IMPORTANT**: xUnit v3 filter paths use assembly name, then C# namespace, then class name, then method name.
    - The namespace may look like a repeat of the assembly name, but it's distinct and required.
    - Example: for class `IdentityApiRegisterFilterTests` in namespace `ControlR.Web.Server.Tests`, project `ControlR.Web.Server.Tests`:
      `-filter /ControlR.Web.Server.Tests/ControlR.Web.Server.Tests/IdentityApiRegisterFilterTests`
    - For a specific method, append `/{method}`.
- If the test filter doesn't find the test, rebuild the solution and try again.

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