# Native Miamo Ads validation

Run from the repository root:

```powershell
./scripts/validation/Invoke-MiamoAdsNative.ps1
```

The fixture links the production XAML component, creates a real WinUI
NavigationView and 500×500 PNGs, and injects fake advertising/browser clients.
It does not read user settings or modify an existing MiaDock process.

It checks layout, above-footer ordering, Uniform rendering, min exposure,
preload exclusion, pane toggle, actual minimize/restore and activation,
keyboard focus/manual navigation, API failure/refresh, single/broken/empty
campaigns, deduplication, click dispatch and owning-window close cleanup.
Only its own test windows/processes are closed. The test briefly activates
these windows; its intentional inactive-window checks remain inactive.

Results and a rendered layout image are in
`artifacts/validation/miamo-ads/native.json` and `native.json.png`.
The fixture calls the private refresh handler directly to inject new lists;
five-minute scheduling is tested separately with the core monotonic schedule.
Neither live Miamo campaigns nor real browser launch/server recording are
claimed by this test. See `doc/MIAMO_ADS.md` for the full scope and open checks.
