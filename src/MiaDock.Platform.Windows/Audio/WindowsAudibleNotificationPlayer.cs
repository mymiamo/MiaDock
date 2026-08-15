using System.Runtime.InteropServices;
using MiaDock.Core.Audio;
using MiaDock.Core.Logging;
using MiaDock.Core.Modules;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsAudibleNotificationPlayer : IAudibleNotificationPlayer
{
    internal static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<AudibleNotificationCue, string> FileNames =
        new Dictionary<AudibleNotificationCue, string>
        {
            [AudibleNotificationCue.NetworkOffline] = "connected-internet-none.wav",
            [AudibleNotificationCue.ConnectedWithoutInternet] = "connected-but-no-internet.wav",
            [AudibleNotificationCue.LowBattery] = "low-battery.wav",
            [AudibleNotificationCue.DeviceConnected] = "device-connected.wav",
            [AudibleNotificationCue.DeviceDisconnected] = "device-left.wav",
            [AudibleNotificationCue.Hourly] = "per-hour-per.wav"
        };

    private readonly object _gate = new();
    private readonly string _soundDirectory;
    private readonly Func<IAudibleNotificationPlaybackSession> _sessionFactory;
    private readonly Func<string, bool> _fileExists;
    private readonly TimeProvider _timeProvider;
    private readonly ILogService? _log;
    private IAudibleNotificationPlaybackSession? _session;
    private AudibleNotificationCue _lastCue;
    private DateTimeOffset _lastCueAt;
    private bool _disposed;

    public WindowsAudibleNotificationPlayer(ILogService? log = null)
        : this(
            Path.Combine(AppContext.BaseDirectory, "Assets", "sfx"),
            static () => new WindowsMediaAudibleNotificationSession(),
            File.Exists,
            TimeProvider.System,
            log)
    {
    }

    internal WindowsAudibleNotificationPlayer(
        string soundDirectory,
        Func<IAudibleNotificationPlaybackSession> sessionFactory,
        Func<string, bool> fileExists,
        TimeProvider timeProvider,
        ILogService? log = null)
    {
        _soundDirectory = soundDirectory;
        _sessionFactory = sessionFactory;
        _fileExists = fileExists;
        _timeProvider = timeProvider;
        _log = log;
    }

    public void Play(AudibleNotificationCue cue) => PlayCore(cue, isPreview: false);

    public void Preview(AudibleNotificationCue cue) => PlayCore(cue, isPreview: true);

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _session?.Stop();
            }
            catch (Exception exception)
            {
                LogFailure("stop", _lastCue, exception);
            }
        }
    }

    public void Dispose()
    {
        IAudibleNotificationPlaybackSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        if (session is null)
        {
            return;
        }

        session.Failed -= OnPlaybackFailed;
        try
        {
            session.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure("dispose", _lastCue, exception);
        }
    }

    private void PlayCore(AudibleNotificationCue cue, bool isPreview)
    {
        if (cue == AudibleNotificationCue.None || !FileNames.TryGetValue(cue, out var fileName))
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            if (!isPreview && cue == _lastCue && now - _lastCueAt < DuplicateWindow)
            {
                return;
            }

            if (!isPreview)
            {
                _lastCue = cue;
                _lastCueAt = now;
            }

            try
            {
                _session?.Stop();
            }
            catch (Exception exception)
            {
                LogFailure("replace", cue, exception);
            }

            var path = Path.Combine(_soundDirectory, fileName);
            if (!_fileExists(path))
            {
                LogFailure("file-missing", cue, null);
                return;
            }

            try
            {
                _session ??= CreateSession();
                _session.Play(new Uri(Path.GetFullPath(path), UriKind.Absolute));
            }
            catch (Exception exception)
            {
                LogFailure("play", cue, exception);
            }
        }
    }

    private IAudibleNotificationPlaybackSession CreateSession()
    {
        var session = _sessionFactory();
        session.Failed += OnPlaybackFailed;
        return session;
    }

    private void OnPlaybackFailed(object? sender, EventArgs args) =>
        LogFailure("media-failed", _lastCue, null);

    private void LogFailure(string operation, AudibleNotificationCue cue, Exception? exception) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.AudibleNotificationPlaybackFailed,
            "NotificationSound",
            "A notification sound could not be played and was skipped safely.",
            properties: new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["cue"] = cue.ToString(),
                ["errorType"] = exception?.GetType().Name
            });
}

internal interface IAudibleNotificationPlaybackSession : IDisposable
{
    event EventHandler? Failed;

    void Play(Uri source);

    void Stop();
}

internal sealed class WindowsMediaAudibleNotificationSession : IAudibleNotificationPlaybackSession
{
    private readonly MediaPlayer _player;
    private MediaSource? _source;
    private bool _disposed;

    internal WindowsMediaAudibleNotificationSession()
    {
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Alerts,
            IsLoopingEnabled = false,
            Volume = 1
        };
        _player.CommandManager.IsEnabled = false;
        _player.MediaFailed += OnMediaFailed;
    }

    public event EventHandler? Failed;

    public void Play(Uri source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previous = _source;
        _source = MediaSource.CreateFromUri(source);
        _player.Source = _source;
        previous?.Dispose();
        _player.Play();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _player.Pause();
        if (_source is not null)
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.MediaFailed -= OnMediaFailed;
        try { _player.Pause(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        try { _player.Source = null; } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        try { _source?.Dispose(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
        _source = null;
        try { _player.Dispose(); } catch (Exception exception) when (exception is COMException or ObjectDisposedException) { }
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
        Failed?.Invoke(this, EventArgs.Empty);
}
