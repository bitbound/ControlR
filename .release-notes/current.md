## Breaking Changes

- ⚠️ You will need to log out and back in if you have "Remember Me" enabled. ⚠️
  - A pre-existing auth cookie will lack the new permission claims.
- Some of the routes and DTOs used in the `/api/v1/*` endpoints have been changed.
  - There will be no more breaking changes to the `/api/v1/*` endpoints after this release.
- Although roles were migrated to permission presets, user tags that mapped users to devices were removed.
  - If you were using user tags to control access to devices, you will need to migrate to the new permissions system.

## Enhancements

- Added `Customers`, `Device Groups`, and `User Groups`.
- Added Tenant and Server service accounts, including API-credential issuance with configurable expiration.
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
- Added `IControlrApiClientFactory` to the `ControlR.ApiClient` library: register one factory and produce `IControlrApi` clients that target different ControlR servers, each with its own server-scoped service-account credentials. Includes idle-target eviction, an optional tracked-target cap with least-recently-used eviction, and credential rotation via remove-and-recreate. Idle eviction leaves a target alone while it holds a live interactive session, so a signed-in target is not swept just because it is quiet. A live login pins its target until the login dies or it is removed, and the tracked-target cap can still evict one, since a hard bound has to be able to evict something. Removing or evicting a target never cancels a call that is already running: its HTTP stack is released once those calls finish, and a call made through a stale reference returns a failed result naming the disposed target instead of one shaped like a server fault. `MaxTrackedClients` must be greater than zero when set, or `null` for no limit. Server-side only; existing `AddControlrApiClient` and `ControlrApiClientBuilder` usage is unchanged.
- `ControlR.ApiClient` can now authenticate as a service account. Set `ServiceAccountApiKey` and requests carry the credential in the `x-api-key` header, authenticating as the service account instead of as a user.
  - Available on `AddControlrApiClient`, `ControlrApiClientBuilder`, and each factory target. A personal access token or bearer token takes precedence when it is also configured.
- Added `ControlrApiClientBuilder.GetAuthSession()`, which exposes the interactive bearer session for the process-wide client.

## Fixes

- The `ControlR.ApiClient` background token-refresh no longer ends the session on transient failures. Previously a single network hiccup or server error during a background refresh wiped the bearer and refresh tokens, forcing a full re-login (including 2FA). Now only a server-rejected refresh token expires the session. Transient errors are retried with backoff.
- The `ControlR.ApiClient` interactive session no longer keeps reporting itself as signed in after the server rejects its refresh token during an ordinary API call. The tokens were cleared but the session state was not, so it stayed `Authenticated` with no tokens, raised no state change, and its background refresh loop exited silently. Every later call then failed as unauthorized while the session still looked healthy.

## Removals

None.

## Internal

None.