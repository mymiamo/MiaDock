# MiaDock 1.4.1.0 release notes

## Stability fixes

- Prevented Core Audio and Windows media callbacks from propagating exceptions across native callback boundaries. This fixes the reported case where using hardware brightness keys while a media app is active could close MiaDock.
- Kept media, audio-meter, and system-activity consumers isolated so a late callback during a device or provider transition cannot terminate the application.
- Added periodic removable-drive reconciliation as a safe fallback when Windows does not deliver a USB volume broadcast to the message-only monitor.

## Diagnostics and updates

- Store update checks now trust Microsoft Store's update-package signal even if cached package metadata reports the installed version, and record the installed version, every returned version, and the selected version in the technical log.
- The Microsoft Store API itself can return a cached result for up to 30 minutes and newly certified packages can take longer to propagate; MiaDock preserves that platform limit instead of reporting a false failure.

## Versioning

- Package, assembly, manifest, release, and validation versions are `1.4.1.0`.
