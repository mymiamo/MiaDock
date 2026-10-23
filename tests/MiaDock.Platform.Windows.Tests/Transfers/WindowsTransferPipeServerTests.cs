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

    [TestMethod]
    public async Task FullCapacity_WaitsForDisconnect_ThenAcceptsNinthClient_AndRestarts()
    {
        var name = $"MiaDock.Tests.{Guid.NewGuid():N}";
        await using var server = new WindowsTransferPipeServer(name);
        var clients = new List<NamedPipeClientStream>();
        var received = new TaskCompletionSource<TransferProgressMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, message) => received.TrySetResult(message);
        try
        {
            await server.StartAsync();
            for (var index = 0; index < 8; index++)
            {
                var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous);
                clients.Add(client);
                await client.ConnectAsync(5000);
            }
            var ninth = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous);
            clients.Add(ninth);
            var pending = ninth.ConnectAsync(5000);
            await Task.Delay(150);
            Assert.AreEqual(TransferProviderState.Listening, server.State);
            Assert.IsFalse(pending.IsCompleted, "At capacity the next client must wait rather than fault the server.");
            await clients[0].DisposeAsync();
            await pending;
            await LengthPrefixedJsonProtocol.WriteAsync(ninth, CreateMessage());
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(TransferProviderState.Stopped, server.State);
            await server.StartAsync();
            await using var restarted = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous);
            await restarted.ConnectAsync(5000);
            Assert.AreEqual(TransferProviderState.Listening, server.State);
        }
        finally { foreach (var client in clients) await client.DisposeAsync(); }
    }
}
