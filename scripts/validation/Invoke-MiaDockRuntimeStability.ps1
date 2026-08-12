[CmdletBinding()]
param(
    [ValidateRange(10, 28800)]
    [int]$DurationSeconds = 60,

    [ValidateRange(1, 30)]
    [int]$SampleIntervalSeconds = 2,

    [ValidateRange(0.1, 100)]
    [double]$MaximumAverageCpuPercent = 1,

    [ValidateRange(1, 1024)]
    [int]$MaximumWorkingSetGrowthMb = 20,

    [ValidateRange(0, 4096)]
    [int]$MaximumHandleGrowth = 64,

    [ValidateRange(0, 256)]
    [int]$MaximumThreadGrowth = 8,

    [string]$ExecutablePath,

    [string]$ResultsDirectory,

    [switch]$UseExistingProcess,

    [switch]$KeepRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $repositoryRoot `
        "src\MiaDock.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\MiaDock.App.exe"
}
$ExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "MiaDock executable was not found: $ExecutablePath"
}

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot "artifacts\validation\1.4.0.0\phase5-runtime"
}
$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$existing = @(Get-Process -Name "MiaDock.App" -ErrorAction SilentlyContinue)
$ownsProcess = $false
if ($existing.Count -gt 0) {
    if (-not $UseExistingProcess) {
        throw "MiaDock is already running. Exit it or pass -UseExistingProcess for read-only monitoring."
    }
    if ($existing.Count -ne 1) {
        throw "Expected one MiaDock process but found $($existing.Count)."
    }
    $process = $existing[0]
} else {
    $process = Start-Process -FilePath $ExecutablePath -PassThru -WindowStyle Hidden
    $ownsProcess = $true
}

$samples = [System.Collections.Generic.List[object]]::new()
$logicalProcessorCount = [Environment]::ProcessorCount
$warmupSeconds = [Math]::Min(5, [Math]::Max(2, [int]($DurationSeconds / 5)))

try {
    Start-Sleep -Seconds $warmupSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "MiaDock exited during the warm-up period with code $($process.ExitCode)."
    }

    $initialWorkingSet = $process.WorkingSet64
    $initialPrivateMemory = $process.PrivateMemorySize64
    $initialHandleCount = $process.HandleCount
    $initialThreadCount = $process.Threads.Count
    $previousCpu = $process.TotalProcessorTime
    $previousSampleAt = [DateTimeOffset]::UtcNow
    $deadline = $previousSampleAt.AddSeconds($DurationSeconds)

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $SampleIntervalSeconds
        $process.Refresh()
        if ($process.HasExited) {
            throw "MiaDock exited during the stability run with code $($process.ExitCode)."
        }

        $sampledAt = [DateTimeOffset]::UtcNow
        $elapsed = ($sampledAt - $previousSampleAt).TotalSeconds
        $cpuDelta = ($process.TotalProcessorTime - $previousCpu).TotalSeconds
        $cpuPercent = if ($elapsed -gt 0) {
            100 * $cpuDelta / ($elapsed * $logicalProcessorCount)
        } else {
            0
        }

        $samples.Add([pscustomobject]@{
            sampledAtUtc = $sampledAt.ToString("O")
            cpuPercent = [Math]::Round($cpuPercent, 3)
            workingSetBytes = $process.WorkingSet64
            privateMemoryBytes = $process.PrivateMemorySize64
            handleCount = $process.HandleCount
            threadCount = $process.Threads.Count
            responding = $process.Responding
        })
        $previousCpu = $process.TotalProcessorTime
        $previousSampleAt = $sampledAt
    }

    $process.Refresh()
    $averageCpu = if ($samples.Count -gt 0) {
        ($samples | Measure-Object -Property cpuPercent -Average).Average
    } else {
        0
    }
    $workingSetGrowth = $process.WorkingSet64 - $initialWorkingSet
    $privateMemoryGrowth = $process.PrivateMemorySize64 - $initialPrivateMemory
    $handleGrowth = $process.HandleCount - $initialHandleCount
    $threadGrowth = $process.Threads.Count - $initialThreadCount
    $notRespondingSamples = @($samples | Where-Object { -not $_.responding }).Count
    $passed = $averageCpu -le $MaximumAverageCpuPercent -and
        $workingSetGrowth -le ($MaximumWorkingSetGrowthMb * 1MB) -and
        $handleGrowth -le $MaximumHandleGrowth -and
        $threadGrowth -le $MaximumThreadGrowth -and
        $notRespondingSamples -eq 0

    $result = [ordered]@{
        passed = $passed
        startedAtUtc = $samples[0].sampledAtUtc
        durationSeconds = $DurationSeconds
        averageCpuPercent = [Math]::Round($averageCpu, 3)
        maximumCpuPercent = [Math]::Round(
            ($samples | Measure-Object -Property cpuPercent -Maximum).Maximum,
            3)
        initialWorkingSetBytes = $initialWorkingSet
        finalWorkingSetBytes = $process.WorkingSet64
        workingSetGrowthBytes = $workingSetGrowth
        initialPrivateMemoryBytes = $initialPrivateMemory
        finalPrivateMemoryBytes = $process.PrivateMemorySize64
        privateMemoryGrowthBytes = $privateMemoryGrowth
        initialHandleCount = $initialHandleCount
        finalHandleCount = $process.HandleCount
        handleGrowth = $handleGrowth
        initialThreadCount = $initialThreadCount
        finalThreadCount = $process.Threads.Count
        threadGrowth = $threadGrowth
        notRespondingSamples = $notRespondingSamples
        thresholds = [ordered]@{
            maximumAverageCpuPercent = $MaximumAverageCpuPercent
            maximumWorkingSetGrowthMb = $MaximumWorkingSetGrowthMb
            maximumHandleGrowth = $MaximumHandleGrowth
            maximumThreadGrowth = $MaximumThreadGrowth
        }
        samples = $samples
    }

    $resultPath = Join-Path $ResultsDirectory (
        "runtime-{0}.json" -f [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss"))
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8
    Write-Host "Runtime stability result: $resultPath"
    Write-Host "Average CPU: $([Math]::Round($averageCpu, 3))%"
    Write-Host "Working-set growth: $([Math]::Round($workingSetGrowth / 1MB, 2)) MB"
    Write-Host "Private-memory growth: $([Math]::Round($privateMemoryGrowth / 1MB, 2)) MB"
    Write-Host "Handle growth: $handleGrowth"
    Write-Host "Thread growth: $threadGrowth"
    Write-Host "Not-responding samples: $notRespondingSamples"

    if (-not $passed) {
        throw "Runtime stability thresholds were not met."
    }
}
finally {
    if ($ownsProcess -and -not $KeepRunning -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
}
