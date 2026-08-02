using MiaDock.Platform.Windows.Connectivity;

namespace MiaDock.Platform.Windows.Tests.Connectivity;

[TestClass]
public sealed class NetworkInterfaceCounterReaderTests
{
    [TestMethod]
    public void TryRead_UsesNativeCountersWhenAvailable()
    {
        var interfaceId = Guid.NewGuid();
        var managedSourceCalled = false;
        var reader = new NetworkInterfaceCounterReader(
            NativeReader,
            () =>
            {
                managedSourceCalled = true;
                return [];
            });

        var succeeded = reader.TryRead(interfaceId, out var received, out var sent);

        Assert.IsTrue(succeeded);
        Assert.AreEqual<ulong>(4_096, received);
        Assert.AreEqual<ulong>(2_048, sent);
        Assert.IsFalse(managedSourceCalled);

        bool NativeReader(Guid candidate, out ulong receivedBytes, out ulong sentBytes)
        {
            Assert.AreEqual(interfaceId, candidate);
            receivedBytes = 4_096;
            sentBytes = 2_048;
            return true;
        }
    }

    [TestMethod]
    public void TryRead_FallsBackToManagedCountersForMatchingAdapter()
    {
        var interfaceId = Guid.NewGuid();
        var reader = new NetworkInterfaceCounterReader(
            NativeReader,
            () =>
            [
                new ManagedNetworkCounter(Guid.NewGuid(), 10, 20),
                new ManagedNetworkCounter(interfaceId, 8_192, 4_096)
            ]);

        var succeeded = reader.TryRead(interfaceId, out var received, out var sent);

        Assert.IsTrue(succeeded);
        Assert.AreEqual<ulong>(8_192, received);
        Assert.AreEqual<ulong>(4_096, sent);

        static bool NativeReader(Guid _, out ulong receivedBytes, out ulong sentBytes)
        {
            receivedBytes = 0;
            sentBytes = 0;
            return false;
        }
    }

    [TestMethod]
    public void TryRead_ReturnsFalseWhenNeitherSourceContainsAdapter()
    {
        var reader = new NetworkInterfaceCounterReader(
            NativeReader,
            () =>
            [
                new ManagedNetworkCounter(
                    Guid.NewGuid(),
                    10,
                    20,
                    IsOperational: false)
            ]);

        var succeeded = reader.TryRead(Guid.NewGuid(), out var received, out var sent);

        Assert.IsFalse(succeeded);
        Assert.AreEqual<ulong>(0, received);
        Assert.AreEqual<ulong>(0, sent);

        static bool NativeReader(Guid _, out ulong receivedBytes, out ulong sentBytes)
        {
            receivedBytes = 0;
            sentBytes = 0;
            return false;
        }
    }

    [TestMethod]
    public void TryRead_AggregatesOperationalAdaptersWhenProfileGuidIsVirtual()
    {
        var reader = new NetworkInterfaceCounterReader(
            NativeReader,
            () =>
            [
                new ManagedNetworkCounter(Guid.NewGuid(), 1_000, 500),
                new ManagedNetworkCounter(Guid.NewGuid(), 4_000, 2_000),
                new ManagedNetworkCounter(
                    Guid.NewGuid(),
                    50_000,
                    30_000,
                    IsLoopbackOrTunnel: true),
                new ManagedNetworkCounter(
                    Guid.NewGuid(),
                    80_000,
                    60_000,
                    IsOperational: false)
            ]);

        var succeeded = reader.TryRead(
            Guid.NewGuid(),
            out var received,
            out var sent);

        Assert.IsTrue(succeeded);
        Assert.AreEqual<ulong>(5_000, received);
        Assert.AreEqual<ulong>(2_500, sent);

        static bool NativeReader(
            Guid _,
            out ulong receivedBytes,
            out ulong sentBytes)
        {
            receivedBytes = 0;
            sentBytes = 0;
            return false;
        }
    }
}
