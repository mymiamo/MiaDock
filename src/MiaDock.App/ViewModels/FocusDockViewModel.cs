using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Focus;
using MiaDock.Core.Localization;
using MiaDock.Core.Threading;

namespace MiaDock.App.ViewModels;

public sealed partial class FocusDockViewModel : ObservableObject, IDisposable
{
    private readonly object _gate = new();
    private readonly IFocusService _focus;
    private readonly ILocalizationService _localization;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<object> _activePresentations =
        new(ReferenceEqualityComparer.Instance);
    private ITimer? _refreshTimer;
    private bool _disposed;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial string ActiveProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveIconGlyph { get; set; } = FocusIconGlyphs.For("star");

    [ObservableProperty]
    public partial string ActiveColor { get; set; } = "#0EA5E9";

    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public FocusDockViewModel(
        IFocusService focus,
        ILocalizationService localization,
        IUiDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        ActivateProfileCommand = new RelayCommand<string>(ActivateProfile);
        SetDurationCommand = new RelayCommand<string>(SetDuration);
        DeactivateCommand = new RelayCommand(Deactivate, () => IsActive);
        _focus.FocusChanged += OnFocusChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        RefreshFrom(_focus.Current);
    }

    public ObservableCollection<FocusProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<FocusProfileItemViewModel> QuickProfiles { get; } = [];

    public IRelayCommand<string> ActivateProfileCommand { get; }

    public IRelayCommand<string> SetDurationCommand { get; }

    public IRelayCommand DeactivateCommand { get; }

    public string FocusTitle => Text("Focus.Title");

    public string DurationLabel => Text("Focus.Duration");

    public string TurnOffLabel => Text("Focus.TurnOff");

    public string Duration15Label => Text("Focus.Duration.15Minutes");

    public string Duration30Label => Text("Focus.Duration.30Minutes");

    public string Duration60Label => Text("Focus.Duration.1Hour");

    public string Duration120Label => Text("Focus.Duration.2Hours");

    public string IndefiniteLabel => Text("Focus.Duration.UntilTurnedOff");

    public void SetPresentationActive(object owner, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (isActive)
            {
                _activePresentations.Add(owner);
            }
            else
            {
                _activePresentations.Remove(owner);
            }

            UpdateRefreshTimerLocked();
        }
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
            _focus.FocusChanged -= OnFocusChanged;
            _localization.LanguageChanged -= OnLanguageChanged;
            _activePresentations.Clear();
            DisposeRefreshTimerLocked();
        }
    }

    partial void OnIsActiveChanged(bool value) => DeactivateCommand.NotifyCanExecuteChanged();

    private void ActivateProfile(string? profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId) &&
            !string.Equals(
                _focus.Current.ActiveProfile?.Id,
                profileId,
                StringComparison.Ordinal))
        {
            _focus.Activate(profileId);
        }
    }

    private void SetDuration(string? value)
    {
        var profileId = _focus.Current.ActiveProfile?.Id;
        if (profileId is null)
        {
            return;
        }

        if (string.Equals(value, "indefinite", StringComparison.Ordinal))
        {
            _focus.ActivateIndefinitely(profileId);
            return;
        }

        if (int.TryParse(value, out var minutes))
        {
            _focus.ActivateFor(profileId, TimeSpan.FromMinutes(minutes));
        }
    }

    private void Deactivate() => _focus.Deactivate();

    private void OnFocusChanged(object? sender, FocusChangedEventArgs args) =>
        RefreshFrom(args.Current);

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        RefreshFrom(_focus.Current);

    private void RefreshFrom(FocusSnapshot snapshot)
    {
        var activeId = snapshot.ActiveProfile?.Id;
        var existingById = Profiles.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var ordered = new List<FocusProfileItemViewModel>(snapshot.Profiles.Count);
        foreach (var profile in snapshot.Profiles)
        {
            var displayName = DisplayName(profile);
            if (!existingById.TryGetValue(profile.Id, out var item) ||
                item.Profile != profile)
            {
                item = new FocusProfileItemViewModel(
                    profile,
                    displayName,
                    profile.Id == activeId);
            }
            else
            {
                item.DisplayName = displayName;
                item.IsActive = profile.Id == activeId;
            }

            ordered.Add(item);
        }

        Profiles.Clear();
        foreach (var item in ordered)
        {
            Profiles.Add(item);
        }

        QuickProfiles.Clear();
        foreach (var item in ordered.Take(4))
        {
            QuickProfiles.Add(item);
        }

        IsActive = snapshot.IsActive;
        ActiveProfileName = snapshot.ActiveProfile is { } active
            ? DisplayName(active)
            : string.Empty;
        ActiveIconGlyph = snapshot.ActiveProfile is { } iconProfile
            ? FocusIconGlyphs.For(iconProfile.IconKey)
            : FocusIconGlyphs.For("star");
        ActiveColor = snapshot.ActiveProfile?.Color ?? "#0EA5E9";
        RefreshTimeText(snapshot);
        OnPropertyChanged(nameof(FocusTitle));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(TurnOffLabel));
        OnPropertyChanged(nameof(Duration15Label));
        OnPropertyChanged(nameof(Duration30Label));
        OnPropertyChanged(nameof(Duration60Label));
        OnPropertyChanged(nameof(Duration120Label));
        OnPropertyChanged(nameof(IndefiniteLabel));

        lock (_gate)
        {
            UpdateRefreshTimerLocked();
        }
    }

    private void RefreshTimeText(FocusSnapshot snapshot)
    {
        if (!snapshot.IsActive)
        {
            RemainingText = string.Empty;
            StatusText = Text("Focus.Status.Off");
            return;
        }

        if (snapshot.ActiveState?.EndsAtUtc is not { } endsAtUtc)
        {
            RemainingText = Text("Focus.Duration.UntilTurnedOff");
            StatusText = Text(
                "Focus.Status.Active",
                ActiveProfileName,
                RemainingText);
            return;
        }

        var remaining = endsAtUtc - _timeProvider.GetUtcNow();
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        RemainingText = Text("Focus.Duration.MinutesRemaining", minutes);
        StatusText = Text("Focus.Status.Active", ActiveProfileName, RemainingText);
    }

    private string DisplayName(FocusProfile profile)
    {
        if (profile.Kind == FocusProfileKind.Custom)
        {
            return profile.CustomName ?? Text("Focus.Profile.Custom.Name");
        }

        return Text(FocusProfileDefaults.GetDisplayNameKey(profile));
    }

    private string Text(string key, params object?[] arguments) =>
        _localization.Get(key, arguments);

    private void UpdateRefreshTimerLocked()
    {
        DisposeRefreshTimerLocked();
        if (_disposed ||
            _activePresentations.Count == 0 ||
            _focus.Current.ActiveState?.EndsAtUtc is null)
        {
            return;
        }

        _refreshTimer = _timeProvider.CreateTimer(
            static state => ((FocusDockViewModel)state!).QueueTimeRefresh(),
            this,
            TimeSpan.FromMinutes(1),
            Timeout.InfiniteTimeSpan);
    }

    private void QueueTimeRefresh()
    {
        lock (_gate)
        {
            if (_disposed || _activePresentations.Count == 0)
            {
                return;
            }
        }

        _dispatcher.TryEnqueue(() =>
        {
            lock (_gate)
            {
                if (_disposed || _activePresentations.Count == 0)
                {
                    return;
                }
            }

            RefreshTimeText(_focus.Current);
            lock (_gate)
            {
                UpdateRefreshTimerLocked();
            }
        });
    }

    private void DisposeRefreshTimerLocked()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }
}
