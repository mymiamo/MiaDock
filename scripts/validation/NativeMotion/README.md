# Native motion regression fixture

Run from the repository root with PowerShell:

```powershell
./scripts/validation/Invoke-MiaDockNativeMotion.ps1 -Baseline -Measure
./scripts/validation/Invoke-MiaDockNativeMotion.ps1 -Measure
```

Each fixture creates a separate real WinUI HWND with deterministic fake module
content, actual Ionicons, the production animation coordinator and the Windows
overlay controller. It does not connect to a running MiaDock instance or read or
write user settings. It closes its own window and process after writing JSON.

The first 1,000 actions alternate hover, expansion, module direction/height,
notification entry/return, content refresh and collapse. Assertions check final
geometry, visible state, opacity, reduced motion, detached preference events,
worker-thread cancellation ownership, reentrant retargeting and stale toolkit
completion. Repeated identical layout and radius-only updates must cause zero
native positioning calls; an actual bounds change must cause exactly one.

`-Measure` adds three actual 60-second sessions. In the candidate, the production
diagnostics monitor also records completed/cancelled transitions and automatically
detaches at the deadline. Bounds/content animation durations are the defaults.

Baseline mode links the eight animation source files from `2616398`; shared
Core/UI/Windows libraries and the fixture remain current so input and native host
are identical. This is an **animation-source comparison**, not a full 1.6.5 app
versus 1.7.0 app comparison. The baseline cannot report transition telemetry added
in 1.7.0. Setup timings include a one-second warmup and are not app startup times.

These are UI Rendering callback intervals, not GPU presentation FPS. A fixture
cannot prove visual quality, all 15 real module behaviors, physical DPI changes,
keyboard/screen reader access, real sleep/unplug/game handling, or whole-app
30-minute/8-hour stability. Those checks remain separate acceptance gates.
