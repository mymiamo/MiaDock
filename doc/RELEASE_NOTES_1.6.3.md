# MiaDock 1.6.3.0 release notes

* Redesigns the expanded Time module with native Timer/Stopwatch tabs.
* Adds a real 224 px circular timer ring driven by persisted timer state and exposed to accessibility tools with a live progress value.
* Changes 5, 10, 15, 25, and 45 minute presets to populate the bounded duration editors before the user starts the timer.
* Disables setup while a timer is active, prevents zero-duration starts, safely normalizes invalid numeric input, and clears preset selection after manual edits.
* Modernizes stopwatch controls and laps without changing stopwatch, persistence, alarm, compact, hover, or notification behavior.
* Adapts from a 45/55 two-column layout to a vertical scrollable layout at narrow widths using theme-aware Fluent resources.
* Keeps common expanded header ownership in `ExpandedModuleHost`, prevents localized quick-duration labels from clipping, and preserves the host's bottom-navigation safe area.
* Redesigns Clipboard Peek around a type-aware current-item hero, responsive native command bar, compact color-format rows, and semantic recent-copy visuals.
* Aligns compact and notification Clipboard Peek surfaces with the expanded design and existing reduced-motion-aware CommunityToolkit micro-interactions.
* Preserves privacy by keeping sensitive values out of tooltips and automation names, and uses generation checks to discard stale asynchronous thumbnail results.
