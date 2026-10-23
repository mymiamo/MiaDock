using MiaDock.Core.Diagnostics;
using System.Text.Json;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class AnimationMeasurementTests
{
    [TestMethod]
    public void OptionalSummaryPreservesExistingSnapshotConstructionAndDeconstruction()
    {
        var original = new ApplicationPerformanceSnapshot(123, 8, 5, []);
        var (ready, processors, interval, scenarios) = original;
        Assert.AreEqual(123d, ready);
        Assert.AreEqual(8, processors);
        Assert.AreEqual(5, interval);
        Assert.IsEmpty(scenarios);
        Assert.IsNull(original.Animation);
        var measurement = new AnimationMeasurement();
        measurement.Start(TimeSpan.Zero);
        var extended = original with { Animation = measurement.Snapshot(TimeSpan.FromSeconds(1)) };
        var roundtrip = JsonSerializer.Deserialize<ApplicationPerformanceSnapshot>(JsonSerializer.Serialize(extended))!;
        Assert.AreEqual(original.DockReadyMilliseconds, roundtrip.DockReadyMilliseconds);
        Assert.AreEqual(1d, roundtrip.Animation!.ElapsedSeconds);
    }
    [TestMethod]
    public void NearestRankPercentilesAndDelayedIntervalsHaveExplicitUnits()
    {
        var measurement = new AnimationMeasurement();
        measurement.Start(TimeSpan.Zero);
        measurement.Frame(TimeSpan.Zero);
        var now = TimeSpan.Zero;
        for (var i = 1; i <= 100; i++) measurement.Frame(now += TimeSpan.FromMilliseconds(i));
        var summary = measurement.Snapshot(now);
        Assert.AreEqual(95d, summary.P95FrameIntervalMilliseconds);
        Assert.AreEqual(99d, summary.P99FrameIntervalMilliseconds);
        Assert.AreEqual(75, summary.DelayedFrames);
        Assert.AreEqual(100, summary.FrameIntervals);
    }

    [TestMethod]
    public void DeadlineStopsWithoutRenderingAndRestartRejectsOldCompletions()
    {
        var measurement = new AnimationMeasurement();
        measurement.Start(TimeSpan.Zero);
        var first = measurement.Session;
        measurement.Tick(TimeSpan.FromSeconds(61));
        Assert.IsFalse(measurement.IsRunning);
        Assert.AreEqual(60d, measurement.Snapshot(TimeSpan.FromSeconds(65)).ElapsedSeconds);
        measurement.Start(TimeSpan.FromSeconds(65));
        measurement.Transition(first, "old", TimeSpan.Zero, TimeSpan.FromSeconds(66), false);
        measurement.Transition(measurement.Session, "module", TimeSpan.FromSeconds(65), TimeSpan.FromSeconds(66), true);
        var result = measurement.Snapshot(TimeSpan.FromSeconds(66));
        Assert.AreEqual(0, result.CompletedTransitions);
        Assert.AreEqual(1, result.CancelledTransitions);
        Assert.AreEqual(1000d, result.Transitions.Single().Milliseconds);
    }

    [TestMethod]
    public void SamplesAreBoundedAndStopIsIdempotent()
    {
        var measurement = new AnimationMeasurement();
        measurement.Start(TimeSpan.Zero);
        for (var i = 0; i < 20000; i++) measurement.Frame(TimeSpan.FromMilliseconds(i));
        for (var i = 0; i < 2000; i++) measurement.Transition(measurement.Session, "refresh", TimeSpan.Zero, TimeSpan.FromSeconds(20), false);
        measurement.Stop(TimeSpan.FromSeconds(20));
        measurement.Stop(TimeSpan.FromSeconds(30));
        measurement.Frame(TimeSpan.FromSeconds(31));
        var result = measurement.Snapshot(TimeSpan.FromSeconds(31));
        Assert.AreEqual(AnimationMeasurement.FrameCapacity, result.FrameIntervals);
        Assert.AreEqual(19999 - AnimationMeasurement.FrameCapacity, result.DroppedFrameSamples);
        Assert.AreEqual(AnimationMeasurement.TransitionCapacity, result.Transitions.Count);
        Assert.AreEqual(2000, result.CompletedTransitions);
        Assert.AreEqual(20d, result.ElapsedSeconds);
    }

    [TestMethod]
    public void EmptyAndDuplicateFramesAreNotInventedAsGpuFps()
    {
        var measurement = new AnimationMeasurement();
        measurement.Frame(TimeSpan.Zero);
        measurement.Start(TimeSpan.Zero);
        measurement.Frame(TimeSpan.Zero); measurement.Frame(TimeSpan.Zero);
        var result = measurement.Snapshot(TimeSpan.Zero);
        Assert.AreEqual(0, result.FrameIntervals);
        Assert.AreEqual(0d, result.P99FrameIntervalMilliseconds);
    }
}
