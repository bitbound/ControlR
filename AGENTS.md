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

## General Instructions

- `semantic_search` is available for searching the codebase. It's local and free. Use it for codebase searches and questions.
- `brave-search_brave_web_search` is available for web searches. Use when external data is needed.

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

### Route Root DTO Convention

DTOs live in `Dtos/ServerApi/` under `ControlR.Libraries.Api.Contracts.Dtos.ServerApi`:

| Location | Contents | Lifecycle |
|---|---|---|
| `Dtos/ServerApi/` (root) | DTOs shared across route roots | Changing affects both Internal and V0 |
| `Dtos/ServerApi/Internal/` | Internal (BFF) only | Dynamic, changes freely |
| `Dtos/ServerApi/V0/` | V0 (S2S) only | Stable contract |
| `Dtos/ServerApi/V1/` | V1 (S2S, future) | Stable contract |

**Rules:**
- If a DTO is used by **only one** route root → place it in that root's folder (`Internal/`, `V0/`, …).
- If a DTO is used by **more than one** route root → keep it in the root `Dtos/ServerApi/` folder. If the contract needs to diverge between versions, duplicate it into each version folder and remove the shared version.
- When sun-setting a version, its DTO folder can be deleted independently.

## API Routing & Versioning

**Three route roots, three stability levels** — never cross them:

| Root | URL prefix | Policy | Stability | Consumer |
|---|---|---|---|---|
| `Internal` | `/api/internal/*` | `RequireUserPrincipalPolicy` | Unversioned, volatile | BFF (Blazor UI) |
| `V0` | `/api/v0/*` | `RequireServerServiceAccountPolicy` | Stable contract | S2S automation |
| `Legacy` | `/api/devices`, `/api/agent-update` | — | Frozen, no new endpoints | Being phased out |

- Controllers live in `Api/{Root}/` with namespace `ControlR.Web.Server.Api.{Root}`.
- Controller class names carry **no** version or audience prefix. The namespace + `[ApiVersion]` attribute convey that.
  - ✅ `Api/V0/DevicesController.cs` — namespace `Api.V0`
  - ❌ `V0DevicesController.cs` — version noise in the class name
- Only add controllers to a new version when stakeholders request them. Don't pre-build.

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
- Braces go on new lines.
- Prefix private fields (including static) with `_` and use camelCase. E.g. `private readonly IFileSystem _fileSystem;`
- Constants: `PascalCase` with `const` modifier. E.g. `private const int MaxRetries = 5;`
- Prefer var over explicit types.
  - Example: `var directories = _fileSystem.GetDirectories(path);`
- Use collection expressions (`[]`).
  - Example: `private readonly Dictionary<string, uint> _displayNodeIds = [];`
- No null-forgiving operator (`!`) outside tests, except the following scenarios:
  - In tests, where a null value would result in a test failure anyway.
  - Within EF Core queries that execute server-side.
  - Blazor framework-injected properties ([SupplyParameterFromForm], [CascadingParameter]) that cannot have a property initializer.
- Null-forgiving examples:
  - DON'T: `var result = myObject!.GetValue();`
  - DO: `var result = myObject?.GetValue() ?? throw new InvalidOperationException("myObject is not initialized.");`
  - OK: `var properties = await _dbContext.Users.Select(x => x.SomeNavigation!.SomeProperty).ToListAsync();`
  - OK: `[CascadingParameter] private HttpContext HttpContext { get; set; } = default!;`
- Use `required` keyword where applicable.
- Use `using` statements for `IDisposable` resources, and `await using` for `IAsyncDisposable`.
  - Example: `using var stream = new FileStream(path, FileMode.Open);`
  - Example: `await using var connection = new DatabaseConnection();`
- No TODOs, placeholder code, or "in production you should..." comments. Every implementation must be complete.
- Don't add "Async" suffix on async methods unless distinguishing from a sync overload.
  - Example: `public async Task Connect()` if there's no `public void Connect()`.
  - Example: If `public void Connect()` exists, then `public async Task ConnectAsync()`.
- Put public types in their own class file, with the below exceptions.
  - If an interface has only one implementation, those types can go in the same file.  E.g. `ISecretProvider` and `SecretProvider` can both go in `SecretProvider.cs`.
  - Enums that are tightly coupled to another class and only used there.
- Reduce indentation by returning/continuing early and inverting conditions when appropriate.
- Constructor parameter order: put concrete classes/implementations before interfaces.

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
  - Build the test project(s) before running tests: `dotnet build {project_path} --verbosity quiet`
  - Whole test project: `dotnet run --project {project_path}`
  - Specific class/method: `dotnet run --project {project_path} -- -filter /{assembly-name}/{namespace}/{class}/{method}`
  - **IMPORTANT**: xUnit v3 filter paths use assembly name + C# namespace, which may look similar but are distinct. The assembly name is the project name.
    - Example: for class `IdentityApiRegisterFilterTests` in namespace `ControlR.Web.Server.Tests`, project `ControlR.Web.Server.Tests`:
      `-filter /ControlR.Web.Server.Tests/ControlR.Web.Server.Tests/IdentityApiRegisterFilterTests`
    - For a specific method, append `/{method}`.
- If the test filter doesn't find the test, rebuild the solution and try again.

## Package Management

- Use central package management via `Directory.Packages.props`.
- Don't add `Version` attributes to `<PackageReference>` elements in csproj files. `Version` attributes go in `Directory.Packages.props`.
- Use `dotnet add <project> package <name>` to add packages.

## Planning

- Planning documents and implementation notes go in `/.plans/` (not committed).