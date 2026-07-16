## Breaking Changes

- If you're using the `ControlrApi` client, the entry point has been split into a few smaller, purpose-specific classes.
  - You'll need to update your code to use the new static properties that serve as the new entry points.
  - Existing endpoints were moved under the `Internal` property.  These will continue to evolve dynamically based on the needs of the UI.
  - The `V0` property is the new versioned API that should be used for server-to-server integrations.
  - `V0` isn't complete and will be fleshed out over time.

## Changed

- Replaced the `AppOptions__EnableFirstUserSelfRegistration` configuration option (default `true`) with `AppOptions__DisableFirstUserSelfRegistration` (default `false`).
  - Existing installations that don't set the new key retain the previous behavior (first-user self-registration enabled by default, first user auto-promoted to server administrator).
  - Set `AppOptions__DisableFirstUserSelfRegistration` to `true` to opt out of the first-user self-registration bootstrap. This is useful for server-to-server integrations where ControlR is driven primarily via API.

## Added

- Added this **What's New** dialog that shows when starting a new version for the first time.
- Devices now set their device ID as the `host.id` attribute for OpenTelemetry (if configured).
  - This makes it easier to find device-specific metrics, logs, and traces in your OTEL backend.
- Replaced PrismJS with Monaco Editor on Remote Logs page.
  - Monaco will also be used in the upcoming scripting feature.

## Fixed

None.

## Changed

None.

## Removed

None.

## Internal

- Added UserStorage key/value user-related data (e.g. "acknowledged version 0.25.50.0 updates").