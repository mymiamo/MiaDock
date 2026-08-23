# MiaDock 1.6.0.0 release notes

* Added central DockMetrics for Compact, Default, Large and safely bounded Custom dock thicknesses, including consistent compact, hover, expanded and notification metrics.
* Added live appearance controls for density, content icon scale, corner style and top offset, plus Minimal, Dynamic, Large and Custom appearance presets.
* Settings are schema-migrated from 1.5.x without replacing the existing configuration contract; legacy edge margin becomes the new top offset.
* Motion preferences now independently control event morphs and decorative icon/shell emphasis. The existing Windows animation-preference service remains the accessibility authority.
* Media, privacy, Device Hub and Clipboard Peek updates share the existing generation- and cancellation-safe island transition pipeline, preventing stale visual state during rapid events.
* This release intentionally does not add a new module, a settings-framework migration, or a Store upload. A locally generated package remains a candidate until device, WACK and flight gates are supplied by the project owner.
