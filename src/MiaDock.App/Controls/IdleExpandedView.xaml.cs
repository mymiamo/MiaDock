using MiaDock.UI.Icons;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;
using MiaDock.Core.Localization;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.ViewModels;
using MiaDock.UI.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using MiaDock.Core.Overlay;
using VirtualKey = Windows.System.VirtualKey;

namespace MiaDock.App.Controls;

public sealed partial class IdleExpandedView : UserControl, IModuleViewActivationAware
{
    private readonly MusicModuleViewModel? _music;
    private readonly IdleDashboardViewModel? _dashboard;
    private readonly ILocalizationService? _localization;
    private readonly ISettingsService? _settings;
    private DispatcherQueueTimer? _clockTimer;
    private bool _isLoaded;
    private bool _isPresentationActive;
    private readonly IslandViewModel? _island;
    private readonly DockPositionEditSession? _positionEditor;

    public IdleExpandedView() : this(null, null, null, null, null, null)
    {
    }

    public IdleExpandedView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? system,
        IdleDashboardViewModel? dashboard,
        ILocalizationService? localization = null,
        ISettingsService? settings = null,
        FocusDockViewModel? focus = null)
    {
        _music = music;
        _dashboard = dashboard;
        _localization = localization;
        _settings = settings;
        _island = App.Services?.GetService<IslandViewModel>();
        _positionEditor = App.Services?.GetService<DockPositionEditSession>();
        InitializeComponent();
        LayoutRoot.DataContext = dashboard;
        SystemStatusPanel.DataContext = system;
        MusicPanel.DataContext = music;
        if (focus is not null)
        {
            FocusPanel.Configure(focus);
        }

        UpdateFocusVisibility();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        MusicActivity.IsSuppressed = !_isPresentationActive;
        if (_positionEditor is not null) _positionEditor.EditingChanged += OnPositionEditingChanged;
        UpdatePositionButtons();
        if (_island is not null) _island.PropertyChanged += OnIslandPropertyChanged;
        RefreshFavorites();
        if (_music is not null)
        {
            _music.PropertyChanged += OnMusicPropertyChanged;
            _music.SetAudioMeterActive(this, _isPresentationActive);
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged += OnSettingsChanged;
        }
        UpdateClock();
        UpdateMediaVisibility();
        UpdateFocusVisibility();
        ScheduleNextMinute();
    }

    public void SetPresentationActive(bool isActive)
    {
        if (_isPresentationActive == isActive) return;
        _isPresentationActive = isActive;
        MusicActivity.IsSuppressed = !isActive;
        _music?.SetAudioMeterActive(this, _isLoaded && isActive);
        if (_isLoaded && isActive) { UpdateClock(); ScheduleNextMinute(); }
        else StopTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _isLoaded = false;
        PlacementMoveHandle.CancelDrag();
        _positionEditor?.Cancel();
        if (_positionEditor is not null) _positionEditor.EditingChanged -= OnPositionEditingChanged;
        if (_island is not null) _island.PropertyChanged -= OnIslandPropertyChanged;
        if (_music is not null)
        {
            _music.PropertyChanged -= OnMusicPropertyChanged;
            _music.SetAudioMeterActive(this, false);
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
        }
        StopTimer();
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MusicModuleViewModel.Current) or
            nameof(MusicModuleViewModel.IsMediaAvailable))
        {
            UpdateMediaVisibility();
        }
    }

    private void UpdateMediaVisibility()
    {
        var hasMedia = _music?.Current.HasMedia == true;
        MediaArtwork.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        EmptyArtwork.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        MediaMetadataPanel.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        MediaEmptyPanel.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        UpdateAutomationName(hasMedia);
    }

    private void UpdateFocusVisibility()
    {
        var isEnabled = _settings?.Current.Focus.IsEnabled ?? true;
        FocusCard.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        FocusColumn.Width = isEnabled ? new GridLength(170) : new GridLength(0);
        Grid.SetColumn(MusicPanel, isEnabled ? 1 : 0);
        Grid.SetColumnSpan(MusicPanel, isEnabled ? 1 : 2);
    }

    private void UpdateClock()
    {
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture,
            ClockSettings);
        TimeText.Text = display.Time;
        DateText.Text = display.Date;
        DateText.Visibility = ClockSettings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAutomationName(bool hasMedia)
    {
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture,
            ClockSettings);
        var detail = hasMedia
            ? $"{_music!.Current.Track.Title}, {_music.Current.Track.Artist}"
            : _dashboard?.StatusSummary;
        AutomationProperties.SetName(
            LayoutRoot,
            string.IsNullOrWhiteSpace(detail)
                ? Text("Dock.Home.Automation", "Ana dock, {0}", display.Time)
                : $"{Text("Dock.Home.Automation", "Ana dock, {0}", display.Time)}, {detail}");
    }

    private void OnMinuteElapsed(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        UpdateClock();
        UpdateMediaVisibility();
        ScheduleNextMinute();
    }

    private void ScheduleNextMinute()
    {
        StopTimer();
        if (!_isLoaded || !_isPresentationActive) return;
        var now = DateTimeOffset.Now;
        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.IsRepeating = false;
        _clockTimer.Interval = ClockDisplayFormatter.DelayUntilNextRefresh(now, ClockSettings);
        _clockTimer.Tick += OnMinuteElapsed;
        _clockTimer.Start();
    }

    private void StopTimer()
    {
        if (_clockTimer is null)
        {
            return;
        }

        _clockTimer.Stop();
        _clockTimer.Tick -= OnMinuteElapsed;
        _clockTimer = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        UpdateClock();
        UpdateMediaVisibility();
        RefreshFavorites();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.Navigation != args.Current.Navigation)
        {
            _island?.RefreshNavigation();
            RefreshFavorites();
        }
        var clockChanged = args.Previous.General.Clock != args.Current.General.Clock;
        var focusVisibilityChanged =
            args.Previous.Focus.IsEnabled != args.Current.Focus.IsEnabled;
        if (!clockChanged && !focusVisibilityChanged)
        {
            return;
        }

        void ApplyChanges()
        {
            if (focusVisibilityChanged)
            {
                UpdateFocusVisibility();
            }

            if (clockChanged)
            {
                UpdateClock();
                UpdateMediaVisibility();
                ScheduleNextMinute();
            }
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyChanges();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyChanges);
        }
    }

    private ClockDisplaySettings ClockSettings =>
        _settings?.Current.General.Clock ?? ClockDisplaySettings.Default;

    private void OnIslandPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IslandViewModel.AvailableModules)) RefreshFavorites();
    }

    private void UpdatePositionButtons()
    {
        var editing = _positionEditor?.IsEditing == true;
        PlacementEditButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        PlacementEditButton.IsEnabled = _positionEditor is not null;
        if (!editing) PlacementMoveHandle.CancelDrag();
        PlacementResetButton.Visibility = PlacementMoveHandle.Visibility = PlacementSaveButton.Visibility =
            editing ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnPositionEditingChanged(object? sender, EventArgs args) => UpdatePositionButtons();
    private void OnPlacementEditClick(object sender, RoutedEventArgs args) => _positionEditor?.RequestEdit();
    private void OnPlacementResetClick(object sender, RoutedEventArgs args) => _positionEditor?.Reset();
    private void OnPlacementSaveClick(object sender, RoutedEventArgs args) => _positionEditor?.Save();
    private bool TryGetPointerScreenX(out double screenX, out double scale)
    {
        screenX = scale = 0;
        var overlay = App.Services?.GetService<IOverlayWindowService>()?.Current;
        return overlay is not null && overlay.TryGetPointerScreenX(out screenX, out scale);
    }
    private void OnPlacementDragStarted(object sender, DragStartedEventArgs args)
    {
        if (_positionEditor?.IsEditing == true && TryGetPointerScreenX(out var screenX, out var scale))
            _positionEditor.BeginDrag(screenX, scale);
        else
            PlacementMoveHandle.CancelDrag();
    }
    private void OnPlacementDragMoved(object sender, DragDeltaEventArgs args)
    {
        // Thumb deltas are relative to the moving control. Read the desktop
        // cursor instead so moving the HWND cannot feed back into the next delta.
        if (TryGetPointerScreenX(out var screenX, out _)) _positionEditor?.DragTo(screenX);
    }
    private void OnPlacementDragEnded(object sender, DragCompletedEventArgs args)
    {
        if (!args.Canceled && TryGetPointerScreenX(out var screenX, out _)) _positionEditor?.DragTo(screenX);
        _positionEditor?.EndDrag();
    }
    private void OnPlacementCaptureLost(object sender, PointerRoutedEventArgs args) => _positionEditor?.EndDrag();
    private void OnPlacementMoveKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Left or VirtualKey.Right)
        { _positionEditor?.Nudge(args.Key == VirtualKey.Left ? -10 : 10); args.Handled = true; }
    }
    private void OnPlacementKeyDown(object sender, KeyRoutedEventArgs args)
    { if (args.Key == VirtualKey.Escape && _positionEditor?.IsEditing == true) { _positionEditor.Cancel(); args.Handled = true; } }

    private void RefreshFavorites()
    {
        if (!DispatcherQueue.HasThreadAccess) { DispatcherQueue.TryEnqueue(RefreshFavorites); return; }
        FavoriteButtons.Children.Clear();
        if (_island is null) return;
        var pins = _settings?.Current.EffectiveNavigation.PinnedModuleIds ?? DockNavigationSettings.Default.PinnedModuleIds;
        foreach (var module in _island.AvailableModules.Where(item => pins.Contains(item.Descriptor.Id)))
        {
            var translated = _localization?.Get(module.Descriptor.DisplayNameKey);
            var label = translated is not null && translated != module.Descriptor.DisplayNameKey ? translated : module.Descriptor.DisplayName;
            var button = new Button { Content = new DockIcon { Glyph = module.Descriptor.IconGlyph,
                FontSize = 16 },
                Width = 44, UseSystemFocusVisuals = true,
                Style = (Style)Application.Current.Resources["DockIconButtonStyle"] };
            AutomationProperties.SetName(button, label);
            ToolTipService.SetToolTip(button, label);
            var id = module.Descriptor.Id;
            button.Click += (_, _) => { _island.SelectModule(id); _island.NotifyModuleInteraction(); };
            FavoriteButtons.Children.Add(button);
        }
    }

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(CultureInfo.CurrentCulture, fallback, arguments);
    }
}
