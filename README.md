<div align="center">

# MiaDock

### A modern Dynamic Island experience for Windows 11.

Lightweight • Native • Customizable • Open Source

[![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11&logoColor=white)](#)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-512BD4?logo=.net&logoColor=white)](#)
[![License](https://img.shields.io/github/license/mymiamo/MiaDock)](LICENSE)
[![Stars](https://img.shields.io/github/stars/mymiamo/MiaDock)](https://github.com/mymiamo/MiaDock/stargazers)
[![Issues](https://img.shields.io/github/issues/mymiamo/MiaDock)](https://github.com/mymiamo/MiaDock/issues)

</div>

---

## ✨ About

**MiaDock** brings a Dynamic Island-inspired experience to Windows 11.

Instead of copying Apple's implementation, MiaDock is designed specifically for Windows with native performance, smooth animations, and seamless integration.

The goal is to provide a beautiful notification and media hub that feels like it belongs to Windows.

---

## 🚀 Planned Features

- 🎵 Music controls
- 🔔 Notification popups
- 🔋 Battery status
- 🎧 Bluetooth device events
- 📶 Network information
- 📥 Download progress
- 🎤 Microphone & camera indicators
- ⌨️ Keyboard shortcuts
- 🌙 Light / Dark mode
- 🎨 Theme customization
- 📍 Multi-monitor support
- ⚡ Native WinUI 3 animations
- 🎮 Smart fullscreen behavior
- 🔧 Modern settings panel

---

## 🖥 Screenshots

> Coming soon.

---

## 🛠 Built With

- WinUI 3
- Windows App SDK
- .NET
- C#
- MVVM Architecture

---

## 📦 Installation

Clone the repository

```bash
git clone https://github.com/mymiamo/MiaDock.git
```

Open the solution with Visual Studio 2022.

Requirements

- Windows 11
- Visual Studio 2022
- Windows App SDK
- .NET

---

## 🗺 Roadmap

- [x] Project planning
- [x] Core overlay window
- [x] Island animations
- [x] Music integration
- [ ] Notification system
- [ ] Settings page
- [ ] Widget system
- [ ] Microsoft Store release

---

## 🤝 Contributing

Contributions, ideas and bug reports are always welcome.

If you'd like to contribute:

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Open a Pull Request

---

## 🐛 Bug Reports

Found a bug?

Please create an Issue with:

- Windows version
- MiaDock version
- Steps to reproduce
- Screenshots (if possible)

---

## 🦺 Build and test

```powershell
dotnet restore MiaDock.sln
dotnet build MiaDock.sln -c Debug -p:Platform=x64
dotnet test tests\MiaDock.Core.Tests\MiaDock.Core.Tests.csproj -c Debug
dotnet test tests\MiaDock.WinUI.Tests\MiaDock.WinUI.Tests.csproj -c Debug
dotnet test tests\MiaDock.Platform.Windows.Tests\MiaDock.Platform.Windows.Tests.csproj -c Debug -p:Platform=x64
```

Run the current Debug build from the configuration folder (not the stale `win-x64` publish subfolder):

```powershell
& .\src\MiaDock.App\bin\x64\Debug\net10.0-windows10.0.26100.0\MiaDock.App.exe
& .\src\MiaDock.App\bin\x64\Debug\net10.0-windows10.0.26100.0\MiaDock.App.exe --settings
```

Optional endurance profiles are disabled during normal test runs. Run the 30-minute event profile or the 8-hour idle profile explicitly:

```powershell
$env:MIADOCK_SOAK_PROFILE = "events" # events, idle, or all
dotnet test tests\MiaDock.Core.Tests\MiaDock.Core.Tests.csproj -c Release --filter TestCategory=Soak
Remove-Item Env:MIADOCK_SOAK_PROFILE
```

For a shortened validation of the same code paths, set `MIADOCK_SOAK_SCALE` to a value between `0` and `1`.

---

## ⭐ Support

If you like the project, consider giving it a ⭐ on GitHub.

It helps a lot.

---

## 👨‍💻 Author

**Eray Durupınar**

GitHub

https://github.com/mymiamo

Website

https://mymiamo.net

---

## 📄 License

This project is licensed under the GNU Affero General Public License.
