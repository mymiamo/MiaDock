# MiaDock

MiaDock is a Windows 11 desktop application that presents media activity in a compact, themeable island interface. The first development phase contains a WinUI 3 preview shell, deterministic UI state management, theme tokens, and a fake media module. Windows media sessions and overlay window behavior are intentionally deferred.

## Requirements

- Windows 11, build 22000 or newer
- .NET 10 SDK
- Visual Studio 2026 with the Windows application development workload for launching and debugging WinUI

## Build and test

```powershell
dotnet restore MiaDock.sln
dotnet build MiaDock.sln -c Debug -p:Platform=x64
dotnet test tests\MiaDock.Core.Tests\MiaDock.Core.Tests.csproj -c Debug
dotnet test tests\MiaDock.WinUI.Tests\MiaDock.WinUI.Tests.csproj -c Debug
```

The preview application uses local sample data and does not access media history, accounts, network services, or telemetry.
