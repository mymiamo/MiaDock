[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $PSScriptRoot 'NativeAds/NativeAds.csproj'
$results = Join-Path $root 'artifacts/validation/miamo-ads'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$msbuild = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe'
& $msbuild $project /restore /p:NuGetAudit=false /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo *> (Join-Path $results 'native-build.log')
if ($LASTEXITCODE -ne 0) { throw "Native advertising build failed: $results/native-build.log" }
$output = Join-Path $results 'native.json'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$exe = Join-Path $PSScriptRoot 'NativeAds/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NativeAds.exe'
$process = Start-Process -FilePath $exe -ArgumentList ('"' + $output + '"') -PassThru -WindowStyle Hidden
Write-Host "Native advertising fixture PID: $($process.Id)"
if (-not $process.WaitForExit(120000)) {
    Stop-Process -Id $process.Id -Force
    throw 'Owned native advertising fixture timed out.'
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "Native advertising fixture failed: $output" }
$result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
if (-not $result.Passed) { throw "Native advertising checks failed: $($result.Failure)" }
Write-Host "Passed $($result.Checks.Count) native checks: $output"
