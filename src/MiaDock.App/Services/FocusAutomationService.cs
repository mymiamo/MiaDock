using MiaDock.Core.Focus;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Services;
using MiaDock.Platform.Windows.Fullscreen;

namespace MiaDock.App.Services;

public sealed class FocusAutomationService : IFocusAutomationService
{
    private readonly object _gate = new();
    private readonly ISettingsService _settings;
    private readonly IFocusService _focus;
    private readonly IApplicationActivityService _applications;
    private readonly IFullscreenDetectionService _fullscreen;
    private readonly ISystemResumeService _systemResume;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;
    private readonly HashSet<string> _suppressedTriggers = new(StringComparer.Ordinal);
    private ITimer? _scheduleTimer;
    private bool _updatingFocus;
    private bool _runtimeActive;
    private bool _disposed;

    public FocusAutomationService(
        ISettingsService settings,
        IFocusService focus,
        IApplicationActivityService applications,
        IFullscreenDetectionService fullscreen,
        ISystemResumeService systemResume,
        IUiDispatcher dispatcher,
        TimeProvider? timeProvider = null,
        TimeZoneInfo? timeZone = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _applications = applications ??
            throw new ArgumentNullException(nameof(applications));
        _fullscreen = fullscreen ??
            throw new ArgumentNullException(nameof(fullscreen));
        _systemResume = systemResume ??
            throw new ArgumentNullException(nameof(systemResume));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public bool IsStarted { get; private set; }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsStarted)
            {
                return;
            }

