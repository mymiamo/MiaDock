using System.IO.Pipes;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Platform.Windows.Transfers;

namespace MiaDock.Platform.Windows.Tests.Transfers;

[TestClass]
public sealed class WindowsTransferPipeServerTests
{
    [TestMethod]
    public async Task CurrentUserPipe_ReceivesValidFramedMessage()
    {
        var pipeName = $"MiaDock.Tests.{Guid.NewGuid():N}";
        await using var server = new WindowsTransferPipeServer(pipeName);
        var received = new TaskCompletionSource<TransferProgressMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, message) => received.TrySetResult(message);
        await server.StartAsync();

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        await LengthPrefixedJsonProtocol.WriteAsync(client, CreateMessage());

        var actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("test.provider", actual.ProviderId);
        Assert.AreEqual(TransferProviderState.Listening, server.State);
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
