using MiaDock.Platform.Windows.Connectivity;

namespace MiaDock.Platform.Windows.Tests.Connectivity;

[TestClass]
public sealed class NetworkRateCalculatorTests
{
    [TestMethod]
    public void Add_UsesCounterDeltaAndElapsedTime()
    {
        var calculator = new NetworkRateCalculator();
        var start = DateTimeOffset.UtcNow;

        Assert.IsNull(calculator.Add(new NetworkCounterSnapshot(1_000, 500, start)));
        var result = calculator.Add(new NetworkCounterSnapshot(5_000, 2_500, start.AddSeconds(2)));

        Assert.IsNotNull(result);
        Assert.AreEqual(2_000, result.Value.Download, 0.001);
        Assert.AreEqual(1_000, result.Value.Upload, 0.001);
    }

    [TestMethod]
    public void Add_CounterResetStartsANewBaseline()
    {
        var calculator = new NetworkRateCalculator();
        var start = DateTimeOffset.UtcNow;
        calculator.Add(new NetworkCounterSnapshot(10_000, 10_000, start));

        Assert.IsNull(calculator.Add(new NetworkCounterSnapshot(100, 100, start.AddSeconds(1))));
        Assert.IsNotNull(calculator.Add(new NetworkCounterSnapshot(200, 300, start.AddSeconds(2))));
    }
}
