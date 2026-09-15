## Breaking Changes

- ⚠️ You will need to log out and back in if you have "Remember Me" enabled. ⚠️
  - A pre-existing auth cookie will lack the new permission claims.
- Some of the routes and DTOs used in the `/api/v1/*` endpoints have been changed.
  - There should be no more breaking changes to the `/api/v1/*` endpoints after this release.
- Although roles were migrated to permission presets, user tags that mapped users to devices were removed.
  - If you were using user tags to control access to devices, you will need to migrate to the new permissions system.

## Enhancements

- Added `Customers`, `Device Groups`, and `User Groups`.
- Added Tenant and Server service accounts, including API-credential issuance with configurable expiration.
  - Revoked and expired credentials can be deleted manually from the service accounts pages, and are
    permanently removed by a background service after `AppOptions:ServiceAccountCredentialCleanupAfterDays`
    days (default 30; 0 disables automatic deletion).
- Replaced roles with a granular permissions system.
  - You can now grant users and service accounts specific permissions, scoped to the whole tenant, a customer, a device group, or an individual device.
- Existing roles get migrated to permission presets, which are bundles of related permissions that can be applied at once.
- Added a Permissions page under Tenant Admin for managing who can do what.
- Added filters on the dashboard for customer and device group.
- Added any/all match mode for filtering by tags and device groups.
- Reworked how ungrouped/untagged device display is toggled.
- Authorization changes are now logged, with tenant and server views of the activity.
- Fine-grained permissions can now be applied to Personal Access Tokens.
- Personal access token management is now permission-gated (`personal-access-token.self.read` / `.self.write`, granted via the new "Self Service" preset that every user receives).
  - Tokens that inherit the owner's full permissions can only be created by a direct login, not by another token.
- Added an Effective Permissions page that shows exactly what a user or service account can do.
- Added `Customer` input to the deploy page, allowing for the device to get added to a specific customer during agent installation.
- Refactored `Deploy` page for better usability (back button, pre-populated expiration for time-based keys, grid sizing).
- Added `IControlrApiClientFactory` to the `ControlR.ApiClient` library. Register one factory and produce `IControlrApi` clients that target different ControlR servers, each with its own credentials.
  - Includes idle-target eviction, an optional tracked-target cap with least-recently-used eviction, and credential rotation via remove-and-recreate.
  - Existing `AddControlrApiClient` and `ControlrApiClientBuilder` usage is unchanged.
- `ControlR.ApiClient` can now authenticate as a service account. Set `ServiceAccountApiKey` and requests carry the credential in the `x-api-key` header, authenticating as the service account instead of as a user.
  - Available on `AddControlrApiClient`, `ControlrApiClientBuilder`, and each factory target. A personal access token or bearer token takes precedence when it is also configured.
- Added `ControlrApiClientBuilder.GetAuthSession()`, which exposes the interactive bearer session for the process-wide client.

## Fixes

- The `ControlR.ApiClient` background token-refresh no longer ends the session on transient failures.
- The `ControlR.ApiClient` interactive session no longer keeps reporting itself as signed in after the server rejects its refresh token during an ordinary API call.
- Disposing a `ControlR.ApiClient` interactive auth session now moves it to a new terminal `Disposed` state and raises `StateChanged`.
- Interactive sign-in in `ControlR.ApiClient` now clears a personal access token or service account key if one was already configured on the session.

## Removals

None.

## Internal

- `ControlR.ApiClient` now marks the internal installer-key and user logon-token methods
  `[Obsolete]`, each pointing at its `/api/v1` replacement and the difference the caller has to
  handle. The UI already uses V1 for both, so nothing in the product calls these anymore.