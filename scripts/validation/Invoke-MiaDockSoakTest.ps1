[CmdletBinding()]
param(
    [ValidateSet("all", "events", "idle", "fullscreen")]
    [string]$Profile = "all",

    [ValidateRange(0.000001, 1.0)]
    [double]$Scale = 1.0,

    [switch]$AllowScaled,

    [string]$ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Scale -lt 1.0 -and -not $AllowScaled) {
    throw "Scaled runs require -AllowScaled so they cannot be mistaken for full-duration validation."
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot "artifacts\validation\1.4.2.0\phase5-soak"
}

$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$eventDuration = [TimeSpan]::FromTicks(
    [long]([TimeSpan]::FromMinutes(30).Ticks * $Scale))
$idleDuration = [TimeSpan]::FromTicks(
    [long]([TimeSpan]::FromHours(8).Ticks * $Scale))
$runKind = if ($Profile -eq "fullscreen") {
    "virtual-full-horizon"
} elseif ($Scale -eq 1.0) {
    "full-duration"
} else {
    "scaled"
}
$resultPath = Join-Path $ResultsDirectory "soak-$Profile-$runKind.trx"
$previousProfile = [Environment]::GetEnvironmentVariable("MIADOCK_SOAK_PROFILE")
$previousScale = [Environment]::GetEnvironmentVariable("MIADOCK_SOAK_SCALE")

Write-Host "Profile: $Profile"
Write-Host "Run kind: $runKind"
Write-Host "Event duration: $eventDuration"
Write-Host "Idle duration: $idleDuration"

try {
    [Environment]::SetEnvironmentVariable("MIADOCK_SOAK_PROFILE", $Profile)
    [Environment]::SetEnvironmentVariable(
        "MIADOCK_SOAK_SCALE",
        $Scale.ToString([System.Globalization.CultureInfo]::InvariantCulture))

    Push-Location $repositoryRoot
    try {
        if ($Profile -in @("all", "events", "idle")) {
        & dotnet test `
            "tests\MiaDock.Core.Tests\MiaDock.Core.Tests.csproj" `
            -c Release `
            -p:Platform=x64 `
            --no-restore `
            --filter "TestCategory=Soak" `
            --logger "trx;LogFileName=$resultPath" `
            --logger "console;verbosity=normal"
        if ($LASTEXITCODE -ne 0) {
            throw "Soak tests failed with exit code $LASTEXITCODE."
        }
        }

        if ($Profile -in @("all", "fullscreen")) {
            $fullscreenResultPath = Join-Path $ResultsDirectory (
                "soak-fullscreen-$runKind.trx")
            & dotnet test `
                "tests\MiaDock.Platform.Windows.Tests\MiaDock.Platform.Windows.Tests.csproj" `
                -c Release `
                -p:Platform=x64 `
                --no-restore `
                --filter "TestCategory=FullscreenSoak" `
                --logger "trx;LogFileName=$fullscreenResultPath" `
                --logger "console;verbosity=normal"
            if ($LASTEXITCODE -ne 0) {
                throw "Fullscreen soak tests failed with exit code $LASTEXITCODE."
            }
        }
    } finally {
        Pop-Location
    }
} finally {
    [Environment]::SetEnvironmentVariable("MIADOCK_SOAK_PROFILE", $previousProfile)
    [Environment]::SetEnvironmentVariable("MIADOCK_SOAK_SCALE", $previousScale)
}

Write-Host "Soak results directory: $ResultsDirectory"
