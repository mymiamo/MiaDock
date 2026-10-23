[CmdletBinding()]
param([switch]$Baseline, [switch]$Measure, [ValidatePattern('^[a-z0-9-]+$')][string]$RunName)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$label = if ($Baseline) { 'baseline-2616398' } else { 'candidate' }
if ($RunName) { $label = $RunName }
$evidence = Join-Path $root "artifacts/validation/1.7.3.0/native/$label"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$msbuild = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe'
$arguments = @((Join-Path $PSScriptRoot 'NativeMotion/NativeMotion.csproj'), '/restore', '/p:NuGetAudit=false', '/p:Configuration=Release', '/p:Platform=x64', '/v:minimal', '/nologo', "/p:OutputPath=$evidence/bin/")
if ($Baseline) {
    $source = Join-Path $evidence 'source'
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    foreach ($name in @('IslandAnimationCoordinator', 'IslandBoundsAnimator', 'ToolkitAnimationFactory', 'IslandAnimationProfile', 'IslandVisualMetrics', 'BoundsEasingProfile', 'MotionSemantics', 'IIslandAnimationCoordinator')) {
        $content = & git -C $root show "2616398:src/MiaDock.App/Animations/$name.cs"
        if ($LASTEXITCODE -ne 0) { throw "Cannot read baseline $name" }
        [IO.File]::WriteAllLines((Join-Path $source "$name.cs"), $content)
    }
    $arguments += "/p:AnimationSourceRoot=$source"
}
& $msbuild @arguments *> (Join-Path $evidence 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Native build failed: $evidence/build.log" }
@{
    GitCommit = (& git -C $root rev-parse HEAD).Trim()
    AnimationSource = if ($Baseline) { '2616398' } else { 'current working tree' }
    SharedHost = 'current Core/UI/Windows libraries; not a full-app baseline'
    Measure = [bool]$Measure
    CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'provenance.json') -Encoding utf8
$result = Join-Path $evidence 'result.json'
$runArguments = @('"' + $result + '"')
if ($Baseline) { $runArguments += '--baseline' }
if ($Measure) { $runArguments += '--measure' }
$exe = Join-Path $evidence 'bin/NativeMotion.exe'
$process = Start-Process -FilePath $exe -ArgumentList $runArguments -PassThru -WindowStyle Hidden
Write-Host "Native $label PID $($process.Id); result: $result"
$timeout = if ($Measure) { 300000 } else { 90000 }
if (-not $process.WaitForExit($timeout)) {
    Stop-Process -Id $process.Id -Force
    throw 'Native fixture timed out (only its owned process was stopped).'
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $result)) { throw 'Native fixture failed; inspect result.json and Windows events.' }
$summary = Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
if (-not $summary.Passed) { throw "Native assertions failed: $($summary.Failures -join '; ')" }
Write-Host "Native $label passed; actions: $($summary.Actions)."
