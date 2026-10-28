## Breaking Changes

- The `ControlR.ApiClient` package no longer exposes the internals used to sign in and keep sign-in tokens alive. Custom integrations or automation built directly against the old sign-in types must be updated to use the sign-in session interface.
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
- Remote control commands moved off the relay onto `AgentHub`, with a new session API, a per-device permission cache, a connection-scoped device connection registry, and end-to-end tracing of the command plane. Old viewer hub methods are preserved for backward compatibility.
- Remote control commands are now authorized per command type in `ViewerHub.RemoteControl`: clipboard text maps to `device.clipboard.write`, clipboard request to `device.clipboard.read`, block-input to `device.remote-control.block-input`, privacy-screen to the new `device.remote-control.privacy-screen`. The check is cached per device for the connection. Presets grant the new permission; custom roles do not get it automatically.
- Selecting the VPX encoder on the desktop client now fails fast. No viewer decodes `VideoStreamPacket` frames, so the option only produced unsupported-DTO warnings.
- CI: per-platform build steps extracted into reusable composite actions, published release tags sync to the private mirror repo, and signing secrets were renamed to the `BBB_` scoped scheme.
