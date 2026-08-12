using System.Runtime.InteropServices;
using MiaDock.Core.Input;

namespace MiaDock.Platform.Windows.Input;

public sealed class WindowsKeyboardLockMonitor : IKeyboardLockMonitor
{
    private const int VkCapital = 0x14;
    private const int VkScroll = 0x91;
    private const int VkNumLock = 0x90;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(75);

    private readonly object _gate = new();
    private readonly Func<int, short> _getKeyState;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private bool _capsOn;
    private bool _numOn;
    private bool _scrollOn;
    private bool _hasBaseline;
    private bool _disposed;

    public WindowsKeyboardLockMonitor()
        : this(GetKeyState)
    {
    }

    internal WindowsKeyboardLockMonitor(Func<int, short> getKeyState)
    {
        _getKeyState = getKeyState ?? throw new ArgumentNullException(nameof(getKeyState));
    }

    public event EventHandler<KeyboardLockStateChangedEventArgs>? StateChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_runTask is { IsCompleted: false })
            {
                return ValueTask.CompletedTask;
            }

            _runCancellation = new CancellationTokenSource();
            var token = _runCancellation.Token;
            CaptureBaseline();
            _runTask = Task.Run(() => RunAsync(token), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task? runTask;
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            runTask = _runTask;
            cancellation = _runCancellation;
            _runTask = null;
            _runCancellation = null;
            _hasBaseline = false;
        }

        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (runTask is not null)
        {
            try
            {
                await runTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                Poll();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void CaptureBaseline()
    {
        _capsOn = IsToggled(VkCapital);
        _numOn = IsToggled(VkNumLock);
        _scrollOn = IsToggled(VkScroll);
        _hasBaseline = true;
    }

    private void Poll()
    {
        var caps = IsToggled(VkCapital);
        var num = IsToggled(VkNumLock);
        var scroll = IsToggled(VkScroll);
        if (!_hasBaseline)
        {
            _capsOn = caps;
            _numOn = num;
            _scrollOn = scroll;
            _hasBaseline = true;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (caps != _capsOn)
        {
            _capsOn = caps;
            Raise(KeyboardLockKind.CapsLock, caps, now);
        }

        if (num != _numOn)
        {
            _numOn = num;
            Raise(KeyboardLockKind.NumLock, num, now);
        }

        if (scroll != _scrollOn)
        {
            _scrollOn = scroll;
            Raise(KeyboardLockKind.ScrollLock, scroll, now);
        }
    }

    private void Raise(KeyboardLockKind kind, bool isOn, DateTimeOffset occurredAtUtc) =>
        StateChanged?.Invoke(this, new KeyboardLockStateChangedEventArgs(kind, isOn, occurredAtUtc));

    private bool IsToggled(int virtualKey) => (_getKeyState(virtualKey) & 1) == 1;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
