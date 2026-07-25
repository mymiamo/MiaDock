using System.Buffers.Binary;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Platform.Windows.Transfers;

namespace MiaDock.Platform.Windows.Tests.Transfers;

[TestClass]
public sealed class LengthPrefixedJsonProtocolTests
{
    [TestMethod]
    public async Task RoundTrip_PreservesTransferMessage()
    {
        var expected = CreateMessage();
        await using var stream = new MemoryStream();
        await LengthPrefixedJsonProtocol.WriteAsync(stream, expected);
        stream.Position = 0;

        var actual = await LengthPrefixedJsonProtocol.ReadAsync(stream);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task Read_RejectsPayloadLargerThan64KbBeforeAllocation()
    {
        await using var stream = new MemoryStream();
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, TransferProtocol.MaximumMessageBytes + 1);
        await stream.WriteAsync(prefix);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await LengthPrefixedJsonProtocol.ReadAsync(stream));
    }

    private static TransferProgressMessage CreateMessage() => new(
        TransferProtocol.CurrentVersion,
        "test.provider",
        "transfer-1",
        "Test aktarımı",
        50,
        100,
        TransferStatus.Running,
        DateTimeOffset.UtcNow);
}
