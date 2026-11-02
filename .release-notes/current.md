## Breaking Changes

- The `ControlR.ApiClient` package no longer exposes the internals used to sign in and keep sign-in tokens alive. Custom integrations or automation built directly against the old sign-in types must be updated to use the sign-in session interface.
  - See `Internal` notes for details.
- The remote control message types in the shared contracts packages were renamed. Custom integrations or automation compiled directly against the contracts or relay client packages must be updated to the new type names. Remote control wire compatibility is unaffected.
  - See `Internal` notes for details.

## Enhancements

- Implemented the remaining remote control permission gates (privacy screen, clipboard transfer, input blocking).
- Remote control commands are now delivered only to the viewer that started the session.
- Custom branding now supports SVG files for the icon and logo.

## Fixes

- Fixed `ControlR.ApiClient` renewing the sign-in token before every request when the token's lifetime was shorter than the configured refresh lead time.
- Remote control commands now travel over the device's persistent connection.
- Improved the reliability of the remote control client at the Linux X11 login screen.
- Saving a duplicate permission assignment is now rejected with a clear "already exists" error.

## Removals

None.

## Internal

- `ControlR.ApiClient` bearer auth rework. `IControlrAuthSession` is the only sign-in surface. `IBearerTokenRefresher`, `BearerTokenRefresher`, `BearerTokenRefreshResult`, `AuthState`, `BearerRefreshContext`, the bearer setters and `BearerRefreshLock` on `ControlrApiClientAuthState`, and the public `ControlrAuthSession` constructor are gone. The public `ControlrApi` constructor no longer takes an `IBearerTokenRefresher`; build clients through `AddControlrApiClient`, `ControlrApiClientBuilder`, or `ControlrApiClientFactory`.
- Bearer token renewal is now on-demand and keyed to the exact token the failed request carried, replacing the background refresh loop. A burst of 401s shares one refresh and each call retries once on the renewed token. Renewals are paced against the lifetime the server granted.
- Remote control commands travel only over the device's WebSocket relay session. The hub-routed command transit and per-command hub authorization were removed. Old viewer session request hub methods are preserved for backward compatibility.
- Remote control grants (interact, block-input, privacy-screen, clipboard read, clipboard write) are evaluated with the device access permissions and returned to the viewer in `DeviceAccessPermissionsDto`, which hides the controls a viewer is not granted. The server-side preset roles include the new permissions. Custom roles do not get them automatically. The per-command map in `RemoteControlCommandPermissions` has no product reader yet.
- `ControlR.Libraries.Api.Contracts` exposes one `RemoteControlDtoType`/`RemoteControlDtoWrapper` pair for remote control messages, replacing the released `DtoType`/`DtoWrapper`. The unreleased per-direction split enums and wrappers are gone. Tag values and the serialized envelope are unchanged. Mixed client, agent, and relay versions keep interoperating on the wire. The relay client package's send and message-handler signatures use the same wrapper.
- Selecting the VPX encoder on the desktop client now fails fast. No viewer decodes `VideoStreamPacket` frames, so the option only produced unsupported-DTO warnings.
- CI: per-platform build steps extracted into reusable composite actions, published release tags sync to the private mirror repo, and signing secrets were renamed to the `BBB_` scoped scheme.
