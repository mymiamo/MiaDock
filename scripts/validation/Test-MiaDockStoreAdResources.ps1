[CmdletBinding()]
param(
    [string]$IncompleteUploadPath,
    [string]$ResultsDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $IncompleteUploadPath) {
    $IncompleteUploadPath = Join-Path $root 'artifacts/release/1.7.0.0/MiaDock-1.7.0.0-x64.msixupload'
}
if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $root 'artifacts/validation/1.7.3.0/negative-package'
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

# Exercise the production archive validator against the actual earlier delivery.
# Import definitions only; do not execute packaging, restores or delivery copies.
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $root 'scripts/release/Build-MiaDockStorePackage.ps1'),
    [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'Packaging script has parsing errors.' }
foreach ($name in @('Get-Sha256', 'Read-PackageIdentity', 'Assert-AndExtractStoreUpload')) {
    $definition = $ast.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $false)
    if ($null -eq $definition) { throw "Missing validator function: $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}
$expectedVersion = '1.7.0.0' # Historical fixture identity; not the current release.
$failure = $null
try { Assert-AndExtractStoreUpload -UploadPath $IncompleteUploadPath | Out-Null }
catch { $failure = $_.Exception.Message }
if ($failure -notlike '*XAML resource is missing*MiamoAdsSlider.xbf*') {
    throw "Expected the old delivery to fail the packaged XAML check. Actual: $failure"
}
[ordered]@{
    Passed = $true
    IncompleteUploadSha256 = Get-Sha256 -Path $IncompleteUploadPath
    RejectedBy = 'PackagedXamlResourceCheck'
    Reason = $failure
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'result.json')
Write-Host 'PASS: production validator rejects the earlier package with missing advertising XAML.'
