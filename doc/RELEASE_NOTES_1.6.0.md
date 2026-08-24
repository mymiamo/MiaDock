# MiaDock 1.6.0.0 release notes

* Added central DockMetrics for Compact, Default, Large and safely bounded Custom dock thicknesses, including consistent compact, hover, expanded and notification metrics.
* Added live appearance controls for density, content icon scale, corner style and top offset, plus Minimal, Dynamic, Large and Custom appearance presets.
* Settings are schema-migrated from 1.5.x without replacing the existing configuration contract; legacy edge margin becomes the new top offset.
* Motion preferences now independently control event morphs and decorative icon/shell emphasis. The existing Windows animation-preference service remains the accessibility authority.
* Media, privacy, Device Hub and Clipboard Peek updates share the existing generation- and cancellation-safe island transition pipeline, preventing stale visual state during rapid events.
* Added an opt-in experimental media spectrum engine, disabled by default and switchable live from Media settings without restarting MiaDock.
* Spectrum capture now uses one shared-mode WASAPI loopback recorder for a selectable Windows render endpoint. This supports Sonar virtual outputs, 44.1/48/96 kHz, 1–32 channel Float32 and PCM 16/24/32 packets, with an eight-channel analysis ceiling.
* The three-band visualizer uses a 2048-sample Hann-window FFT with overlap and bounded analysis frequency. Bass, vocal/mid and treble data fall back to the selected media session's real channel peaks whenever system spectrum is stale or unavailable.
* Recorder lifecycle is independent from track metadata and media-session rebinding. Silent packets remain healthy, device and recorder failures use bounded retry, and transient Apple Music playback-state changes no longer interrupt an active Sonar spectrum stream.
* Settings schema 33 preserves the experimental default-off policy and adds the selected spectrum output endpoint. Missing endpoints remain visible in settings and can be replaced without restarting the app.
* Release validation covers FFT bands, multichannel phase cancellation, packet formats and padding, live lifecycle transitions, endpoint selection, stale-spectrum fallback and delayed callbacks. The real selected Sonar Media endpoint was also verified at 96 kHz / 8 channels.
* GitHub release artifacts are locally generated candidates. Microsoft Store submission still requires the project owner's device, WACK and flight gates.
