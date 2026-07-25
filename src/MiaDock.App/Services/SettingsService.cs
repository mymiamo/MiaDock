using MiaDock.Core.Settings;
using MiaDock.Core.Logging;

namespace MiaDock.App.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(300);
    private readonly ISettingsStore _store;
    private readonly ILogService? _log;
    private CancellationTokenSource? _saveCancellation;
    private Task _pendingSave = Task.CompletedTask;
    private bool _disposed;

    public SettingsService(ISettingsStore store, ILogService? log = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log;
    }

    public MiaDockSettings Current { get; private set; } = MiaDockSettings.Default;

    public Exception? LastSaveFailure { get; private set; }

    public string SettingsFilePath => _store.SettingsFilePath;

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            Current = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _log?.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.SettingsLoadFailed,
                "Settings",
                "Settings could not be loaded.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "load" });
            throw;
        }
    }

    public void Update(Func<MiaDockSettings, MiaDockSettings> update)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(update);
        var previous = Current;
        var next = SettingsValidator.Normalize(update(previous));
        if (next == previous)
        {
            return;
        }

        Current = next;
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(previous, next));
        ScheduleSave(next);
    }

    public void Reset() => Update(_ => MiaDockSettings.Default);

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _saveCancellation?.Cancel();
        try
        {
            await _pendingSave.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            await _store.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
            LastSaveFailure = null;
        }
        catch (Exception exception)
        {
            LastSaveFailure = exception;
            _log?.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.SettingsSaveFailed,
                "Settings",
                "Settings flush failed.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "flush" });
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            _saveCancellation?.Dispose();
            _saveCancellation = null;
        }
    }

    private void ScheduleSave(MiaDockSettings snapshot)
    {
        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = new CancellationTokenSource();
        _pendingSave = SaveAfterDelayAsync(snapshot, _saveCancellation.Token);
    }

    private async Task SaveAfterDelayAsync(MiaDockSettings snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SaveDelay, cancellationToken).ConfigureAwait(false);
            await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            LastSaveFailure = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LastSaveFailure = exception;
            _log?.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.SettingsSaveFailed,
                "Settings",
                "Settings could not be saved.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "save" });
        }
    }
}
