## Breaking Changes

None.

## Enhancements

None.

## Fixes

- Fixed inserts to UserStorage failing due to upsert bypassing EF Core custom conventions configuration.
- Fixed an issue with the Markdown parser incorrectly rendering underscores as italics when flanked by non-whitespace characters.

## Removals

- Removed `ControlR.Web.Server/Data/Extensions/UpsertExtensions.cs`.

## Internal

- Added `BlazorDisableThrowNavigationException` to Blazor projects.
