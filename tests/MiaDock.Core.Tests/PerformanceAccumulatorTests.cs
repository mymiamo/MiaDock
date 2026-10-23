using MiaDock.Core.Diagnostics;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class PerformanceAccumulatorTests
{
    [TestMethod]
    public void CpuIsNormalizedAcrossProcessors_AndHistoryIsBounded()
    {
        var accumulator = new PerformanceAccumulator(4, 2);
        for (var index = 0; index < 5; index++)
            accumulator.Add(PerformanceScenario.Idle, new(TimeSpan.FromSeconds(index), 1000 + index, 2000), TimeSpan.FromSeconds(1));
        var summary = accumulator.Snapshot(120).Scenarios[0];
        Assert.AreEqual(2, summary.SampleCount);
        Assert.AreEqual(25, summary.MeanCpuPercent);
        Assert.AreEqual(1004, summary.LatestWorkingSetBytes);
        Assert.AreEqual(120, accumulator.Snapshot(120).DockReadyMilliseconds);
    }

    [TestMethod]
    public void ScenarioTransitionsAndCounterResetsAreNotReportedAsMeasurements()
    {
        var accumulator = new PerformanceAccumulator(1);
        accumulator.Add(PerformanceScenario.Idle, new(TimeSpan.Zero, 1, 1), TimeSpan.FromSeconds(5));
        accumulator.Add(PerformanceScenario.Spectrum, new(TimeSpan.FromSeconds(2), 1, 1), TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, accumulator.Snapshot().Scenarios[2].SampleCount);
        accumulator.Add(PerformanceScenario.Spectrum, new(TimeSpan.Zero, 1, 1), TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, accumulator.Snapshot().Scenarios[2].SampleCount);
        accumulator.Add(PerformanceScenario.Spectrum, new(TimeSpan.FromSeconds(1), 1, 1), TimeSpan.FromSeconds(5));
        Assert.AreEqual(20, accumulator.Snapshot().Scenarios[2].MeanCpuPercent);
        Assert.AreEqual(0, accumulator.Snapshot().Scenarios[1].SampleCount);
    }
}
