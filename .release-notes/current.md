## Breaking Changes

- `ControlR.ApiClient` no longer exposes its bearer refresh internals: `IBearerTokenRefresher`, `BearerTokenRefresher`, `BearerTokenRefreshResult`, `AuthState`, `BearerRefreshContext`, the bearer setters and `BearerRefreshLock` on `ControlrApiClientAuthState`, and the public `ControlrAuthSession` constructor. Use `IControlrAuthSession` for sign-in, restore, and sign-out.

## Enhancements

None.

## Fixes

- Fixed an issue in `ControlR.ApiClient` where interactive sign-in could spam token refresh requests.
  - Tokens now renew only when a request needs them, and a burst of rejected requests shares one renewal.

## Removals

None.

## Internal

None.
