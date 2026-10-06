---
applyTo: '**'
---

# ControlR

Cross-platform remote access and control. .NET 10 backend (ASP.NET Core), Blazor WebAssembly frontend, Avalonia desktop apps.

Detailed conventions (C# standards, API surface and DTOs, permissions, hub contracts, outbound URLs, platform layout, web/desktop UI, test running, packaging) live in path-gated rule files that load automatically when a touched path matches. Keep this file to repository-wide facts only. New detail belongs in a gated file, not here; target under 6,000 characters.

## Build & Run

- Build: `dotnet build ControlR.slnx --verbosity quiet` (no output = success)
- Run: Use IDE launch profiles — "Full Stack" in VS/Rider; "Full Stack (Debug)" or "Full Stack (Hot Reload)" in VS Code.

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

## Tenant Isolation (EF Query Filters)

- `AppDb` applies **claims-driven global query filters**: `UseUserClaims(user)` (`Data/Configuration/DbContextOptionsBuilderExtensions.cs`) reads the authenticated principal's tenant/user claims when the context is created. When a tenant claim is present, most tenant-owned entities (`Users`, `Devices`, `UserGroups`, `DeviceGroups`, `Customers`, `Tags`, etc.) are filtered automatically, including via `[FromServices] AppDb` in controllers.
- **Server principals** (no tenant claim, e.g. server service accounts) get an **unfiltered context by explicit contract**. That is what enables their cross-tenant access.
- **Exempt by design, NO query filters:** `PermissionAssignment`, `ServiceAccount`, `AuthorizationChangeLog`. Tenant isolation for these is enforced in service code. Never assume a filter exists for them.
- For security-critical boundaries, don't rely solely on the implicit filters. Explicit `TenantId == tenantId` predicates are encouraged. They survive unfiltered contexts and make the boundary self-documenting.

## General Coding Standards

- Use 2 spaces for indentation.

## Planning

- Planning documents and implementation notes go in `/.plans/` (not committed).
