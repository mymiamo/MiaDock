# Music Compact View Design QA

- Source visual truth: conversation attachment showing playing, paused, and next-track frames (no filesystem path available)
- Source dimensions: 788 × 544 px; reference pill approximately 746 × 124 px
- Implementation screenshots:
  - `artifacts/design-qa/music-compact-playing.png`
  - `artifacts/design-qa/music-compact-paused.png`
- Implementation viewport: 292 × 46 effective pixels at 100% density
- States checked: playing and paused

## Full-view comparison

The reference pill was normalized from roughly 746 × 124 to MiaDock's configured
292 × 46 compact surface. The implementation preserves the reference hierarchy:
artwork at the leading edge, a single-line title, and a fixed trailing state area.

## Focused region comparison

- Artwork: 30 × 30, 7 px radius, 10 px leading inset. This is approximately the
  same surface-height ratio and left inset as the reference.
- Metadata: begins after the artwork with 8 px compact grid spacing, remains
  vertically centered, and uses regular 12 px Segoe UI Variable Text.
- Playing state: trailing area contains the same 10 × 14 three-bar activity
  indicator used by the main dock. Bars grow equally above and below their
  vertical center and use the shared accent brush.
- Paused state: the artwork remains mounted and visible beneath a 45% black scrim;
  the pause glyph remains fully opaque. The trailing area switches to the native
  Segoe Fluent Icons ellipsis glyph.
- Next-track state: reuses the same artwork scrim, shows the next glyph for 650 ms,
  and uses the same trailing ellipsis state as the reference.

## Fidelity surfaces

- Typography: shared Segoe UI Variable Text, regular weight, single-line ellipsis.
- Spacing and rhythm: scaled against the 292 × 46 production viewport; no content
  touches the outer pill or competes with its 23 px radius.
- Colors: semantic theme brushes are retained; the media scrim is a dedicated
  semantic token with high-contrast behavior.
- Image fidelity: production artwork continues to use the real media-session image.
  The QA fake source's SVG does not render through `BitmapImage`, so the captured
  diagnostic screenshot shows the existing fallback icon; DOM/XAML inspection and
  tests verify that the artwork control stays mounted below the scrim.
- Copy: runtime media title is preserved and safely truncated.

## Comparison history

1. P1: 42 px artwork occupied 91% of the 46 px surface and the 6 × 28 meter was
   visually oversized. Fixed to 36 px artwork and a 5 × 24 meter.
2. P1: paused overlay used an opaque surface brush at 78% element opacity, which
   overly obscured the artwork and dimmed the icon. Fixed with separate layers:
   a 45% black scrim below a fully opaque glyph.
3. P2: paused and track-change states left baseline bars at the trailing edge.
   Fixed by switching those states to the native ellipsis glyph.
4. P2: the first compact pass still carried too much visual weight. Refined to a
   30 px artwork tile, 12 px title and the main dock's centered 10 × 14 meter
   without changing the user's dock-size preset.

## Final result

passed
