using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;

namespace MiaDock.Platform.Windows.Transfers;

public sealed class WindowsTransferPipeServer : ITransferProgressProvider
{
    private const int MaximumServerInstances = 8;
    private readonly string _pipeName;
    private readonly ProviderRateLimiter _rateLimiter;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<int, Task> _clients = new();
    private CancellationTokenSource? _cancellation;
    private Task? _acceptTask;
    private int _clientId;

    public WindowsTransferPipeServer(
        string? pipeName = null,
        TimeProvider? timeProvider = null)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? TransferProtocol.CurrentUserPipeName
            : pipeName;
        _rateLimiter = new ProviderRateLimiter(timeProvider);
    }

    public TransferProviderState State { get; private set; } = TransferProviderState.Stopped;

    public event EventHandler<TransferProgressMessage>? MessageReceived;
    public event EventHandler<TransferProviderState>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_acceptTask is { IsCompleted: false }) return Task.CompletedTask;
            SetState(TransferProviderState.Starting);
            try
            {
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _acceptTask = AcceptLoopAsync(_cancellation.Token);
                SetState(TransferProviderState.Listening);
            }
            catch (Exception exception) when (
                exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                _cancellation?.Dispose();
                _cancellation = null;
                _acceptTask = null;
                SetState(TransferProviderState.Unavailable);
            }
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancellation;
        Task? acceptTask;
        lock (_gate)
        {
            cancellation = _cancellation;
            acceptTask = _acceptTask;
            _cancellation = null;
            _acceptTask = null;
        }

        cancellation?.Cancel();
        if (acceptTask is not null)
        {
            try { await acceptTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        }

        var clients = _clients.Values.ToArray();
        if (clients.Length > 0)
        {
            try { await Task.WhenAll(clients).WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        }
        cancellation?.Dispose();
        SetState(TransferProviderState.Stopped);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var server = CreateServer();
                try
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                var clientId = Interlocked.Increment(ref _clientId);
                var task = HandleClientAsync(server, cancellationToken);
                _clients[clientId] = task;
                _ = task.ContinueWith(
                    antecedent => _clients.TryRemove(clientId, out var removedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetState(TransferProviderState.Faulted);
        }
    }

    private async Task HandleClientAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        await using (server.ConfigureAwait(false))
        {
            try
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var message = await LengthPrefixedJsonProtocol.ReadAsync(server, cancellationToken)
                        .ConfigureAwait(false);
                    if (message is null) break;
                    if (!TransferProtocol.TryNormalize(message, out var normalized)) continue;
                    if (!_rateLimiter.TryAcquire(normalized.ProviderId)) continue;
                    MessageReceived?.Invoke(this, normalized);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (
                exception is IOException or InvalidDataException or JsonException)
            {
                // Invalid clients are disconnected without logging message content or identifiers.
            }
        }
    }

    private NamedPipeServerStream CreateServer() => new(
        _pipeName,
        PipeDirection.In,
        MaximumServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
        TransferProtocol.MaximumMessageBytes,
        TransferProtocol.MaximumMessageBytes);

    private void SetState(TransferProviderState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