            IsStarted = true;
            _settings.SettingsChanged += OnSettingsChanged;
        }
        ApplyEnabledState(_settings.Current.Focus.IsEnabled);
    }

    public void Refresh()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsStarted || !_runtimeActive || !_settings.Current.Focus.IsEnabled)
            {
                return;
            }
        }

        var candidates = EvaluateCandidates();
        var activeTriggerKeys = candidates
            .Select(candidate => candidate.TriggerKey)
            .ToHashSet(StringComparer.Ordinal);
        _suppressedTriggers.RemoveWhere(key => !activeTriggerKeys.Contains(key));

        var selected = candidates.FirstOrDefault(candidate =>
            !_suppressedTriggers.Contains(candidate.TriggerKey));
        var activeState = _focus.Current.ActiveState;
        if (activeState?.Source is FocusActivationSource.Manual or
            FocusActivationSource.Restored)
        {
            ScheduleNextEvaluation();
            return;
        }

        if (selected is null)
        {
            if (activeState?.Source is FocusActivationSource.Schedule or
                FocusActivationSource.Automation)
            {
                UpdateFocus(_focus.Deactivate);
            }

            ScheduleNextEvaluation();
            return;
        }

        if (activeState is not null &&
            activeState.ProfileId.Equals(selected.ProfileId, StringComparison.Ordinal) &&
            activeState.Source == selected.Source)
        {
            ScheduleNextEvaluation();
            return;
        }

        UpdateFocus(() => _focus.ActivateIndefinitely(
            selected.ProfileId,
            selected.Source));
        ScheduleNextEvaluation();
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
            if (IsStarted)
            {
                _settings.SettingsChanged -= OnSettingsChanged;
                DetachRuntimeLocked();
                IsStarted = false;
            }

            _scheduleTimer?.Dispose();
            _scheduleTimer = null;
        }
    }

    private IReadOnlyList<FocusAutomationCandidate> EvaluateCandidates()
    {
        var now = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
        var applicationState = _applications.Current;
        var fullscreenState = _fullscreen.Current;
        var candidates = new List<(FocusAutomationCandidate Candidate, int ProfileIndex)>();
        var profiles = _settings.Current.Focus.Profiles;
        for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
        {
            var profile = profiles[profileIndex];
            foreach (var rule in profile.ActivationRules.Where(rule => rule.IsEnabled))
            {
                var matched = rule.Kind switch
                {
                    FocusActivationRuleKind.ApplicationForeground =>
                        applicationState.IsForeground(rule.Target),
                    FocusActivationRuleKind.FullscreenApplication =>
                        fullscreenState.IsFullscreen &&
                        (rule.Target is null ||
                         applicationState.IsForeground(rule.Target)),
                    FocusActivationRuleKind.ApplicationRunning =>
                        applicationState.IsProcessMonitoringAvailable &&
                        applicationState.IsRunning(rule.Target),
                    _ => false
                };
                if (!matched)
                {
                    continue;
                }

                candidates.Add((
                    new FocusAutomationCandidate(
                        profile.Id,
                        $"rule:{profile.Id}:{rule.Id}",
                        FocusActivationSource.Automation,
                        RulePriority(rule.Kind)),
                    profileIndex));
            }

            foreach (var schedule in profile.Schedules.Where(schedule =>
                         FocusScheduleEvaluator.IsActive(schedule, now)))
            {
                candidates.Add((
                    new FocusAutomationCandidate(
                        profile.Id,
                        $"schedule:{profile.Id}:{schedule.Id}",
                        FocusActivationSource.Schedule,
                        100),
                    profileIndex));
            }
        }

        return candidates
            .OrderByDescending(item => item.Candidate.Priority)
            .ThenBy(item => item.ProfileIndex)
            .ThenBy(item => item.Candidate.TriggerKey, StringComparer.Ordinal)
            .Select(item => item.Candidate)
            .ToArray();
    }

    private static int RulePriority(FocusActivationRuleKind kind) => kind switch
    {
        FocusActivationRuleKind.ApplicationForeground => 400,
        FocusActivationRuleKind.FullscreenApplication => 300,
        FocusActivationRuleKind.ApplicationRunning => 200,
        _ => 0
    };

    private void ScheduleNextEvaluation()
    {
        lock (_gate)
        {
            _scheduleTimer?.Dispose();
            _scheduleTimer = null;
            if (_disposed ||
                !IsStarted ||
                !_runtimeActive ||
                !_settings.Current.Focus.IsEnabled ||
                !_settings.Current.Focus.Profiles.Any(profile =>
                    profile.Schedules.Any(schedule => schedule.IsEnabled)))
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var next = FocusScheduleEvaluator.NextMinuteBoundary(now, _timeZone);
            var due = next - now;
            if (due < TimeSpan.Zero)
            {
                due = TimeSpan.Zero;
            }

            _scheduleTimer = _timeProvider.CreateTimer(
                static state => ((FocusAutomationService)state!).QueueRefresh(),
                this,
                due,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void QueueRefresh()
    {
        lock (_gate)
        {
            if (_disposed || !IsStarted || !_runtimeActive)
            {
                return;
            }
        }

        _dispatcher.TryEnqueue(Refresh);
    }

    private void UpdateFocus(Func<bool> update)
    {
        _updatingFocus = true;
        try
        {
            _ = update();
        }
        finally
        {
            _updatingFocus = false;
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.Focus.IsEnabled != args.Current.Focus.IsEnabled)
        {
            ApplyEnabledState(args.Current.Focus.IsEnabled);
        }
        else if (args.Current.Focus.IsEnabled && args.Previous.Focus != args.Current.Focus)
        {
            Refresh();
        }
    }

    private void ApplyEnabledState(bool enabled)
    {
        var shouldStartDependencies = false;
        lock (_gate)
        {
            if (_disposed || !IsStarted)
            {
                return;
            }

            if (!enabled)
            {
                DetachRuntimeLocked();
                _suppressedTriggers.Clear();
                _scheduleTimer?.Dispose();
                _scheduleTimer = null;
                return;
            }

            if (_runtimeActive)
            {
                return;
            }

            _runtimeActive = true;
            _focus.FocusChanged += OnFocusChanged;
            _applications.ActivityChanged += OnApplicationActivityChanged;
            _fullscreen.StateChanged += OnFullscreenChanged;
            _systemResume.Resumed += OnSystemResumed;
            shouldStartDependencies = true;
        }

        if (shouldStartDependencies)
        {
            _applications.Start();
            _fullscreen.Start();
            _systemResume.Start();
            Refresh();
        }
    }

    private void DetachRuntimeLocked()
    {
        if (!_runtimeActive)
        {
            return;
        }
        _runtimeActive = false;
        _focus.FocusChanged -= OnFocusChanged;
        _applications.ActivityChanged -= OnApplicationActivityChanged;
        _fullscreen.StateChanged -= OnFullscreenChanged;
        _systemResume.Resumed -= OnSystemResumed;
    }

    private void OnFocusChanged(object? sender, FocusChangedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed || !_runtimeActive)
            {
                return;
            }
        }
        if (!_updatingFocus &&
            args.Reason == FocusChangeReason.Deactivated &&
            args.Previous.IsActive)
        {
            foreach (var candidate in EvaluateCandidates())
            {
                _suppressedTriggers.Add(candidate.TriggerKey);
            }
        }

        if (!_updatingFocus)
        {
            Refresh();
        }
    }

    private void OnApplicationActivityChanged(
        object? sender,
        ApplicationActivitySnapshot snapshot) =>
        Refresh();

    private void OnFullscreenChanged(object? sender, FullscreenSnapshot snapshot) =>
        Refresh();

    private void OnSystemResumed(object? sender, EventArgs args)
    {
        _applications.Refresh();
        _fullscreen.Refresh();
        Refresh();
    }
}
