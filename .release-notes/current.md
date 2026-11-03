## Breaking Changes

- Agent, desktop client, and Assist files now live under a publisher directory, `Bitbound\ControlR` rather
  than `ControlR`, so `C:\Program Files\Bitbound\ControlR`, `C:\ProgramData\Bitbound\ControlR\{instance}`,
  `/usr/local/bin/Bitbound/ControlR`, `/etc/bitbound/controlr/{instance}`, and
  `/Library/Application Support/Bitbound/ControlR`. An existing install moves itself onto the new paths
  during its next automatic update, keeping its device identity and service registration, so no manual
  redeploy is needed. Log history and settings under the old directories are left in place.

## Enhancements

None.

## Fixes

None.

## Removals

None.

## Internal

None.
