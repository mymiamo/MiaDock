[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64",

    [switch]$SkipRestore,

    [switch]$LaunchSmokeTest,

    [switch]$StopRunningApp,

    [switch]$BuildUnsignedTestPackage,

    [string]$MspdbcmfPath,

    [string]$ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
$applicationProject = Join-Path $repositoryRoot "src\MiaDock.App\MiaDock.App.csproj"
$solution = Join-Path $repositoryRoot "MiaDock.sln"

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot "artifacts\validation\1.5.4.0"
}

$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$steps = [System.Collections.Generic.List[object]]::new()
$summary = [ordered]@{
    SchemaVersion = 1
    Product = "MiaDock"
    Version = "1.5.4.0"
    StartedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    CompletedAtUtc = $null
    Result = "Running"
    Configuration = $Configuration
    Platform = $Platform
    InstalledStorePackage = $null
    Steps = $steps
}

function Add-StepResult {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Result,

        [string]$Detail = ""
    )

    $steps.Add([ordered]@{
        Name = $Name
        Result = $Result
        Detail = $Detail
        CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    })
}

function Invoke-DotNetStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host "== $Name =="
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        Add-StepResult -Name $Name -Result "Failed" -Detail "dotnet exit code $LASTEXITCODE"
        throw "$Name failed with exit code $LASTEXITCODE."
    }

    Add-StepResult -Name $Name -Result "Passed"
}

function Find-Mspdbcmf {
    if (-not [string]::IsNullOrWhiteSpace($MspdbcmfPath)) {
        $candidate = [System.IO.Path]::GetFullPath($MspdbcmfPath)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
        throw "The specified mspdbcmf.exe path does not exist."
    }

    $command = Get-Command "mspdbcmf.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $vsWhere = Join-Path ${env:ProgramFiles(x86)} (
        "Microsoft Visual Studio\Installer\vswhere.exe")
    if (Test-Path -LiteralPath $vsWhere) {
        $found = @(& $vsWhere -latest -products * -find "**\mspdbcmf.exe" |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $x64 = $found | Where-Object {
            $_ -match '[\\/]Hostx64[\\/]x64[\\/]mspdbcmf\.exe$'
        } | Select-Object -First 1
        if ($null -ne $x64 -and
            (Test-Path -LiteralPath $x64 -PathType Leaf)) {
            return $x64
        }
        if ($found.Count -gt 0 -and
            (Test-Path -LiteralPath $found[0] -PathType Leaf)) {
            return $found[0]
        }
    }

    return $null
}

