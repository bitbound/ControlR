## Breaking Changes

None.

## Enhancements

- DesktopClient now persists user-specific settings and falls back to defaults if the settings file cannot be read.
- DesktopClient now supports language selection.
- The Linux desktop client can now run on Wayland-only sessions.
- Device groups and user groups can now be renamed or re-described from their list pages.
- An installed agent can now move to a build with a different product name without re-enrolling the device.
- System messages in device chat now appear as muted inline text.
- Admin sections in the nav menu can now be collapsed and expanded.

## Fixes

- Remote control sessions now stop trying after two minutes when the other device never connects.
- Remote control connections now use the server's public URL, so they work when the viewer is on a local network or behind a proxy.
- Improved detection of graphical sessions vs headless sessions on Linux.
- Timestamps in admin grids and the file browser now appear in the viewer's local time.
- Fixed dismissal of the What's New dialog sometimes not persisting.
- Fixed Linux agent updates sometimes failing to start the new service, and a failed install now leaves the previous agent running.
- A Linux uninstall without root access now stops instead failing on the next step.

## Removals

None.

## Internal

- Test runs now flag a libman browser library that is behind its latest release.
