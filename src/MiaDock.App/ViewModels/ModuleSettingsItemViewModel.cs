using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.App.Services;

namespace MiaDock.App.ViewModels;

public sealed class ModuleSettingsItemViewModel : ObservableObject
{
    private readonly Action<ModuleSettingsItemViewModel> _changed;
    private bool _isEnabled;
    private double _eventDurationSeconds;
    private bool _showInFullscreen;
    private ModuleAvailabilityState _availabilityState;
    private string _availabilityText = string.Empty;
    private string _availabilityGlyph = "\uE73E";

    public ModuleSettingsItemViewModel(
        string moduleId,
        string title,
        string description,
        string iconGlyph,
        Action<ModuleSettingsItemViewModel> changed)
    {
        ModuleId = moduleId;
        Title = title;
        Description = description;
        IconGlyph = iconGlyph;
        _changed = changed;
    }

    public string ModuleId { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string IconGlyph { get; }
    public bool IsPermissionBased => ModuleId == "notifications";
    public bool HasCustomOptions => ModuleId is "battery" or "media" or "notifications" or "timer";

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }

    public double EventDurationSeconds
    {
        get => _eventDurationSeconds;
        set
        {
            var normalized = Math.Clamp(double.IsFinite(value) ? value : 5, 1, 60);
            if (SetProperty(ref _eventDurationSeconds, normalized)) _changed(this);
        }
    }

    public bool ShowInFullscreen
    {
        get => _showInFullscreen;
        set
        {
            if (SetProperty(ref _showInFullscreen, value)) _changed(this);
        }
    }

    public ModuleAvailabilityState AvailabilityState
    {
        get => _availabilityState;
        private set => SetProperty(ref _availabilityState, value);
    }

    public string AvailabilityText
    {
        get => _availabilityText;
        private set => SetProperty(ref _availabilityText, value);
    }

    public string AvailabilityGlyph
    {
        get => _availabilityGlyph;
        private set => SetProperty(ref _availabilityGlyph, value);
    }

    public void Refresh(
        string title,
        string description,
        bool isEnabled,
        double eventDurationSeconds,
        bool showInFullscreen,
        ModuleAvailability availability,
        string availabilityText)
    {
        Title = title;
        Description = description;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        IsEnabled = isEnabled;
        SetProperty(ref _eventDurationSeconds, eventDurationSeconds, nameof(EventDurationSeconds));
        SetProperty(ref _showInFullscreen, showInFullscreen, nameof(ShowInFullscreen));
        AvailabilityState = availability.State;
        AvailabilityText = availabilityText;
        AvailabilityGlyph = availability.State switch
        {
            ModuleAvailabilityState.Ready => "\uE73E",
            ModuleAvailabilityState.Disabled => "\uE711",
            ModuleAvailabilityState.PermissionRequired => "\uE72E",
            ModuleAvailabilityState.PermissionDenied => "\uE783",
            ModuleAvailabilityState.NoCompatibleDevice => "\uE7F8",
            ModuleAvailabilityState.TemporaryError => "\uEA39",
            _ => "\uE946"
        };
    }
}
