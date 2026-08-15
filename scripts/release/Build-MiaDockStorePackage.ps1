[CmdletBinding()]
param(
    [Alias("Phase11EvidencePath")]
    [string]$ReleaseEvidencePath,

    [string]$MspdbcmfPath,

    [string]$ResultsDirectory,

    [switch]$SkipRestore,

    [switch]$AllowUnverifiedCandidate,

    [switch]$AllowDirtyWorkingTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$expectedVersion = "1.5.3.0"
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
$applicationProject = Join-Path $repositoryRoot "src\MiaDock.App\MiaDock.App.csproj"
$validationScript = Join-Path $repositoryRoot (
    "scripts\validation\Invoke-MiaDockReleaseValidation.ps1")

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $ResultsDirectory = Join-Path $repositoryRoot (
        "artifacts\release\1.5.3.0\candidate-$timestamp")
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
            "MiaDock.App.exe")
        $missing = @($requiredEntries | Where-Object { $_ -notin $entryNames })
        if ($missing.Count -gt 0) {
            throw "Store package entries are missing: $($missing -join ', ')."
        }
    } finally {
        $packageArchive.Dispose()
    }

    return [ordered]@{
        UploadPath = $UploadPath
        UploadSha256 = (Get-FileHash -LiteralPath $UploadPath -Algorithm SHA256).Hash
        WackPackagePath = $wackPackagePath
        WackPackageSha256 = (
            Get-FileHash -LiteralPath $wackPackagePath -Algorithm SHA256).Hash
        IdentityName = $identity.Name
        Publisher = $identity.Publisher
        Version = $identity.Version
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

    & powershell -NoProfile -ExecutionPolicy Bypass -File $validationScript `
        -SkipRestore -LaunchSmokeTest -StopRunningApp
    if ($LASTEXITCODE -ne 0) {
        throw "Release validation failed before Store packaging."
    }

    Invoke-DotNet -Arguments @(
        "build",
        $applicationProject,
        "-c", "Release",
        "-p:Platform=x64",
        "-p:RuntimeIdentifier=win-x64",
        "-p:BuildMsix=true",
        "-p:UapAppxPackageBuildMode=StoreUpload",
        "-p:AppxPackageSigningEnabled=false",
        "-p:AppxSymbolPackageEnabled=true",
        "-p:PdbCmfx64ExeFullPath=$symbolToolForMsBuild",
        "-p:MsPdbCmfExeFullpath=$symbolToolForMsBuild",
        "-p:AppxPackageDir=$packageDirectoryForMsBuild",
        "--no-restore",
        "-v:minimal")
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
$gitCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$summary = [ordered]@{
    SchemaVersion = 1
    Product = "MiaDock"
    Version = $expectedVersion
    CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    GitCommit = $gitCommit
    ReleaseEvidence = $releaseEvidenceStatus
    WorkingTreeDirty = $workingTreeDirty
    Package = $packageSummary
}
$summaryPath = Join-Path $ResultsDirectory "store-package-summary.json"
$summary | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "Store candidate: $($upload.FullName)"
Write-Host "WACK package: $($packageSummary.WackPackagePath)"
Write-Host "Summary: $summaryPath"
