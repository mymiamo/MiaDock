using MiaDock.Modules.Transfers;
using MiaDock.Platform.Windows.Transfers;

namespace MiaDock.Platform.Windows.Tests.Transfers;

[TestClass]
public sealed class ProviderRateLimiterTests
{
    [TestMethod]
    public void TryAcquire_AllowsTenUpdatesPerProviderEachSecond()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new ProviderRateLimiter(time);

        for (var index = 0; index < TransferProtocol.MaximumUpdatesPerSecond; index++)
            Assert.IsTrue(limiter.TryAcquire("provider"));
        Assert.IsFalse(limiter.TryAcquire("provider"));
        Assert.IsTrue(limiter.TryAcquire("another-provider"));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsTrue(limiter.TryAcquire("provider"));
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
