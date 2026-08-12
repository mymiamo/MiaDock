using System.Runtime.InteropServices;
using MiaDock.Core.Logging;
using MiaDock.Modules.Time.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace MiaDock.Platform.Windows.Time;

public sealed class WindowsTimerAlarmPlayer : ITimerAlarmPlayer, IDisposable
{
    internal const int AlarmPlayCount = 5;
    private const uint SoundAsync = 0x00000001;
    private const uint SoundAlias = 0x00010000;
    private const uint SoundSystem = 0x00200000;
    private readonly object _gate = new();
    private readonly object _sessionOperationGate = new();
    private readonly object _callbackExecutionGate = new();
    private readonly string _alarmPath;
    private readonly Func<IAlarmPlaybackSession> _sessionFactory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action _playFallback;
    private readonly ILogService? _log;
    private readonly Action<Action> _callbackScheduler;
    private IAlarmPlaybackSession? _activeSession;
    private int _remainingPlayCount;
    private bool _disposed;

    public WindowsTimerAlarmPlayer(ILogService? log = null)
        : this(
            Path.Combine(AppContext.BaseDirectory, "Assets", "miadock-ringtone.wav"),
            static () => new WindowsMediaAlarmPlaybackSession(),
            File.Exists,
            PlaySystemFallback,
            log,
            QueueCallback)
    {
    }

    internal WindowsTimerAlarmPlayer(
        string alarmPath,
        Func<IAlarmPlaybackSession> sessionFactory,
        Func<string, bool> fileExists,
        Action playFallback,
        ILogService? log = null,
        Action<Action>? callbackScheduler = null)
    {
        _alarmPath = alarmPath;
        _sessionFactory = sessionFactory;
        _fileExists = fileExists;
        _playFallback = playFallback;
        _log = log;
        _callbackScheduler = callbackScheduler ?? (action => action());
    }

    public void Play()
    {
        IAlarmPlaybackSession? session = null;
        lock (_gate)
        {
            if (_disposed || _activeSession is not null)
            {
                return;
            }

            if (!_fileExists(_alarmPath))
            {
                LogPlaybackFailure("file-missing", null);
                _playFallback();
                return;
            }

            try
            {
                LogStartingCheckpoint();
                session = _sessionFactory();
                session.Completed += OnPlaybackCompleted;
                session.Failed += OnPlaybackFailed;
                _activeSession = session;
                _remainingPlayCount = AlarmPlayCount;
                lock (_sessionOperationGate)
                {
                    session.Play(CreateFileUri(_alarmPath));
                }
            }
            catch (Exception exception)
            {
                if (session is not null)
                {
                    session.Completed -= OnPlaybackCompleted;
                    session.Failed -= OnPlaybackFailed;
                    session.Dispose();
                }

                _activeSession = null;
                _remainingPlayCount = 0;
                LogPlaybackFailure("start", exception);
                _playFallback();
            }
        }
    }

    public void Stop()
    {
        IAlarmPlaybackSession? session;
        lock (_gate)
        {
            session = _activeSession;
            _activeSession = null;
            _remainingPlayCount = 0;
        }

        DisposeSession(session);
    }

    public void Dispose()
    {
        IAlarmPlaybackSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _activeSession;
            _activeSession = null;
            _remainingPlayCount = 0;
        }

