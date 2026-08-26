# MiaDock 1.6.2.0 release notes

* Packages the current opt-in `SystemSpectrum` engine from the active source tree under Store identity version 1.6.2.0.
* Keeps experimental spectrum disabled by default and exposes its live toggle in Media settings.
* Allows selecting the Windows render endpoint monitored by WASAPI loopback, including SteelSeries Sonar virtual outputs.
* Uses a 2048-sample Hann-window FFT for bass, mid/vocal and treble analysis across supported Float32 and PCM 16/24/32 formats.
* Preserves recorder continuity across Apple Music and rapid media-session transitions while stopping after a real stable pause.
* Falls back to the selected media session's real peak amplitude whenever spectrum data is stale or unavailable.
* Store package validation verifies the 1.6.2.0 identity, StartupTask, runtime files, symbols and required application assets.
