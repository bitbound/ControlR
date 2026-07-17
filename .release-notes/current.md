## Breaking Changes

- If you're using the `ControlrApi` client, the entry point has been split into three top-level properties: `ControlrApi.Internal`, `ControlrApi.V0`, and `ControlrApi.Agent`.
  - You'll need to update your code to use the new static properties that serve as the new entry points.
  - Existing endpoints were moved under the `Internal` property.  These will continue to evolve dynamically based on the needs of the UI.
  - The `V0` property is the new versioned API that should be used for server-to-server integrations.

- Likewise, the REST API has been split into three top-level routes: `/api/internal/`, `/api/v0/`, and `/api/agent/`.
  - The previous `/api/` route is now `/api/internal/` and will continue to evolve dynamically based on the needs of the UI.
  - The server will continue listening on the `/api/` route for several releases for backward compatibility.
  - The new `/api/v0/` route is the new versioned API that should be used for server-to-server integrations.
  - The `/api/agent/` route will contain only endpoints used by the agent.
    - This makes it easier to protect the server with services like Cloudflare Access, while white-listing the agent endpoints.

## Added

- Added this **What's New** dialog that shows when starting a new version for the first time.
- Devices now set their device ID as the `host.id` attribute for OpenTelemetry (if configured).
  - This makes it easier to find device-specific metrics, logs, and traces in your OTEL backend.
- Replaced PrismJS with Monaco Editor on Remote Logs page.
  - Monaco will also be used in the upcoming scripting feature.
- Added the `AppOptions__DisableFirstUserSelfRegistration` configuration option (default `false`) to control whether the first user can self-register.
  - By default, the first user can self-register and is automatically promoted to server administrator and first-tenant admin.
  - This is useful for server-to-server integrations where ControlR is driven primarily via API.
- Added the `AppOptions__DisableDesktopPreview` configuration option (default `false`) to hide the Desktop Preview button on the Remote Control page and reject requests to the desktop preview endpoint.
  - The button is still visible by default. Set this to `true` to disable the feature entirely.

## Fixed

- Fixed a delay in the remote control UI when websocket stream is closing.

## Changed

- Created guards in the agent auto-update and repair processes to prevent an agent with a different brand or publisher name from bricking the install.

## Removed

None.

## Internal

- Added UserStorage key/value user-related data (e.g. "acknowledged version 0.25.50.0 updates").