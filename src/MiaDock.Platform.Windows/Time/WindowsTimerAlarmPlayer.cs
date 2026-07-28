using System.Runtime.InteropServices;
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
    private readonly string _alarmPath;
    private readonly Func<IAlarmPlaybackSession> _sessionFactory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action _playFallback;
    private IAlarmPlaybackSession? _activeSession;
    private int _remainingPlayCount;
    private bool _disposed;

    public WindowsTimerAlarmPlayer()
        : this(
            Path.Combine(AppContext.BaseDirectory, "Assets", "miadock-ringtone.wav"),
            static () => new WindowsMediaAlarmPlaybackSession(),
            File.Exists,
            PlaySystemFallback)
    {
    }

    internal WindowsTimerAlarmPlayer(
        string alarmPath,
        Func<IAlarmPlaybackSession> sessionFactory,
        Func<string, bool> fileExists,
        Action playFallback)
    {
        _alarmPath = alarmPath;
        _sessionFactory = sessionFactory;
        _fileExists = fileExists;
        _playFallback = playFallback;
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
                _playFallback();
                return;
            }

            try
            {
                session = _sessionFactory();
                session.Completed += OnPlaybackCompleted;
                session.Failed += OnPlaybackFailed;
                _activeSession = session;
                _remainingPlayCount = AlarmPlayCount;
                session.Play(CreateFileUri(_alarmPath));
            }
            catch
            {
                if (session is not null)
                {
                    session.Completed -= OnPlaybackCompleted;
                    session.Failed -= OnPlaybackFailed;
                    session.Dispose();
                }

                _activeSession = null;
                _remainingPlayCount = 0;
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
        if (sender is not IAlarmPlaybackSession session)
        {
            return;
        }

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
            session.Replay();
        }
        catch
        {
            FinishPlayback(session, useFallback: true);
        }
    }

    private void OnPlaybackFailed(object? sender, EventArgs args)
    {
        if (sender is IAlarmPlaybackSession session)
        {
            FinishPlayback(session, useFallback: true);
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

        session.Completed -= OnPlaybackCompleted;
        session.Failed -= OnPlaybackFailed;
        session.Dispose();
    }

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

        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.Pause();
        _player.Source = null;
        _source?.Dispose();
        _source = null;
        _player.Dispose();
        _disposed = true;
    }

    private void OnMediaEnded(MediaPlayer sender, object args) =>
        Completed?.Invoke(this, EventArgs.Empty);

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
        Failed?.Invoke(this, EventArgs.Empty);
}
