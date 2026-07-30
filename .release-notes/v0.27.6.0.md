## Breaking Changes

None.

## Enhancements

- Added an option for WebP encoding format for remote control sessions.
  - WebP provides better compression (lower bandwidth utilization) while still being better quality than JPEG.
  - However, WebP takes longer to encode, so FPS can be lower than JPEG, depending on screen activity.
- Added SIMD acceleration for image diffing, reducing diff time by up to 29%.
  - This can offset some of the additional encoding time of WebP in some scenarios.

## Fixes

- Fixed inserts to UserStorage failing due to upsert bypassing EF Core custom conventions configuration.
  - This was causing the dismissal of the "What's New" dialog to not "stick".
- Fixed an issue with the Markdown parser incorrectly rendering underscores as italics when flanked by non-whitespace characters.

## Removals

- Removed `ControlR.Web.Server/Data/Extensions/UpsertExtensions.cs`.

## Internal

- Added `BlazorDisableThrowNavigationException` to Blazor projects.
