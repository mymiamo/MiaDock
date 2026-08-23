# MiaDock 1.5.5.0 release notes

* Theme selection is now deliberate: selecting a theme marks it as pending, and **Apply theme** persists and activates it. Other appearance controls, including size and motion, keep their live-preview behavior.
* Pending theme selections survive unrelated live appearance saves. If another source changes the persisted theme, the settings view reloads that new value.
* Live theme changes replace detached XAML resource dictionaries instead of mutating dictionaries that have participated in resource resolution, avoiding a native XAML fail-fast during transitions.
* Adaptive Fluent rebuilds its surface, text, control, stroke, and accent brushes from the Windows background, foreground, and accent colors for the active light or dark system palette.
* Tozpembe resolves as a light theme. Equivalent Mica backdrops remain connected across resource refreshes, while actual backdrop changes recreate only the required native object.
* This maintenance release changes no settings schema or external integration contract.
