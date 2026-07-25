using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TransferStateServiceTests
{
    [TestMethod]
    public async Task Heartbeat_ChangesWaitingThenRemovesDisconnectedTransfer()
    {
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var time = new ManualTimeProvider(now);
        var provider = new FakeTransferProvider();
        await using var service = new TransferStateService(provider, time);
        var snapshots = new List<TransferSnapshot>();
        service.SnapshotChanged += (_, snapshot) => snapshots.Add(snapshot);
        await service.StartAsync();

        provider.Publish(CreateMessage(TransferStatus.Running));
        Assert.HasCount(1, service.ActiveTransfers);

        time.Advance(TimeSpan.FromSeconds(16));
        service.EvaluateHeartbeats();
        Assert.AreEqual(TransferStatus.Waiting, service.ActiveTransfers[0].Status);

        time.Advance(TimeSpan.FromSeconds(15));
        service.EvaluateHeartbeats();
        Assert.IsEmpty(service.ActiveTransfers);
        Assert.AreEqual(TransferStatus.Disconnected, snapshots[^1].Status);
    }

    [TestMethod]
    public async Task TerminalUpdate_RemovesPersistentTransfer()
    {
        var provider = new FakeTransferProvider();
        await using var service = new TransferStateService(provider);
        await service.StartAsync();
        provider.Publish(CreateMessage(TransferStatus.Running));
        provider.Publish(CreateMessage(TransferStatus.Completed));

        Assert.IsEmpty(service.ActiveTransfers);
    }

    [TestMethod]
    public async Task RapidProgressUpdates_QueueOneUiCallbackAndPublishNewestState()
    {
        var provider = new FakeTransferProvider();
        var dispatcher = new QueuedDispatcher();
        await using var service = new TransferStateService(
            provider,
            dispatcher: dispatcher);
        IReadOnlyList<TransferSnapshot> displayed = [];
        service.TransfersChanged += (_, transfers) => displayed = transfers;
        await service.StartAsync();

        for (var index = 1; index <= 50_000; index++)
        {
            provider.Publish(CreateMessage(TransferStatus.Running) with
            {
                TransferredBytes = index,
                TotalBytes = 50_000
            });
        }

        Assert.AreEqual(1, dispatcher.PendingCount);
        dispatcher.RunQueued();
        Assert.HasCount(1, displayed);
        Assert.AreEqual(50_000, displayed[0].TransferredBytes);
        Assert.AreEqual(0, dispatcher.PendingCount);
    }

    private static TransferProgressMessage CreateMessage(TransferStatus status) => new(
        TransferProtocol.CurrentVersion,
        "test.provider",
        "transfer-1",
        "Test aktarımı",
        status == TransferStatus.Completed ? 100 : 50,
        100,
        status,
        DateTimeOffset.UtcNow);

    internal sealed class FakeTransferProvider : ITransferProgressProvider
    {
        public TransferProviderState State { get; private set; } = TransferProviderState.Stopped;
        public event EventHandler<TransferProgressMessage>? MessageReceived;
        public event EventHandler<TransferProviderState>? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            State = TransferProviderState.Listening;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = TransferProviderState.Stopped;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public void Publish(TransferProgressMessage message) => MessageReceived?.Invoke(this, message);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _callbacks = new();

        public bool HasThreadAccess => false;
        public int PendingCount => _callbacks.Count;

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        public void RunQueued()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
