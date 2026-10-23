# Dock position drag fix — 2026-10-05

## Change

The move icon opened with the other editing controls, but dragging was reported
not to work. The previous input path observed handled Button pointer events,
shared that Button's capture lifecycle and reconstructed the screen coordinate
from the moving HWND plus a control-relative point. This investigation identifies
risks in that path; the exact original failure was not reproduced in live input.

The move handle now uses a WinUI Thumb with the same 32-DIP appearance and icon.
DragStarted, DragDelta and DragCompleted own the gesture. Native GetCursorPos
and GetDpiForWindow are read through IOverlayWindowController; the starting
screen coordinate and DPI scale remain fixed for each drag. Moving the HWND
does not become part of the next pointer delta. Failed pointer reads are ignored
instead of replacing the coordinate with zero.

Release/capture loss ends dragging while keeping editing open. Reset ends any
active drag. Saving, cancelling, resetting, Tab focus and 10-DIP arrow-key
movement retain their existing behavior. The shell treats Thumb as an interactive
source. Existing settings schema 34 and monitor-specific offsets are retained.

## Automated validation

- Release x64 unpackaged MSBuild: exit code 0, no warnings/errors reported.
- Core: 419 passed (including 11 dock-position cases).
- Windows: 246 passed, 1 existing environment-dependent skip.
- WinUI: 210 passed. Total: 875 passed, 1 skipped, 0 failed.
- Regressions cover screen-coordinate movement without feedback, direction
  reversal and repeat drags; release/capture-loss session semantics; bad reads;
  reset/save/cancel; all anchors at 96/144/192 DPI on a negative-origin monitor;
  a new settings-store instance preserving global and monitor-profile offsets;
  Thumb event wiring, keyboard access and system focus visuals.

Logs and TRX results are in ignored artifacts/build-position-drag.log,
artifacts/tests-position-drag-*.log and artifacts/test-results/position-drag-*.trx.

## Live acceptance still pending

The previous development process was closed through its settings dialog and the
new Release executable was launched with --settings. Computer Use was then
stopped by the user's physical Escape key; no further UI input was issued.

Consequently actual dragging, physical DPI changes, reopening the dock,
application-restart persistence and screen-reader/high-contrast interaction are
NOT accepted by this report. Automated coordinate and persistence tests do not
replace those checks. The new development build remains available for testing.

Remaining procedure: pencil -> drag right and left -> release outside the handle
-> drag again -> save; reopen/restart and verify position. Check reset -> save,
move -> Escape, Tab/arrow keys, monitor edges and actual 100%/150%/200% scaling.
Do not describe live acceptance as complete until these have been observed.
