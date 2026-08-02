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
    $ResultsDirectory = Join-Path $repositoryRoot "artifacts\validation\1.2.1"
}

$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$steps = [System.Collections.Generic.List[object]]::new()
$summary = [ordered]@{
    SchemaVersion = 1
    Product = "MiaDock"
    Version = "1.2.1.0"
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
        $packageIdentity.Version -ne "1.2.1.0" -or
        $packageIdentity.Name -ne "mymiamo.net.MiaDock") {
        throw "Package identity or version is not the expected MiaDock 1.2.1.0 identity."
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
            $identity.Version -ne "1.2.1.0" -or
            $null -eq $startupTask) {
            throw "The packaged identity, version or StartupTask declaration is invalid."
        }

        $requiredEntries = @(
            "Assets/miadock-ringtone.wav",
            "Assets/StoreLogo.png",
            "Assets/Square44x44Logo.png",
            "Assets/Square150x150Logo.png",
            "MiaDock.App.exe")
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
        -Detail "$($package.Name) contains the expected identity, StartupTask and runtime assets."
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

    $executable = Join-Path $repositoryRoot (
        "src\MiaDock.App\bin\$Platform\$Configuration\" +
        "net10.0-windows10.0.26100.0\win-x64\MiaDock.App.exe")
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "The built MiaDock.App executable was not found."
    }

    $process = Start-Process -FilePath $executable -PassThru
    Start-Sleep -Seconds 5
    $runningProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if ($null -eq $runningProcess) {
        throw "MiaDock.App exited during the startup smoke test."
    }

    $responding = $runningProcess.Responding
    Stop-Process -Id $runningProcess.Id -Force
    if (-not $responding) {
        throw "MiaDock.App entered a non-responding state during startup."
    }

    Add-StepResult -Name "Unpackaged startup smoke test" -Result "Passed" `
        -Detail "The process remained alive and responsive for five seconds."
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
