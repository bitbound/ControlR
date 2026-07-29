## Breaking Changes

None.

## Enhancements

None.

## Fixes

- Fixed inserts to UserStorage failing due to upsert bypassing EF Core custom conventions configuration.

## Removals

- Removed `ControlR.Web.Server/Data/Extensions/UpsertExtensions.cs`.

## Internal

- Added `BlazorDisableThrowNavigationException` to Blazor projects.
