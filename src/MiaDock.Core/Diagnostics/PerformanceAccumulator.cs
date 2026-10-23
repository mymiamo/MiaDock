namespace MiaDock.Core.Diagnostics;

public enum PerformanceScenario { Idle, Media, Spectrum }
public sealed record ProcessCounters(TimeSpan ProcessorTime, long WorkingSetBytes, long PrivateBytes);
public sealed record PerformanceSample(double CpuPercent, long WorkingSetBytes, long PrivateBytes);
public sealed record ScenarioPerformance(PerformanceScenario Scenario, int SampleCount, double MeanCpuPercent,
    double PeakCpuPercent, long LatestWorkingSetBytes, long PeakWorkingSetBytes, long LatestPrivateBytes);
public sealed record ApplicationPerformanceSnapshot(double? DockReadyMilliseconds, int LogicalProcessors,
    int SampleIntervalSeconds, IReadOnlyList<ScenarioPerformance> Scenarios)
{
    public AnimationPerformanceSummary? Animation { get; init; }
}

public sealed class PerformanceAccumulator(int logicalProcessors, int capacityPerScenario = 180)
{
    private readonly object _gate = new();
    private readonly Dictionary<PerformanceScenario, Queue<PerformanceSample>> _samples = new();
    private ProcessCounters? _previous;
    private PerformanceScenario _previousScenario;

    public void Add(PerformanceScenario scenario, ProcessCounters counters, TimeSpan elapsed)
    {
        if (logicalProcessors <= 0 || capacityPerScenario <= 0) throw new InvalidOperationException("Invalid sampling configuration.");
        lock (_gate)
        {
            var previous = _previous;
            _previous = counters;
            var stableScenario = _previousScenario == scenario;
            _previousScenario = scenario;
            if (previous is null || !stableScenario || elapsed <= TimeSpan.Zero || counters.ProcessorTime < previous.ProcessorTime) return;
            var cpu = Math.Clamp((counters.ProcessorTime - previous.ProcessorTime).TotalSeconds /
                elapsed.TotalSeconds / logicalProcessors * 100, 0, 100);
            if (!_samples.TryGetValue(scenario, out var samples)) _samples[scenario] = samples = new();
            samples.Enqueue(new(cpu, counters.WorkingSetBytes, counters.PrivateBytes));
            while (samples.Count > capacityPerScenario) samples.Dequeue();
        }
    }

    public ApplicationPerformanceSnapshot Snapshot(double? dockReadyMilliseconds = null)
    {
        lock (_gate)
        {
            var summaries = Enum.GetValues<PerformanceScenario>().Select(scenario =>
            {
                if (!_samples.TryGetValue(scenario, out var samples) || samples.Count == 0)
                    return new ScenarioPerformance(scenario, 0, 0, 0, 0, 0, 0);
                var latest = samples.Last();
                return new ScenarioPerformance(scenario, samples.Count, samples.Average(item => item.CpuPercent),
                    samples.Max(item => item.CpuPercent), latest.WorkingSetBytes, samples.Max(item => item.WorkingSetBytes), latest.PrivateBytes);
            }).ToArray();
            return new(dockReadyMilliseconds, logicalProcessors, 5, summaries);
        }
    }
}
