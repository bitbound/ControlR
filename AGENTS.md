---
applyTo: '**'
---

# ControlR

Cross-platform remote access and control. .NET 10 backend (ASP.NET Core), Blazor WebAssembly frontend, Avalonia desktop apps.

## Build & Run

- Build: `dotnet build ControlR.slnx --verbosity quiet` (no output = success)
- Run: Use IDE launch profiles — "Full Stack" in VS/Rider; "Full Stack (Debug)" or "Full Stack (Hot Reload)" in VS Code.
- Fix `BB0001: Member '{member_name}' is not in the correct order` from the solution root:
  - `dotnet format analyzers --diagnostics "BB0001" --severity hidden`
  - Note: The command requires `--severity hidden` because BB0001 is emitted with a `hidden` severity by default.

## Context Scope

- Exclude `ControlR.Web.Server/novnc/` and any `node_modules/` directories from context.

## General Instructions

- Various web search tools are available. Use them when external data is needed, when you need info about specific libraries or APIs, etc.

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
| `Dtos/ServerApi/Internal/` | Internal (BFF) only | Dynamic, changes freely |
| `Dtos/ServerApi/V1/` | Versioned API contracts | Stable contract |

**Rules:**
- Every DTO belongs to exactly one route root's folder (`Internal/`, `V1/`, …). There is no shared root `Dtos/ServerApi/` folder for DTOs.

### Readonly Collections on DTOs

- ServerApi request/response DTO collection properties and record parameters must be **read-only collection types**: `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IEnumerable<T>`, or `ImmutableArray<T>`.
  - ✅ `IReadOnlyList<Guid> DeviceIds`, `ImmutableArray<Guid>? TagIds`
  - ❌ `List<Guid> DeviceIds`, `Guid[] DeviceIds`, `HashSet<Guid>` — never expose mutable or array collections on a DTO.
- Exception: **raw binary `byte[]` payloads** (image/video/stream data such as `JpegData`, `PacketData`, `Signature`) stay `byte[]`. Hub, IPC, and remote-control wire-format DTOs are out of scope for this rule.
- When a DTO collection changes, **propagate the read-only type downstream** (service/manager/EF query-extension method parameters) instead of converting with `.ToList()`/`.ToArray()` at the controller boundary — only convert where an out-of-scope target (e.g. a `string[]` hub contract) requires it.
- Use `.Count` (not `.Length`) when consuming `IReadOnlyList`/`IReadOnlyCollection` members.

## API Routing & Versioning

**Two route roots, two stability levels**:

| Root | URL prefix | Stability | Consumer |
|---|---|---|---|
| `Api/Internal` | `/api/*` | Unversioned, volatile | Internal. BFF (Blazor UI) |
| `Api/V1` | `/api/v1/*` | Stable contract | Endpoint-specific authorization; may accept users, PATs, server service accounts, or tenant service accounts |
| `Api/Agent` | `/api/agent/*`, | Unversioned, volatile | Internal. Public APIs for agent. |

- Controllers live in `Api/{Root}/` with namespace `ControlR.Web.Server.Api.{Root}`.
- Do not infer an endpoint's audience or resource scope from the `Api/V1` route root. V1 describes contract stability. Determine authorization and tenant/server scope from each controller's policy, resource authorization, explicit tenant checks, and manager method calls.
- V1 controllers may support user, PAT, server service-account, and tenant service-account principals when their endpoint authorization permits them. A V1 controller that uses `ForServer` methods is server-scoped because of that endpoint's explicit contract, not because it is V1.
- Controller class names carry **no** version or audience prefix. The namespace + `[ApiVersion]` attribute convey that.
  - ✅ `Api/V1/DevicesController.cs` — namespace `Api.V1`
  - ❌ `V1DevicesController.cs` — version noise in the class name
- Only add controllers to a new version when stakeholders request them. Don't pre-build.

### V1-first rule

- New endpoints go to `Api/V1` by default. Standard CRUD shape means ID-addressed resources, required `tenantId` (query param, or path segment for tenant service accounts) on collection/create operations, and plain envelopes.
- An `Api/Internal` endpoint exists only because its shape is irregular (streaming, live-session, interactive, auth/session flow, pre-auth probe, agent negotiation). It must name that constraint in `InternalV1ParityGuardrailTests.IrregularShapeAllowList`.
- The guardrail test fails when an Internal operation is neither deprecated, V1-twinned (same verb + path template under `/api/v1`), nor allow-listed. Migration packages prune their entries as twins land.

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