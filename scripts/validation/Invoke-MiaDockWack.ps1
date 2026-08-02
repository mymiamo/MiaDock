[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [string]$ReportPath,

    [string]$AppCertPath =
        "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
$PackagePath = [System.IO.Path]::GetFullPath($PackagePath)
$AppCertPath = [System.IO.Path]::GetFullPath($AppCertPath)

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "WACK package was not found: $PackagePath"
}
if ([System.IO.Path]::GetExtension($PackagePath) -notin ".msix", ".appx") {
    throw "WACK must receive the packaged .msix or .appx, not a .msixupload file."
}
if (-not (Test-Path -LiteralPath $AppCertPath -PathType Leaf)) {
    throw "appcert.exe was not found. Install the Windows App Certification Kit."
}

$principal = [Security.Principal.WindowsPrincipal]::new(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "WACK must be run from an elevated PowerShell session."
}
if (-not [Environment]::UserInteractive) {
    throw "WACK requires an active interactive user session."
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $ReportPath = Join-Path $repositoryRoot (
        "artifacts\validation\1.2.1\wack\wack-$timestamp.xml")
}

$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
$reportDirectory = Split-Path -Parent $ReportPath
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

& $AppCertPath reset
if ($LASTEXITCODE -ne 0) {
    throw "appcert reset failed with exit code $LASTEXITCODE."
}

& $AppCertPath test `
    -appxpackagepath $PackagePath `
    -reportoutputpath $ReportPath
if ($LASTEXITCODE -ne 0) {
    throw "WACK failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "WACK did not create a report."
}

$reportText = Get-Content -LiteralPath $ReportPath -Raw
$failurePatterns = @(
    '(?i)OVERALL_RESULT\s*=\s*["'']FAIL',
    '(?i)<OVERALL_RESULT>\s*FAIL\s*</OVERALL_RESULT>',
    '(?i)<RESULT>\s*FAIL\s*</RESULT>')
foreach ($pattern in $failurePatterns) {
    if ($reportText -match $pattern) {
        throw "WACK report contains a failed result. Review: $ReportPath"
    }
}

$summary = [ordered]@{
    SchemaVersion = 1
    Product = "MiaDock"
    Version = "1.2.1.0"
    CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    Result = "PassedNoDetectedFailures"
    PackageSha256 = (
        Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
    ReportPath = $ReportPath
}
$summaryPath = [System.IO.Path]::ChangeExtension($ReportPath, ".summary.json")
$summary | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "WACK report: $ReportPath"
Write-Host "WACK summary: $summaryPath"
