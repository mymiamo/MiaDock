using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TransferProtocolTests
{
    [TestMethod]
    public void Normalize_AcceptsVersionedMessageAndRemovesControlCharacters()
    {
        var message = new TransferProgressMessage(
            TransferProtocol.CurrentVersion,
            "sample.provider",
            "transfer-1",
            "Arşiv\u0000 aktarımı",
            50,
            100,
            TransferStatus.Running,
            DateTimeOffset.Now);

        Assert.IsTrue(TransferProtocol.TryNormalize(message, out var normalized));
        Assert.AreEqual("Arşiv aktarımı", normalized.SafeDisplayName);
        Assert.AreEqual(TimeSpan.Zero, normalized.TimestampUtc.Offset);
    }

    [TestMethod]
    public void Normalize_RejectsInvalidIdentityAndByteRanges()
    {
        var message = new TransferProgressMessage(
            TransferProtocol.CurrentVersion,
            "invalid provider",
            "transfer-1",
            "Aktarım",
            101,
            100,
            TransferStatus.Running,
            DateTimeOffset.UtcNow);

        Assert.IsFalse(TransferProtocol.TryNormalize(message, out _));
    }

    [TestMethod]
    public void PipeName_DoesNotExposeTheWindowsUserName()
    {
        StringAssert.StartsWith(TransferProtocol.CurrentUserPipeName, "MiaDock.TransferProgress.");
        Assert.IsFalse(
            TransferProtocol.CurrentUserPipeName.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
    }
}
