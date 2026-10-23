[CmdletBinding()]
param(
    [Alias("Phase11EvidencePath")]
    [string]$ReleaseEvidencePath,

    [string]$MspdbcmfPath,

    [string]$ResultsDirectory,

    [string]$AutomatedTestResultsDirectory,

    [switch]$SkipRestore,

    [switch]$AllowUnverifiedCandidate,

    [switch]$AllowDirtyWorkingTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$expectedVersion = "1.7.3.0"
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
$applicationProject = Join-Path $repositoryRoot "src\MiaDock.App\MiaDock.App.csproj"
$validationScript = Join-Path $repositoryRoot (
    "scripts\validation\Invoke-MiaDockReleaseValidation.ps1")

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot (
        "artifacts\release\1.7.3.0")
}

$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
$releaseEvidenceStatus = "Validated"
$workingTreeDirty = $false
if (-not [string]::IsNullOrWhiteSpace($ReleaseEvidencePath)) {
    $ReleaseEvidencePath = [System.IO.Path]::GetFullPath($ReleaseEvidencePath)
}

function Assert-ReleaseEvidence {
    if ([string]::IsNullOrWhiteSpace($ReleaseEvidencePath)) {
        if ($AllowUnverifiedCandidate) {
            $script:releaseEvidenceStatus = "Bypassed"
            return
        }
        throw "Release evidence is required unless -AllowUnverifiedCandidate is specified."
    }

    if (-not (Test-Path -LiteralPath $ReleaseEvidencePath -PathType Leaf)) {
        throw "Release evidence file was not found: $ReleaseEvidencePath"
    }

    $evidence = Get-Content -LiteralPath $ReleaseEvidencePath -Raw |
        ConvertFrom-Json
    if ($evidence.SchemaVersion -ne 1 -or
        $evidence.Product -ne "MiaDock" -or
        $evidence.Version -ne $expectedVersion) {
        throw "Release evidence identity or schema is invalid."
    }

    $requiredGates = @(
        "FullEventSoak",
        "FullIdleSoak",
        "PackagedLifecycle",
        "RealDeviceRegression")
    foreach ($gate in $requiredGates) {
        $property = $evidence.PSObject.Properties[$gate]
        if ($null -eq $property -or
            $null -eq $property.Value -or
            $property.Value.Result -ne "Passed") {
            throw "Phase 11 gate '$gate' has not passed."
        }
    }

    if ($evidence.ApprovedForStoreCandidate -ne $true) {
        throw "Store candidate approval is missing from the Phase 11 evidence."
    }
}

function Assert-CleanWorkingTree {
    Push-Location $repositoryRoot
    try {
        $status = @(& git status --porcelain --untracked-files=all)
        if ($LASTEXITCODE -ne 0) {
            throw "Git status could not be read."
        }

        if ($status.Count -gt 0) {
            $script:workingTreeDirty = $true
            if (-not $AllowDirtyWorkingTree) {
                throw "Store packages must be built from a clean Git working tree."
            }
        }
    } finally {
        Pop-Location
    }
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
        if ($found.Count -gt 0 -and
            (Test-Path -LiteralPath $found[0] -PathType Leaf)) {
            return $found[0]
        }
    }

    throw (
        "mspdbcmf.exe was not found. Install the required Visual Studio/" +
        "Windows SDK symbol packaging component before building a Store candidate.")
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $stream = [System.IO.File]::OpenRead($Path)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return -join ($algorithm.ComputeHash($stream) | ForEach-Object {
            $_.ToString("x2")
        })
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Read-PackageIdentity {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive]$Archive
    )

    $manifestEntry = $Archive.Entries |
        Where-Object { $_.FullName -eq "AppxManifest.xml" } |
        Select-Object -First 1
    if ($null -eq $manifestEntry) {
        throw "AppxManifest.xml is missing from the packaged MSIX."
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
        $identity.Publisher -ne "CN=FAC642FD-F594-4E90-B1DB-38F94EA36BCA" -or
        $identity.Version -ne $expectedVersion -or
        $null -eq $startupTask) {
        throw "The Store package identity, publisher, version or StartupTask is invalid."
    }

    return $identity
}

