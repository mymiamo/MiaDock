[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$results = Join-Path $root 'artifacts/validation/dock-indicators'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$msbuild = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'NativeIndicators/NativeIndicators.csproj') /restore /p:NuGetAudit=false /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo *> (Join-Path $results 'native-build.log')
if ($LASTEXITCODE -ne 0) { throw "Native indicator build failed: $results/native-build.log" }
$output = Join-Path $results 'native.json'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$exe = Join-Path $PSScriptRoot 'NativeIndicators/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NativeIndicators.exe'
$process = Start-Process -FilePath $exe -ArgumentList ('"' + $output + '"') -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { Stop-Process -Id $process.Id -Force; throw 'Owned native fixture timed out.' }
if (-not (Test-Path -LiteralPath $output)) { throw 'Native indicator fixture did not produce a result.' }
$result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
if (-not $result.Passed) { throw $result.Failure }
Write-Host "Passed $($result.Checks.Count) native indicator checks: $output"
