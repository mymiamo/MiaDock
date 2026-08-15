# MiaDock 1.5.0.0 release notes

## New features

* **Device Hub** unifies Bluetooth, audio input/output, and removable storage on the dock. Connection, disconnection, audio-output change, and low-battery events are on by default. Safe eject and Windows Bluetooth/sound shortcuts are included.
* **Clipboard Peek** is opt-in. It classifies copies (text, URL, email, color, file, folder, image), keeps a RAM-only session history (default 5 items), masks secrets until revealed, and supports copy / open / compose email / open folder / save image.
* **Hourly Notification** is opt-in and shows the current time on the dock at the top of each hour.
* **Notification Sounds** add local cues for offline network, connected without internet, low battery, device connect/disconnect, and hourly. Defaults are on; each cue can be previewed. Timer alarm is independent.
* Expanded idle dock can toggle Wi-Fi and Bluetooth radios.
* Edge-reveal fullscreen/visibility mode keeps a 15 DIP status strip so the dock remains findable.

## Settings

* Personalize → Notification Sounds: master switch plus per-event toggles and preview.
* Modules: Device Hub and Clipboard Peek options.
* Modules → Optional: keyboard locks, USB events, hourly reminder.

## Privacy

* Clipboard Peek is off until enabled, monitors the clipboard only while on, never writes history to disk, and clears it on exit.
* Sensitive clipboard content (keys, tokens, OTPs, payment-card patterns, high-entropy secrets) is masked.
* Device Hub and sounds use local Windows APIs only; nothing is sent off-device.

## Defaults

* Device Hub: on
* Clipboard Peek: off
* Hourly notification: off
* Notification sounds: on (hourly sound only plays if the hourly module is also on)
* Settings schema: 29

## Versioning

* Package, assembly, manifest, release, and validation versions are `1.5.0.0`.