function Assert-ManifestConsistency {
    $packageManifestPath = Join-Path $repositoryRoot "src\MiaDock.App\Package.appxmanifest"
    $applicationManifestPath = Join-Path $repositoryRoot "src\MiaDock.App\app.manifest"
    [xml]$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw
    [xml]$applicationManifest = Get-Content -LiteralPath $applicationManifestPath -Raw

    $packageIdentity = $packageManifest.SelectSingleNode(
        "/*[local-name()='Package']/*[local-name()='Identity']")
    $applicationIdentity = $applicationManifest.SelectSingleNode(
        "/*[local-name()='assembly']/*[local-name()='assemblyIdentity']")
    $startupTask = $packageManifest.SelectSingleNode(
        "//*[local-name()='StartupTask' and @TaskId='MiaDockStartupTask']")

    if ($null -eq $packageIdentity -or
        $packageIdentity.Version -ne "1.5.4.0" -or
        $packageIdentity.Name -ne "mymiamo.net.MiaDock") {
        throw "Package identity or version is not the expected MiaDock 1.5.4.0 identity."
    }

    if ($null -eq $applicationIdentity -or
        $applicationIdentity.version -ne $packageIdentity.Version) {
        throw "Application and package manifest versions do not match."
    }

    if ($null -eq $startupTask) {
        throw "MiaDockStartupTask is missing from Package.appxmanifest."
    }

    Add-StepResult -Name "Manifest consistency" -Result "Passed" `
        -Detail "Identity, version and StartupTask declarations are consistent."
}

function Get-ExpectedFluentTrayAssets {
    return @(
        "window_24_regular.svg",
        "settings_24_regular.svg",
        "arrow_previous_24_regular.svg",
        "play_24_regular.svg",
        "pause_24_regular.svg",
        "arrow_next_24_regular.svg",
        "music_note_2_24_regular.svg",
        "alert_24_regular.svg",
        "desktop_24_regular.svg",
        "eye_off_24_regular.svg",
        "power_24_regular.svg")
}

function Assert-FluentTrayIconResources {
    $assetDirectory = Join-Path $repositoryRoot "src\MiaDock.App\Assets\FluentIcons"
    $expectedAssets = Get-ExpectedFluentTrayAssets
    $missingAssets = @($expectedAssets | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $assetDirectory $_) -PathType Leaf)
    })
    if ($missingAssets.Count -gt 0) {
        throw "Fluent tray icon assets are missing: $($missingAssets -join ', ')."
    }

    $resolverPath = Join-Path $repositoryRoot "src\MiaDock.Platform.Windows\Tray\FluentTrayIconResolver.cs"
    $coordinatorPath = Join-Path $repositoryRoot "src\MiaDock.App\Services\TrayMenuCoordinator.cs"
    $resolverSource = Get-Content -LiteralPath $resolverPath -Raw
    $coordinatorSource = Get-Content -LiteralPath $coordinatorPath -Raw
    foreach ($asset in $expectedAssets) {
        if ($resolverSource -notmatch [regex]::Escape($asset)) {
            throw "Fluent tray icon resolver has no mapping for $asset."
        }
    }

    if ($resolverSource -notmatch "SvgImageSource" -or
        $coordinatorSource -match "IconGlyph") {
        throw "Tray icon semantics must resolve through local SVG assets, not raw glyph values."
    }

    Add-StepResult -Name "Fluent tray icon resources" -Result "Passed" `
        -Detail "$($expectedAssets.Count) semantic Fluent SVG tray assets and resolver mappings are present."
}

function Assert-LocalizationCoverage {
    $requiredCultures = @("az-Latn-AZ", "en-US", "es-ES", "es-MX", "pt-BR", "tr-TR")
    $requiredKeys = @(
        "Common.Play",
        "Common.Pause",
        "Dock.Show",
        "Dock.Hide",
        "Dock.Settings",
        "Tray.Previous",
        "Tray.Next",
        "Tray.MediaNotFound",
        "Tray.PrimaryMonitor",
        "Tray.ActiveMonitor",
        "Tray.DefaultMedia",
        "Tray.SelectMonitor",
        "Tray.TemporaryNotifications",
        "Tray.FocusTurnOff",
        "Tray.Exit")

    foreach ($culture in $requiredCultures) {
        $resourcePath = Join-Path $repositoryRoot "src\MiaDock.App\Strings\$culture\Resources.resw"
        if (-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)) {
            throw "Localization table is missing for $culture."
        }

        [xml]$resourceTable = Get-Content -LiteralPath $resourcePath -Raw
        $resourceKeys = @($resourceTable.root.data | ForEach-Object { [string]$_.name })
        $missingKeys = @($requiredKeys | Where-Object { $_ -notin $resourceKeys })
        if ($missingKeys.Count -gt 0) {
            throw "Localization table $culture is missing tray keys: $($missingKeys -join ', ')."
        }
    }

    Add-StepResult -Name "Tray localization coverage" -Result "Passed" `
        -Detail "Required tray strings are present in all $($requiredCultures.Count) supported languages."
}

function Assert-NoDuplicateProjectReferences {
    $duplicates = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "src") -Recurse -Filter "*.csproj" -File |
        ForEach-Object {
            [xml]$project = Get-Content -LiteralPath $_.FullName -Raw
            $references = @($project.SelectNodes("//*[local-name()='ProjectReference']") |
                ForEach-Object { [string]$_.Include } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            $duplicateReferences = @($references |
                Group-Object { $_.Trim() } |
                Where-Object { $_.Count -gt 1 } |
                ForEach-Object { $_.Name })
            if ($duplicateReferences.Count -gt 0) {
                $duplicates.Add("$($_.Name): $($duplicateReferences -join ', ')")
            }
        }

    if ($duplicates.Count -gt 0) {
        throw "Duplicate ProjectReference entries found: $($duplicates -join '; ')."
    }

    Add-StepResult -Name "Project reference consistency" -Result "Passed" `
        -Detail "No duplicate ProjectReference entries were found under src."
}