function Assert-AndExtractStoreUpload {
    param(
        [Parameter(Mandatory)]
        [string]$UploadPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $uploadArchive = [System.IO.Compression.ZipFile]::OpenRead($UploadPath)
    try {
        $msixEntry = $uploadArchive.Entries |
            Where-Object { $_.FullName.EndsWith(".msix", [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
        $symbolEntry = $uploadArchive.Entries |
            Where-Object { $_.FullName.EndsWith(".appxsym", [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
        if ($null -eq $msixEntry) {
            throw "The .msixupload file does not contain an MSIX package."
        }
        if ($null -eq $symbolEntry) {
            throw "The .msixupload file does not contain an Appx symbol package."
        }

        $wackDirectory = Join-Path $ResultsDirectory "wack"
        New-Item -ItemType Directory -Path $wackDirectory -Force | Out-Null
        $wackPackagePath = Join-Path $wackDirectory (
            [System.IO.Path]::GetFileName($msixEntry.FullName))
        $input = $msixEntry.Open()
        $output = [System.IO.File]::Create($wackPackagePath)
        try {
            $input.CopyTo($output)
        } finally {
            $output.Dispose()
            $input.Dispose()
        }
    } finally {
        $uploadArchive.Dispose()
    }

    $packageArchive = [System.IO.Compression.ZipFile]::OpenRead($wackPackagePath)
    try {
        $identity = Read-PackageIdentity -Archive $packageArchive
        $entryNames = @($packageArchive.Entries | ForEach-Object {
            $_.FullName.Replace("\", "/")
        })
        $requiredEntries = @(
            "Assets/miadock-ringtone.wav",
            "Assets/StoreLogo.png",
            "Assets/Square44x44Logo.png",
            "Assets/Square150x150Logo.png",
            "MiaDock.App.exe",
            "MiaDock.App.dll",
            "MiaDock.Core.dll",
            "resources.pri",
            "Assets/Ionicons/LICENSE",
            "WinUIEx.dll",
            "WinUIEx.pri",
            "Assets/FluentIcons/window_24_regular.svg",
            "Assets/FluentIcons/settings_24_regular.svg",
            "Assets/FluentIcons/arrow_previous_24_regular.svg",
            "Assets/FluentIcons/play_24_regular.svg",
            "Assets/FluentIcons/pause_24_regular.svg",
            "Assets/FluentIcons/arrow_next_24_regular.svg",
            "Assets/FluentIcons/music_note_2_24_regular.svg",
            "Assets/FluentIcons/alert_24_regular.svg",
            "Assets/FluentIcons/desktop_24_regular.svg",
            "Assets/FluentIcons/eye_off_24_regular.svg",
            "Assets/FluentIcons/power_24_regular.svg")
        $missing = @($requiredEntries | Where-Object { $_ -notin $entryNames })
        if ($missing.Count -gt 0) {
            throw "Store package entries are missing: $($missing -join ', ')."
        }

        # Check the delivered assemblies, rather than the source tree or build log.
        # A stale package must not be delivered as the sponsored release.
        foreach ($check in @(
            @{ File = 'MiaDock.App.dll'; Required = 'MiamoAdsSlider' },
            @{ File = 'MiaDock.Core.dll'; Required = 'MiamoAdsClient' })) {
            $entry = $packageArchive.GetEntry($check.File)
            $stream = $entry.Open()
            $buffer = [IO.MemoryStream]::new()
            try {
                $stream.CopyTo($buffer)
                $metadata = [Text.Encoding]::UTF8.GetString($buffer.ToArray())
                if (-not $metadata.Contains($check.Required)) {
                    throw "Sponsored release implementation is missing from $($check.File)."
                }
            } finally {
                $buffer.Dispose()
                $stream.Dispose()
            }
        }
        # XBF files are embedded in resources.pri, not separate ZIP entries.
        # Assembly metadata alone did not catch the previous stale PRI payload.
        $makePri = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin/10.0.26100.0/x64/makepri.exe'
        if (-not (Test-Path -LiteralPath $makePri)) { throw 'Windows SDK makepri.exe is required for packaged XAML verification.' }
        $resourceDirectory = Join-Path $ResultsDirectory 'build/resource-verification'
        New-Item -ItemType Directory -Path $resourceDirectory -Force | Out-Null
        $priPath = Join-Path $resourceDirectory 'resources.pri'
        $dumpPath = Join-Path $resourceDirectory 'resources.xml'
        $priInput = $packageArchive.GetEntry('resources.pri').Open()
        $priOutput = [IO.File]::Create($priPath)
        try { $priInput.CopyTo($priOutput) } finally { $priInput.Dispose(); $priOutput.Dispose() }
        & $makePri dump /if $priPath /of $dumpPath /o *> (Join-Path $resourceDirectory 'makepri.log')
        if ($LASTEXITCODE -ne 0) { throw 'Packaged XAML resource map could not be inspected.' }
        [xml]$resources = Get-Content -LiteralPath $dumpPath -Raw
        foreach ($name in @('MiamoAdsSlider.xbf', 'SettingsWindow.xbf')) {
            if ($null -eq $resources.SelectSingleNode("//NamedResource[@name='$name']")) {
                throw "Sponsored release XAML resource is missing from the packaged PRI: $name"
            }
        }
    } finally {
        $packageArchive.Dispose()
    }

    return [ordered]@{
        UploadPath = $UploadPath
        UploadSha256 = Get-Sha256 -Path $UploadPath
        WackPackagePath = $wackPackagePath
        WackPackageSha256 = Get-Sha256 -Path $wackPackagePath
        IdentityName = $identity.Name
        Publisher = $identity.Publisher
        Version = $identity.Version
        PackagedAdsImplementationVerified = $true
        PackagedAdsXamlVerified = $true
    }
}

Assert-ReleaseEvidence
Assert-CleanWorkingTree
$symbolTool = Find-Mspdbcmf
$env:PATH = "$(Split-Path -Parent $symbolTool);$env:PATH"
$symbolToolForMsBuild = $symbolTool.Replace("\", "/")

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$packageDirectory = Join-Path $ResultsDirectory "package"
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
$packageDirectoryForMsBuild = $packageDirectory.Replace("\", "/") + "/"

Push-Location $repositoryRoot
try {
    if (-not $SkipRestore) {
        Invoke-DotNet -Arguments @("restore", "MiaDock.sln", "-p:Platform=x64")
    }

    if ($AutomatedTestResultsDirectory) {
        if (-not $AllowUnverifiedCandidate) { throw 'External automated tests require -AllowUnverifiedCandidate; device gates remain pending.' }
        foreach ($name in @('measurement.trx', 'windows.trx', 'winui.trx')) {
            $testPath = Join-Path $AutomatedTestResultsDirectory $name
            [xml]$tests = Get-Content -LiteralPath $testPath -Raw
            $counters = $tests.TestRun.ResultSummary.Counters
            if ([int]$counters.failed -ne 0 -or [int]$counters.passed -le 0) { throw "Automated tests did not pass: $testPath" }
        }
    } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $validationScript -SkipRestore -LaunchSmokeTest
        if ($LASTEXITCODE -ne 0) { throw "Release validation failed before Store packaging." }
    }

    $buildOutput = (Join-Path $ResultsDirectory 'build/bin').Replace('\', '/') + '/'
    $isolatedBuildRoot = (Join-Path $ResultsDirectory ('build/session-' + [Guid]::NewGuid().ToString('N'))).Replace('\', '/') + '/'
    New-Item -ItemType Directory -Path (Join-Path $ResultsDirectory 'build') -Force | Out-Null
    $msbuild = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe'
    if (-not (Test-Path -LiteralPath $msbuild)) { throw 'Visual Studio MSBuild is required for WinUI packaging.' }
    $buildArguments = @(
        $applicationProject,
        "/p:Configuration=Release", "/p:SelfContained=true", "/p:NuGetAudit=false",
        "/p:OutputPath=$buildOutput",
        "/p:MiaDockBuildRoot=$isolatedBuildRoot",
        "-p:Platform=x64",
        "-p:RuntimeIdentifier=win-x64",
        "-p:BuildMsix=true",
        "-p:UapAppxPackageBuildMode=StoreUpload",
        "-p:AppxPackageSigningEnabled=false",
        "-p:AppxSymbolPackageEnabled=true",
        "-p:PdbCmfx64ExeFullPath=$symbolToolForMsBuild",
        "-p:MsPdbCmfExeFullpath=$symbolToolForMsBuild",
        "-p:AppxPackageDir=$packageDirectoryForMsBuild",
        "/v:minimal", "/nologo")
    # Fresh intermediates require their own assets even with -SkipRestore.
    $buildArguments += '/restore'
    $buildArguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'build/msbuild-arguments.json') -Encoding utf8
    & $msbuild @buildArguments *> (Join-Path $ResultsDirectory 'build/store-build.log')
    if ($LASTEXITCODE -ne 0) { throw "Store build failed; inspect $ResultsDirectory/build/store-build.log" }
} finally {
    Pop-Location
}

$upload = Get-ChildItem -LiteralPath $packageDirectory -Recurse -Filter "*.msixupload" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $upload) {
    throw "The Store .msixupload artifact was not produced."
}

$packageSummary = Assert-AndExtractStoreUpload -UploadPath $upload.FullName
$deliveryPath = Join-Path $ResultsDirectory "MiaDock-$expectedVersion-x64.msixupload"
Copy-Item -LiteralPath $upload.FullName -Destination $deliveryPath -Force
$packageSummary.UploadPath = $deliveryPath
$gitCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$summary = [ordered]@{
    SchemaVersion = 1
    Product = "MiaDock"
    Version = $expectedVersion
    CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    GitCommit = $gitCommit
    ReleaseEvidence = $releaseEvidenceStatus
    WorkingTreeDirty = $workingTreeDirty
    AutomatedTestResultsDirectory = $AutomatedTestResultsDirectory
    DeviceAcceptance = 'Pending: full-app baseline comparison, real 30-minute heavy use, real 8-hour idle, physical device checks, WACK'
    Package = $packageSummary
}
$summaryPath = Join-Path $ResultsDirectory "store-package-summary.json"
$summary | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "Store candidate: $($upload.FullName)"
Write-Host "WACK package: $($packageSummary.WackPackagePath)"
Write-Host "Summary: $summaryPath"
