## Breaking Changes

- If you're using the `ControlrApi` client, the entry point has been split into three top-level properties: `ControlrApi.Internal`, `ControlrApi.V1`, and `ControlrApi.Agent`.
  - You'll need to update your code to use the new static properties that serve as the new entry points.
- The endpoints under `ControlrApi.V1` on the client map to the new `/api/v1/` route on the server.
  - This is the new versioned, stable API intended for server-to-server integrations.
  - It's not yet fully implemented and will be completed over time.
- The endpoints under `ControlrApi.Agent` on the client map to the new `/api/agent/` route on the server.
  - Agents will only call endpoints under the `/api/agent/` route.
  - This makes it easier to protect the server with services like Cloudflare Access, while white-listing the agent endpoints.
- The endpoints under `ControlrApi.Internal` are the pre-existing endpoints and will remain under the root `/api/` route.

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