function Assert-TestPackageContents {
    param(
        [Parameter(Mandatory)]
        [string]$PackageDirectory
    )

    $package = Get-ChildItem -LiteralPath $PackageDirectory -Recurse -Filter "*.msix" |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "The unsigned test MSIX was not produced."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $manifestEntry = $archive.Entries |
            Where-Object { $_.FullName -eq "AppxManifest.xml" } |
            Select-Object -First 1
        if ($null -eq $manifestEntry) {
            throw "AppxManifest.xml is missing from the test package."
        }

        $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
        try {
            [xml]$manifest = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }

        $identity = $manifest.SelectSingleNode(
            "/*[local-name()='Package']/*[local-name()='Identity']")
        $startupTask = $manifest.SelectSingleNode(
            "//*[local-name()='StartupTask' and @TaskId='MiaDockStartupTask']")
        if ($null -eq $identity -or
            $identity.Name -ne "mymiamo.net.MiaDock" -or
            $identity.Version -ne "1.5.4.0" -or
            $null -eq $startupTask) {
            throw "The packaged identity, version or StartupTask declaration is invalid."
        }

        $requiredEntries = @(
            "Assets/miadock-ringtone.wav",
            "Assets/StoreLogo.png",
            "Assets/Square44x44Logo.png",
            "Assets/Square150x150Logo.png",
            "MiaDock.App.exe",
            "WinUIEx.dll",
            "WinUIEx.pri") + @(
                Get-ExpectedFluentTrayAssets | ForEach-Object { "Assets/FluentIcons/$_" })
        $entryNames = @($archive.Entries | ForEach-Object {
            $_.FullName.Replace("\", "/")
        })
        $missingEntries = @($requiredEntries | Where-Object { $_ -notin $entryNames })
        if ($missingEntries.Count -gt 0) {
            throw "Test package entries are missing: $($missingEntries -join ', ')."
        }
    } finally {
        $archive.Dispose()
    }

    Add-StepResult -Name "Unsigned test package contents" -Result "Passed" `
        -Detail "$($package.Name) contains the expected identity, StartupTask, runtime and Fluent tray assets."
}

function Get-ApplicationOutputDirectory {
    return Join-Path $repositoryRoot (
        "src\MiaDock.App\bin\$Platform\$Configuration\" +
        "net10.0-windows10.0.26100.0\win-x64")
}

function Assert-UnpackagedRuntimeDependencies {
    $outputDirectory = Get-ApplicationOutputDirectory
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        throw "The application output directory was not found: $outputDirectory"
    }

    $requiredFiles = @("WinUIEx.dll", "WinUIEx.pri")
    $missingFiles = @($requiredFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $outputDirectory $_) -PathType Leaf)
    })
    if ($missingFiles.Count -gt 0) {
        throw "The unpackaged application output is missing WinUIEx runtime files: $($missingFiles -join ', ')."
    }

    $depsPath = Join-Path $outputDirectory "MiaDock.App.deps.json"
    if (-not (Test-Path -LiteralPath $depsPath -PathType Leaf)) {
        throw "The unpackaged application dependency manifest was not found: $depsPath"
    }

    $depsJson = Get-Content -LiteralPath $depsPath -Raw
    if ($depsJson -notmatch '"WinUIEx/2\.9\.3"') {
        throw "The unpackaged application dependency manifest does not contain WinUIEx 2.9.3."
    }

    Add-StepResult -Name "Unpackaged WinUIEx runtime payload" -Result "Passed" `
        -Detail "WinUIEx.dll, WinUIEx.pri and the WinUIEx 2.9.3 dependency entry are present."
}

