# ControlR.Libraries.Branding

This library contains shared branding constants for various ControlR projects.  It is used during the build process to create customized ControlR instances.

It is not intended for public consumption. 

## Customization config

Branded builds fetch a JSON customization payload from the portal. `Invoke-Customize.ps1` reads it by property name rather than through a shared type, so the serialized names are the contract.

The emitting DTOs live in SubscriberPortal (`SubscriberPortal.Server.Features.Builds`), which is the only project that serializes them. When adding or renaming a payload property, update both sides together:

- `SubscriberPortal.Server/Features/Builds/CustomizationConfigDto.cs` defines the payload.
- `.scripts/Invoke-Customize.ps1` consumes it. Use the `PSObject.Properties` guard for any new optional property so payloads predating it do not trip `Set-StrictMode`.
- SubscriberPortal's `CustomizationConfigDtoTests` pins the serialized key names, and ControlR's `Test-CustomizeServerUrl.ps1` proves the script picks them up.
