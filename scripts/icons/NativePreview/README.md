# Native dock icon verification

This standalone WinUI window loads every bundled Ionicons outline/filled pair
through the production `DockIcon` component. It does not start MiaDock services
or read or modify MiaDock settings. It is a developer tool and is not included
in the application build.

Build from the repository root with Visual Studio MSBuild:

```powershell
& 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe' scripts/icons/NativePreview/NativePreview.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
```

For visual inspection, run
`scripts/icons/NativePreview/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NativePreview.exe`.
Use the dark/light button to verify both themes and in-place brush color updates.
Each key has an outline and a filled icon. Some Ionicons pairs intentionally
share the same silhouette. Scroll to inspect the last row.

For a repeatable native decoding check, supply an absolute JSON output path
and `--check`. After three seconds the tool records loaded sources, actual
sizes and the current window DPI scale, then exits with code 0 when every
source loaded and every layout check passed, or 1 on failure:

```powershell
$previewExe = Join-Path $PWD 'scripts/icons/NativePreview/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NativePreview.exe'
$previewResult = Join-Path $PWD 'artifacts/ionicons-native-result.json'
$previewProcess = Start-Process -FilePath $previewExe -ArgumentList ('"' + $previewResult + '" --check') -WindowStyle Hidden -PassThru -Wait
$previewProcess.ExitCode
Get-Content -LiteralPath $previewResult
```

The preview also embeds icons in Focus, network, throughput, power, privacy and
call-sized containers. It verifies that their arranged sizes stay at FontSize,
and that explicit stretching receives a matching raster budget. This regression
caught a 17-DIP Focus icon being stretched to 46 DIP from a 17-pixel source.

This checks native source loading and layout sizes; it does not assert visual alignment, a
screen reader's speech, live dock interactions, or physical DPI transitions.
Use an interactive Windows desktop. Do not interpret a result from one scale
as validation of other monitor scales.