function Assert-NoStartupUnhandledException {
    param(
        [Parameter(Mandatory)]
        [DateTimeOffset]$StartedAtUtc
    )

    $logDirectory = Join-Path $env:LOCALAPPDATA "MiaDock\Logs"
    if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) {
        return
    }

    $failures = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $logDirectory -Filter "*.ndjson" -File |
        Where-Object { $_.LastWriteTimeUtc -ge $StartedAtUtc.UtcDateTime.AddSeconds(-2) } |
        ForEach-Object {
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ([string]::IsNullOrWhiteSpace($line)) {
                    continue
                }

                try {
                    $entry = $line | ConvertFrom-Json
                    # ConvertFrom-Json materializes ISO timestamps as local DateTime values.
                    # Read the raw wire value so an older UTC crash cannot be mistaken for a
                    # startup-smoke failure after an offset conversion.
                    $timestampMatch = [regex]::Match(
                        $line,
                        '"TimestampUtc"\s*:\s*"(?<timestamp>[^"]+)"',
                        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
                    if (-not $timestampMatch.Success) {
                        continue
                    }
                    $timestamp = [DateTimeOffset]::MinValue
                    if (-not [DateTimeOffset]::TryParse(
                        $timestampMatch.Groups["timestamp"].Value,
                        [System.Globalization.CultureInfo]::InvariantCulture,
                        [System.Globalization.DateTimeStyles]::RoundtripKind,
                        [ref]$timestamp)) {
                        continue
                    }
                    if ($entry.eventId -eq "app.unhandled" -and $timestamp -ge $StartedAtUtc) {
                        $exception = if ([string]::IsNullOrWhiteSpace($entry.exceptionType)) {
                            "unspecified exception"
                        } else {
                            $entry.exceptionType
                        }
                        $failures.Add("$exception at $($timestamp.ToString('O'))")
                    }
                } catch {
                    # Logs can be written while the app is running; an incomplete line is ignored.
                }
            }
        }

    if ($failures.Count -gt 0) {
        throw "MiaDock logged app.unhandled during the startup smoke test: $($failures -join '; ')."
    }
}

function Read-InstalledStorePackage {
    $package = Get-AppxPackage -Name "mymiamo.net.MiaDock" -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        $summary.InstalledStorePackage = [ordered]@{
            Present = $false
        }
        Add-StepResult -Name "Installed Store package inspection" -Result "NotAvailable" `
            -Detail "No installed Microsoft Store package was found."
        return
    }

    $summary.InstalledStorePackage = [ordered]@{
        Present = $true
        Version = $package.Version.ToString()
        Status = $package.Status.ToString()
        SignatureKind = $package.SignatureKind.ToString()
        IsDevelopmentMode = [bool]$package.IsDevelopmentMode
    }
    Add-StepResult -Name "Installed Store package inspection" -Result "Passed" `
        -Detail "Installed package metadata was read without modifying the package."
}

