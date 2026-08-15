using System.Globalization;
using MiaDock.App.Services;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Core.Presentation;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Services;

namespace MiaDock.App.Modules;

public sealed class HourlyNotificationModule : IIslandModule, IDisposable
{
    public const string ModuleId = "hourly-notification";
    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaximumDeliveryLateness = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinimumTimerDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaximumScheduleCheckInterval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISystemResumeService _resumeService;
    private readonly TimeProvider _timeProvider;
    private ITimer? _timer;
    private DateTimeOffset? _scheduledBoundaryUtc;
    private string? _lastPublishedBoundaryKey;
    private bool _isEnabled;
    private bool _disposed;

    public HourlyNotificationModule(
        ISettingsService settings,
        ILocalizationService localization,
        IUiDispatcher dispatcher,
        ISystemResumeService resumeService,
        TimeProvider? timeProvider = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _resumeService = resumeService ?? throw new ArgumentNullException(nameof(resumeService));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _resumeService.Resumed += OnSystemResumed;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Saat Başı Bildirimi",
        170,
        "GenericCompactModuleView",
        "GenericExpandedModuleView",
        new HashSet<ModuleEventKind> { ModuleEventKind.Notification },
        NotificationDuration,
        notificationViewKey: null,
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE121",
        displayNameKey: "HourlyNotification.ModuleName");

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PresentationChanged?.Invoke(this, null);
        }
    }

    public ModulePresentation? CurrentPresentation => null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => false;

    public ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            LifecycleState = ModuleLifecycleState.Active;
            _resumeService.Start();
            ArmNextBoundaryLocked();
        }

        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            LifecycleState = ModuleLifecycleState.Inactive;
            CancelScheduleLocked();
        }

        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
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
            LifecycleState = ModuleLifecycleState.Inactive;
            CancelScheduleLocked();
        }

        _resumeService.Resumed -= OnSystemResumed;
    }

    private bool ShouldRun =>
        !_disposed && IsEnabled && LifecycleState == ModuleLifecycleState.Active;

    private void OnSystemResumed(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            if (!ShouldRun)
            {
                return;
            }

            // Replacing the due boundary first prevents missed hours from being
            // replayed when Windows releases timers after resume.
            ArmNextBoundaryLocked();
        }
    }

    private void OnTimer(object? state)
    {
        DateTimeOffset boundaryUtc;
        bool shouldPublish;
        lock (_gate)
        {
            if (!ShouldRun || _scheduledBoundaryUtc is not { } scheduledBoundaryUtc)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var lateness = now - scheduledBoundaryUtc;
            var boundaryKey = scheduledBoundaryUtc.UtcDateTime.ToString(
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture);
            shouldPublish = lateness >= TimeSpan.Zero &&
                            lateness < MaximumDeliveryLateness &&
                            !string.Equals(
                                _lastPublishedBoundaryKey,
                                boundaryKey,
                                StringComparison.Ordinal);
            if (shouldPublish)
            {
                _lastPublishedBoundaryKey = boundaryKey;
            }

            boundaryUtc = scheduledBoundaryUtc;
            ArmNextBoundaryLocked();
        }

        if (!shouldPublish)
        {
            return;
        }

        void Publish()
        {
            lock (_gate)
            {
                if (!ShouldRun)
                {
                    return;
                }
            }

            var localBoundary = TimeZoneInfo.ConvertTime(
                boundaryUtc,
                _timeProvider.LocalTimeZone);
            var clockSettings = _settings.Current.General.Clock with
            {
                ShowSeconds = false,
                ShowDate = false
            };
            var timeText = ClockDisplayFormatter.Format(
                localBoundary,
                _localization.CurrentCulture,
                clockSettings).Time;
            var presentation = new ModulePresentation(
                ModuleId,
                _localization.Get("HourlyNotification.TimeFormat", timeText),
                _localization.Get("HourlyNotification.Description"),
                "\uE121",
                ModuleIndicatorKind.StatusDot,
                presentationKind: ModulePresentationKind.Status);
            EventOccurred?.Invoke(this, new ModuleEvent(
                ModuleId,
                ModuleEventKind.Notification,
                presentation,
                NotificationDuration,
                _timeProvider.GetUtcNow(),
                ModuleEventPriority.Normal,
                $"hourly-notification:{boundaryUtc:yyyyMMddHHmmss}",
                isFullscreenEligible: false,
                audibleCue: AudibleNotificationCue.Hourly));
        }

        if (_dispatcher.HasThreadAccess)
        {
            Publish();
        }
        else
        {
            _dispatcher.TryEnqueue(Publish);
        }
    }

    private void ArmNextBoundaryLocked()
    {
        if (!ShouldRun)
        {
            CancelScheduleLocked();
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var nextBoundaryUtc = FindNextLocalHourBoundaryUtc(now);
        var due = nextBoundaryUtc - now;
        if (due < MinimumTimerDelay)
        {
            due = MinimumTimerDelay;
        }
        else if (due > MaximumScheduleCheckInterval)
        {
            // Re-evaluate periodically so a live system time-zone change does
            // not leave the old local-hour boundary armed.
            due = MaximumScheduleCheckInterval;
        }

        _scheduledBoundaryUtc = nextBoundaryUtc;
        _timer ??= _timeProvider.CreateTimer(
            OnTimer,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _timer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private DateTimeOffset FindNextLocalHourBoundaryUtc(DateTimeOffset utcNow)
    {
        var timeZone = _timeProvider.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var currentLocalHour = DateTime.SpecifyKind(
            new DateTime(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                localNow.Hour,
                0,
                0),
            DateTimeKind.Unspecified);

        for (var hourOffset = 0; hourOffset <= 48; hourOffset++)
        {
            var localBoundary = currentLocalHour.AddHours(hourOffset);
            if (timeZone.IsInvalidTime(localBoundary))
            {
                continue;
            }

            var offsets = timeZone.IsAmbiguousTime(localBoundary)
                ? timeZone.GetAmbiguousTimeOffsets(localBoundary)
                : [timeZone.GetUtcOffset(localBoundary)];
            var candidate = offsets
                .Select(offset => new DateTimeOffset(localBoundary, offset).ToUniversalTime())
                .Where(value => value > utcNow)
                .OrderBy(value => value)
                .FirstOrDefault();
            if (candidate != default)
            {
                return candidate;
            }
        }

        return utcNow.AddHours(1);
    }

    private void CancelScheduleLocked()
    {
        _scheduledBoundaryUtc = null;
        _timer?.Dispose();
        _timer = null;
    }
}
