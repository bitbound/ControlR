## Breaking Changes

- Agent, desktop client, and Assist files now live under a publisher directory, for example `C:\Program Files\Bitbound\ControlR`. No action is needed, because an existing install moves itself during its next automatic update. Old logs stay where they are.

## Enhancements

- In fullscreen, the web viewer now captures OS-level shortcuts such as Win, Win+R, and Alt+Tab and sends them to the remote device. This uses the Keyboard Lock API, which only Chromium-based browsers support. Hold Esc to exit fullscreen.
- Added a "Send Keys" section to the Input menu with common shortcuts for the remote platform, such as Win, Win+R, Alt+Tab, and Ctrl+Shift+Esc on Windows.

## Fixes

None.

## Removals

None.

## Internal

None.