function Prepare-RunningApplication {
    $running = @(Get-Process -Name "MiaDock.App" -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) {
        Add-StepResult -Name "Running application preparation" -Result "Passed" `
            -Detail "No running MiaDock.App process was found."
        return
    }

    if (-not $StopRunningApp) {
        Add-StepResult -Name "Running application preparation" -Result "Skipped" `
            -Detail "MiaDock.App is running; the build may be locked. Use -StopRunningApp for isolated validation."
        return
    }

    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    if (Get-Process -Name "MiaDock.App" -ErrorAction SilentlyContinue) {
        throw "MiaDock.App could not be stopped before validation."
    }

    Add-StepResult -Name "Running application preparation" -Result "Passed" `
        -Detail "Running MiaDock.App processes were stopped before build and test operations."
}

function Invoke-ApplicationSmokeTest {
    $running = @(Get-Process -Name "MiaDock.App" -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0 -and -not $StopRunningApp) {
        Add-StepResult -Name "Unpackaged startup smoke test" -Result "Skipped" `
            -Detail "MiaDock.App was already running. Use -StopRunningApp to run an isolated smoke test."
        return
    }

    if ($running.Count -gt 0) {
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }

    $outputDirectory = Get-ApplicationOutputDirectory
    $executable = Join-Path $outputDirectory "MiaDock.App.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "The built MiaDock.App executable was not found."
    }

    $startedAtUtc = [DateTimeOffset]::UtcNow
    $process = Start-Process -FilePath $executable -PassThru
    try {
        Start-Sleep -Seconds 10
        $runningProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -eq $runningProcess) {
            throw "MiaDock.App exited during the startup smoke test."
        }

        $responding = $runningProcess.Responding
        $mainWindowHandle = $runningProcess.MainWindowHandle
        Assert-NoStartupUnhandledException -StartedAtUtc $startedAtUtc
        if (-not $responding) {
            throw "MiaDock.App entered a non-responding state during startup."
        }
        if ($mainWindowHandle -eq [IntPtr]::Zero) {
            throw "MiaDock.App did not create a top-level window during startup."
        }
    } finally {
        $smokeProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -ne $smokeProcess) {
            Stop-Process -Id $smokeProcess.Id -Force
        }
    }

    Add-StepResult -Name "Unpackaged startup smoke test" -Result "Passed" `
        -Detail "The process remained alive, responsive, windowed and free of app.unhandled events for ten seconds."
}

try {
    Push-Location $repositoryRoot
    try {
        Prepare-RunningApplication

        if (-not $SkipRestore) {
            Invoke-DotNetStep -Name "Restore" -Arguments @(
                "restore", $solution, "-p:Platform=$Platform")
        }

        Assert-ManifestConsistency
        Assert-FluentTrayIconResources
        Assert-LocalizationCoverage
        Assert-NoDuplicateProjectReferences

        Invoke-DotNetStep -Name "Core tests" -Arguments @(
            "test",
            "tests\MiaDock.Core.Tests\MiaDock.Core.Tests.csproj",
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--no-restore",
            "-v:q")
        Invoke-DotNetStep -Name "Windows platform tests" -Arguments @(
            "test",
            "tests\MiaDock.Platform.Windows.Tests\MiaDock.Platform.Windows.Tests.csproj",
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--no-restore",
            "-v:q")
        Invoke-DotNetStep -Name "WinUI resource tests" -Arguments @(
            "test",
            "tests\MiaDock.WinUI.Tests\MiaDock.WinUI.Tests.csproj",
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--no-restore",
            "-v:q")
        Invoke-DotNetStep -Name "Application build" -Arguments @(
            "build",
            $applicationProject,
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "-p:RuntimeIdentifier=win-x64",
            "--no-restore",
            "-v:minimal")

        Assert-UnpackagedRuntimeDependencies

        if ($LaunchSmokeTest) {
            Invoke-ApplicationSmokeTest
        } else {
            Add-StepResult -Name "Unpackaged startup smoke test" -Result "Skipped" `
                -Detail "Use -LaunchSmokeTest to enable this check."
        }

        Read-InstalledStorePackage

        if ($BuildUnsignedTestPackage) {
            $testPackageDirectory = Join-Path $ResultsDirectory "package"
            New-Item -ItemType Directory -Path $testPackageDirectory -Force | Out-Null
            $testPackageDirectoryForMsBuild =
                $testPackageDirectory.Replace("\", "/") + "/"
            $packageArguments = @(
                "build",
                $applicationProject,
                "-c", "Release",
                "-p:Platform=x64",
                "-p:RuntimeIdentifier=win-x64",
                "-p:BuildMsix=true",
                "-p:UapAppxPackageBuildMode=SideloadOnly",
                "-p:AppxPackageSigningEnabled=false",
                "-p:AppxPackageDir=$testPackageDirectoryForMsBuild",
                "-p:AppxSymbolPackageEnabled=false",
                "--no-restore",
                "-v:minimal")
            $symbolTool = Find-Mspdbcmf
            if ($null -ne $symbolTool) {
                $symbolToolForMsBuild = $symbolTool.Replace("\", "/")
                $env:PATH = "$(Split-Path -Parent $symbolTool);$env:PATH"
                $packageArguments += "-p:PdbCmfx64ExeFullPath=$symbolToolForMsBuild"
                $packageArguments += "-p:MsPdbCmfExeFullpath=$symbolToolForMsBuild"
            }
            Invoke-DotNetStep -Name "Unsigned test package build" -Arguments $packageArguments
            Assert-TestPackageContents -PackageDirectory $testPackageDirectory
        } else {
            Add-StepResult -Name "Unsigned test package build" -Result "Skipped" `
                -Detail "Use -BuildUnsignedTestPackage to create a non-installable inspection artifact."
        }

        $summary.Result = "Passed"
    } finally {
        Pop-Location
    }
} catch {
    $summary.Result = "Failed"
    Add-StepResult -Name "Validation script" -Result "Failed" `
        -Detail $_.Exception.GetType().Name
    throw
} finally {
    $summary.CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    $summaryPath = Join-Path $ResultsDirectory "release-validation.json"
    $summary | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $summaryPath -Encoding utf8
    Write-Host ""
    Write-Host "Validation summary: $summaryPath"
}