        DisposeSession(session);
    }

    private void OnPlaybackCompleted(object? sender, EventArgs args)
    {
        if (sender is IAlarmPlaybackSession session)
        {
            ScheduleCallback(() => HandlePlaybackCompleted(session));
        }
    }

    private void HandlePlaybackCompleted(IAlarmPlaybackSession session)
    {
        var replay = false;
        lock (_gate)
        {
            if (!ReferenceEquals(_activeSession, session))
            {
                return;
            }

            _remainingPlayCount--;
            if (_remainingPlayCount > 0)
            {
                replay = true;
            }
            else
            {
                _activeSession = null;
            }
        }

        if (!replay)
        {
            DisposeSession(session);
            return;
        }

        try
        {
            lock (_sessionOperationGate)
            {
                session.Replay();
            }
        }
        catch (Exception exception)
        {
            LogPlaybackFailure("replay", exception);
            FinishPlayback(session, useFallback: true);
        }
    }

    private void OnPlaybackFailed(object? sender, EventArgs args)
    {
        if (sender is IAlarmPlaybackSession session)
        {
            ScheduleCallback(() =>
            {
                LogPlaybackFailure("media-failed", null);
                FinishPlayback(session, useFallback: true);
            });
        }
    }

    private void FinishPlayback(IAlarmPlaybackSession session, bool useFallback)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_activeSession, session))
            {
                return;
            }

            _activeSession = null;
            _remainingPlayCount = 0;
        }

        DisposeSession(session);
        if (useFallback)
        {
            _playFallback();
        }
    }

    private void DisposeSession(IAlarmPlaybackSession? session)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            lock (_sessionOperationGate)
            {
                session.Completed -= OnPlaybackCompleted;
                session.Failed -= OnPlaybackFailed;
                session.Dispose();
            }
        }
        catch (Exception exception)
        {
            LogPlaybackFailure("dispose", exception);
        }
    }

    private void ScheduleCallback(Action action)
    {
        try
        {
            _callbackScheduler(() =>
            {
                try
                {
                    lock (_callbackExecutionGate)
                    {
                        action();
                    }
                }
                catch (Exception exception)
                {
                    LogPlaybackFailure("callback", exception);
                }
            });
        }
        catch (Exception exception)
        {
            LogPlaybackFailure("callback-schedule", exception);
        }
    }

    private void LogStartingCheckpoint()
    {
        if (_log is null)
        {
            return;
        }

        _log.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.TimerAlarmStarting,
            "TimerAlarm",
            "Timer alarm playback is starting.",
            properties: new Dictionary<string, object?>
            {
                ["phase"] = "before-media-player-play",
                ["count"] = AlarmPlayCount
            });
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            _log.FlushAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // The alarm must still play when diagnostic storage is unavailable.
        }
    }

    private void LogPlaybackFailure(string phase, Exception? exception) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.TimerAlarmPlaybackFailed,
            "TimerAlarm",
            "Timer alarm playback used a safe fallback or cleanup path.",
            exception,
            new Dictionary<string, object?>
            {
                ["phase"] = phase,
                ["hresult"] = exception?.HResult,
                ["count"] = _remainingPlayCount
            });

    private static void QueueCallback(Action action) =>
        ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), action);

    private static Uri CreateFileUri(string path) =>
        new(Path.GetFullPath(path), UriKind.Absolute);

    private static void PlaySystemFallback() =>
        _ = PlaySound(
            "SystemExclamation",
            nint.Zero,
            SoundAlias | SoundSystem | SoundAsync);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, nint module, uint flags);
}

internal interface IAlarmPlaybackSession : IDisposable
{
    event EventHandler? Completed;

    event EventHandler? Failed;

    void Play(Uri source);

    void Replay();
}

internal sealed class WindowsMediaAlarmPlaybackSession : IAlarmPlaybackSession
{
    private readonly MediaPlayer _player;
    private MediaSource? _source;
    private bool _disposed;

    internal WindowsMediaAlarmPlaybackSession()
    {
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Alerts,
            IsLoopingEnabled = false,
            Volume = 1
        };
        _player.CommandManager.IsEnabled = false;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
    }

    public event EventHandler? Completed;

    public event EventHandler? Failed;

    public void Play(Uri source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _source = MediaSource.CreateFromUri(source);
        _player.Source = _source;
        _player.Play();
    }

    public void Replay()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _player.PlaybackSession.Position = TimeSpan.Zero;
        _player.Play();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        try { _player.Pause(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        try { _player.Source = null; } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        try { _source?.Dispose(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        _source = null;
        try { _player.Dispose(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
    }

    private void OnMediaEnded(MediaPlayer sender, object args) =>
        Completed?.Invoke(this, EventArgs.Empty);

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
        Failed?.Invoke(this, EventArgs.Empty);
}
