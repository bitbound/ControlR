## Breaking Changes

- In a traditional, interactive server deployment, the first registered user automatically becomes a server administrator.
  - This behavior now requires explicit opt-in via the `AppOptions__EnableFirstUserBootstrap` configuration option.

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