# Phase Plan — 1.4.0 Dock Community Toolkit Animations

## Scope

- Add `CommunityToolkit.WinUI.Animations` for dock content motion.
- Keep island bounds on `IslandBoundsAnimator` + Composition metrics.
- Settings `SettingsControls` migration is out of scope.

## Package

- `CommunityToolkit.WinUI.Animations` **8.2.251219**
- Verified restore/build against `Microsoft.WindowsAppSDK` **2.3.1** / `net10.0-windows10.0.26100.0` (uses package TFM `net9.0-windows10.0.19041`).

## Architecture

| Concern | Owner |
|---------|--------|
| Width / height / corner radii | `IslandBoundsAnimator` |
| Opacity / scale / translation | `ToolkitAnimationFactory` (`AnimationBuilder`) |
| Orchestration / cancel / reduced motion | `IslandAnimationCoordinator` |

## Validation checklist

- [x] Motion presets Off / Minimal / Balanced / Fluid / Springy / Dynamic
- [x] Rapid expand / collapse spam cancels cleanly
- [x] Module switch slide + fade
- [x] Content refresh pulse
- [x] Windows “Animations enabled” off → instant layout

Automated: `PerformanceGuardTests` 7/7 passed (Release x64). Unpackaged Release app build succeeded with `CommunityToolkit.WinUI.Animations` 8.2.251219.