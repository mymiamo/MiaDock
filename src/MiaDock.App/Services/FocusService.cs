using MiaDock.Core.Focus;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;

namespace MiaDock.App.Services;

public sealed class FocusService : IFocusService
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    private readonly object _gate = new();
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private ITimer? _expirationTimer;
    private FocusChangeReason? _requestedReason;
    private bool _started;
    private bool _disposed;

    public FocusService(
        ISettingsService settings,
        IUiDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public FocusSnapshot Current { get; private set; } = FocusSnapshot.Empty;

    public event EventHandler<FocusChangedEventArgs>? FocusChanged;

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _settings.SettingsChanged += OnSettingsChanged;
        }

        Reconcile(_settings.Current.Focus, FocusChangeReason.Initialized);
    }

    public bool Activate(
        string profileId,
        FocusActivationSource source = FocusActivationSource.Manual) =>
        ActivateCore(profileId, FocusActivationOptions.ProfileDefault(source));

    public bool ActivateFor(
        string profileId,
        TimeSpan duration,
        FocusActivationSource source = FocusActivationSource.Manual) =>
        ActivateCore(profileId, FocusActivationOptions.ForDuration(duration, source));

    public bool ActivateIndefinitely(
        string profileId,
        FocusActivationSource source = FocusActivationSource.Manual) =>
        ActivateCore(profileId, FocusActivationOptions.Indefinite(source));

    public bool Deactivate()
    {
        ThrowIfUnavailable();
        if (_settings.Current.Focus.ActiveState is null)
        {
            return false;
        }

        return UpdateActiveState(null, FocusChangeReason.Deactivated);
    }

    public bool Refresh()
    {
        ThrowIfUnavailable();
        var activeState = _settings.Current.Focus.ActiveState;
        if (activeState?.EndsAtUtc is not { } endsAtUtc ||
            endsAtUtc > UtcNow())
        {
            return false;
        }

        return UpdateActiveState(null, FocusChangeReason.Expired);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_started)
            {
                _settings.SettingsChanged -= OnSettingsChanged;
                _started = false;
            }

            DisposeExpirationTimerLocked();
        }
    }

    private bool ActivateCore(string profileId, FocusActivationOptions options)
    {
        ThrowIfUnavailable();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Source))
        {
            return false;
        }

        var normalizedId = profileId.Trim();
        var profile = _settings.Current.Focus.Profiles.FirstOrDefault(
            candidate => candidate.Id.Equals(normalizedId, StringComparison.Ordinal));
        if (profile is null)
        {
            return false;
        }

        var duration = options.UseProfileDefaultDuration
            ? profile.DefaultDurationMinutes is { } minutes
                ? TimeSpan.FromMinutes(minutes)
                : null
            : options.Duration;
        if (duration is { } requestedDuration &&
            (requestedDuration < MinimumDuration || requestedDuration > MaximumDuration))
        {
            return false;
        }

        var now = UtcNow();
        var activeState = new FocusActivationState(
            profile.Id,
            options.Source,
            now,
            duration is { } validDuration ? now.Add(validDuration) : null);
        var reason = _settings.Current.Focus.ActiveState is { } previous &&
                     !previous.ProfileId.Equals(profile.Id, StringComparison.Ordinal)
            ? FocusChangeReason.Switched
            : FocusChangeReason.Activated;
        return UpdateActiveState(activeState, reason);
    }

    private bool UpdateActiveState(
        FocusActivationState? activeState,
        FocusChangeReason reason)
    {
        lock (_gate)
        {
            ThrowIfUnavailableLocked();
            if (_settings.Current.Focus.ActiveState == activeState)
            {
                return false;
            }

            _requestedReason = reason;
        }

        try
        {
            _settings.Update(settings => settings with
            {
                Focus = settings.Focus with { ActiveState = activeState }
            });
        }
        finally
        {
            lock (_gate)
            {
                _requestedReason = null;
            }
        }

        return true;
    }

    private void Reconcile(FocusSettings focus, FocusChangeReason reason)
    {
        var now = UtcNow();
        if (focus.ActiveState?.EndsAtUtc is { } endsAtUtc && endsAtUtc <= now)
        {
            UpdateActiveState(null, FocusChangeReason.Expired);
            return;
        }

        var activeProfile = focus.ActiveState is { } activeState
            ? focus.Profiles.FirstOrDefault(profile =>
                profile.Id.Equals(activeState.ProfileId, StringComparison.Ordinal))
            : null;
        var snapshot = new FocusSnapshot(
            focus.Profiles,
            activeProfile,
            activeProfile is null ? null : focus.ActiveState);
        FocusChangedEventArgs? eventArgs = null;

        lock (_gate)
        {
            if (_disposed || !_started)
            {
                return;
            }

            var previous = Current;
            Current = snapshot;
            ScheduleExpirationLocked(snapshot.ActiveState?.EndsAtUtc, now);
            if (!SnapshotsEquivalent(previous, snapshot) ||
                reason == FocusChangeReason.Initialized)
            {
                eventArgs = new FocusChangedEventArgs(previous, snapshot, reason);
            }
        }

        if (eventArgs is not null)
        {
            FocusChanged?.Invoke(this, eventArgs);
        }
    }

    private void ScheduleExpirationLocked(
        DateTimeOffset? endsAtUtc,
        DateTimeOffset now)
    {
        DisposeExpirationTimerLocked();
        if (endsAtUtc is null)
        {
            return;
        }

        var dueTime = endsAtUtc.Value - now;
        if (dueTime < TimeSpan.Zero)
        {
            dueTime = TimeSpan.Zero;
        }

        _expirationTimer = _timeProvider.CreateTimer(
            static state => ((FocusService)state!).QueueExpirationRefresh(),
            this,
            dueTime,
            Timeout.InfiniteTimeSpan);
    }

    private void QueueExpirationRefresh()
    {
        lock (_gate)
        {
            if (_disposed || !_started)
            {
                return;
            }
        }

        _dispatcher.TryEnqueue(() =>
        {
            lock (_gate)
            {
                if (_disposed || !_started)
                {
                    return;
                }
            }

            Refresh();
        });
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (ReferenceEquals(args.Previous.Focus, args.Current.Focus) ||
            args.Previous.Focus == args.Current.Focus)
        {
            return;
        }

        FocusChangeReason reason;
        lock (_gate)
        {
            if (_disposed || !_started)
            {
                return;
            }

            reason = _requestedReason ?? ResolveExternalChangeReason(
                Current,
                args.Current.Focus);
        }

        Reconcile(args.Current.Focus, reason);
    }

    private static FocusChangeReason ResolveExternalChangeReason(
        FocusSnapshot previous,
        FocusSettings current)
    {
        if (previous.ActiveState is { } active &&
            current.ActiveState is null &&
            current.Profiles.All(profile =>
                !profile.Id.Equals(active.ProfileId, StringComparison.Ordinal)))
        {
            return FocusChangeReason.ProfileRemoved;
        }

        return FocusChangeReason.SettingsChanged;
    }

    private static bool SnapshotsEquivalent(
        FocusSnapshot left,
        FocusSnapshot right) =>
        left.ActiveState == right.ActiveState &&
        left.ActiveProfile == right.ActiveProfile &&
        left.Profiles.Count == right.Profiles.Count &&
        left.Profiles.SequenceEqual(right.Profiles);

    private DateTimeOffset UtcNow() => _timeProvider.GetUtcNow().ToUniversalTime();

    private void DisposeExpirationTimerLocked()
    {
        _expirationTimer?.Dispose();
        _expirationTimer = null;
    }

    private void ThrowIfUnavailable()
    {
        lock (_gate)
        {
            ThrowIfUnavailableLocked();
        }
    }

    private void ThrowIfUnavailableLocked()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            throw new InvalidOperationException("The focus service has not been started.");
        }
    }
}
