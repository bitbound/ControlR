## Breaking Changes

- `ControlR.ApiClient` no longer exposes its bearer refresh internals: `IBearerTokenRefresher`, `BearerTokenRefresher`, `BearerTokenRefreshResult`, `AuthState`, `BearerRefreshContext`, the bearer setters and `BearerRefreshLock` on `ControlrApiClientAuthState`, and the public `ControlrAuthSession` constructor. Use `IControlrAuthSession` for sign-in, restore, and sign-out.
- The public `ControlrApi` constructor no longer takes an `IBearerTokenRefresher`. Build the client through `AddControlrApiClient`, `ControlrApiClientBuilder`, or `ControlrApiClientFactory`, which wire the bearer session themselves.

## Enhancements

None.

## Fixes

- Fixed an issue in `ControlR.ApiClient` where a token shorter than the client's refresh lead time was renewed before every call.

## Removals

None.

## Internal

None.
