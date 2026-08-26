using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Animations;
using MiaDock.App.Services;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using Windows.System;

namespace MiaDock.App.Controls;

public sealed partial class ModuleSwitcher : UserControl
{
    private ILocalizationService? _localization;
    private readonly Dictionary<string, ModuleButtonVisual> _moduleButtons =
        new(StringComparer.Ordinal);
    private readonly IMicroInteractionCoordinator? _microInteractions;
    private readonly IAnimationPreferenceService? _animationPreferences;
    private ModuleButtonVisual? _defaultVisual;
    private ModuleButtonVisual? _selectedVisual;
    private bool _isLoaded;
    public static readonly DependencyProperty ModulesProperty = DependencyProperty.Register(
        nameof(Modules), typeof(IReadOnlyList<ModuleDisplayState>), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnModulesChanged));
    public static readonly DependencyProperty SelectedModuleIdProperty = DependencyProperty.Register(
        nameof(SelectedModuleId), typeof(string), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnSelectionChanged));

    public ModuleSwitcher()
    {
        InitializeComponent();
        _microInteractions = App.Services?.GetService<IMicroInteractionCoordinator>();
        _animationPreferences = App.Services?.GetService<IAnimationPreferenceService>();
        _defaultVisual = new ModuleButtonVisual(DefaultButton, DefaultIcon);
        RebuildModuleButtons();
    }

    public event EventHandler? PreviousRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? DefaultRequested;
    public event EventHandler<ModuleSelectedEventArgs>? ModuleSelected;

    public IReadOnlyList<ModuleDisplayState>? Modules
    {
        get => (IReadOnlyList<ModuleDisplayState>?)GetValue(ModulesProperty);
        set => SetValue(ModulesProperty, value);
    }

    public string? SelectedModuleId
    {
        get => (string?)GetValue(SelectedModuleIdProperty);
        set => SetValue(SelectedModuleIdProperty, value);
    }

    public void ConfigureLocalization(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RefreshLocalizedLabels();
    }

    public void RefreshLocalizedContent() => RefreshLocalizedLabels();

    private static void OnModulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ModuleSwitcher)sender).RebuildModuleButtons();

    private static void OnSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ModuleSwitcher)sender).UpdateSelectionVisuals();

    private void RebuildModuleButtons()
    {
        Visibility = Modules is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        ModuleButtons.Children.Clear();
        _moduleButtons.Clear();
        foreach (var module in Modules ?? Array.Empty<ModuleDisplayState>())
        {
            var icon = CreateButtonIcon(module.Descriptor.IconGlyph);
            var button = new Button
            {
                Width = 44,
                Height = 44,
                Style = ResourceStyle("ModuleSwitcherItemStyle"),
                Tag = module.Descriptor.Id,
                UseSystemFocusVisuals = true,
                Content = icon
            };
            MicroMotion.SetIsInteractive(button, true);
            var displayName = LocalizedModuleName(module.Descriptor);
            ToolTipService.SetToolTip(button, displayName);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, displayName);
            button.Click += OnModuleClick;
            ModuleButtons.Children.Add(button);
            _moduleButtons[module.Descriptor.Id] = new ModuleButtonVisual(button, icon);
        }

        UpdateAutomationSetMetadata();
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        var defaultSelected = SelectedModuleId is null;
        ModuleButtonVisual? selectedVisual = defaultSelected ? _defaultVisual : null;
        if (_defaultVisual is not null)
        {
            ApplySelectionState(_defaultVisual, defaultSelected);
        }

        foreach (var (moduleId, visual) in _moduleButtons)
        {
            var selected = string.Equals(moduleId, SelectedModuleId, StringComparison.Ordinal);
            ApplySelectionState(visual, selected);
            if (selected)
            {
                selectedVisual = visual;
            }
        }

        var changed = !ReferenceEquals(_selectedVisual?.Button, selectedVisual?.Button);
        _selectedVisual = selectedVisual;
        if (changed && selectedVisual is not null && _isLoaded)
        {
            _microInteractions?.PlayStateChange(selectedVisual.Button);
            BringSelectedButtonIntoView(selectedVisual.Button);
        }
    }

    private void RefreshLocalizedLabels()
    {
        var homeName = Text("Dock.Home", "Ana dock");
        ToolTipService.SetToolTip(DefaultButton, homeName);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DefaultButton, homeName);
        foreach (var module in Modules ?? Array.Empty<ModuleDisplayState>())
        {
            if (!_moduleButtons.TryGetValue(module.Descriptor.Id, out var visual))
            {
                continue;
            }

            var displayName = LocalizedModuleName(module.Descriptor);
            ToolTipService.SetToolTip(visual.Button, displayName);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(visual.Button, displayName);
        }

        UpdateSelectionVisuals();
    }

    private static FontIcon CreateButtonIcon(string glyph)
    {
        return new FontIcon
        {
            Glyph = glyph,
            FontSize = 17,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = ResourceBrush("IslandTextSecondaryBrush")
        };
    }

    private void ApplySelectionState(ModuleButtonVisual visual, bool selected)
    {
        visual.Icon.Foreground = ResourceBrush(
            selected ? "IslandTextPrimaryBrush" : "IslandTextSecondaryBrush");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(
            visual.Button,
            selected ? Text("Navigation.Selected", "Seçili") : string.Empty);
    }

    private void UpdateAutomationSetMetadata()
    {
        var size = _moduleButtons.Count + 1;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetPositionInSet(DefaultButton, 1);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetSizeOfSet(DefaultButton, size);
        var position = 2;
        foreach (var visual in _moduleButtons.Values)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetPositionInSet(visual.Button, position++);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetSizeOfSet(visual.Button, size);
        }
    }

    private void BringSelectedButtonIntoView(Button button)
    {
        if (!button.IsLoaded || TabViewport.ActualWidth <= 0)
        {
            return;
        }

        try
        {
            var point = button.TransformToVisual(TabViewport).TransformPoint(default);
            if (point.X < 0 || point.X + button.ActualWidth > TabViewport.ActualWidth)
            {
                button.StartBringIntoView(new BringIntoViewOptions
                {
                    AnimationDesired = _animationPreferences?.AnimationsEnabled != false,
                    HorizontalAlignmentRatio = 0.5
                });
            }
        }
        catch (ArgumentException)
        {
            // Layout is being rebuilt; the queued layout pass will resolve the new bounds.
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
    }

    private void OnPreviousClick(object sender, RoutedEventArgs args) => RaisePrevious();
    private void OnNextClick(object sender, RoutedEventArgs args) => RaiseNext();
    private void OnDefaultClick(object sender, RoutedEventArgs args) =>
        DefaultRequested?.Invoke(this, EventArgs.Empty);

    private void OnModuleClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string moduleId })
        {
            ModuleSelected?.Invoke(this, new ModuleSelectedEventArgs(moduleId));
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Left)
        {
            RaisePrevious();
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Right)
        {
            RaiseNext();
            args.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        var delta = args.GetCurrentPoint(this).Properties.MouseWheelDelta;
        if (delta > 0)
        {
            RaisePrevious();
        }
        else if (delta < 0)
        {
            RaiseNext();
        }

        args.Handled = delta != 0;
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs args)
    {
        if (args.Cumulative.Translation.X <= -32)
        {
            RaiseNext();
        }
        else if (args.Cumulative.Translation.X >= 32)
        {
            RaisePrevious();
        }
    }

    private void RaisePrevious() => PreviousRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseNext() => NextRequested?.Invoke(this, EventArgs.Empty);

    private static Brush? ResourceBrush(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true
            ? value as Brush
            : null;

    private static Style? ResourceStyle(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true
            ? value as Style
            : null;

    private string LocalizedModuleName(ModuleDescriptor descriptor)
    {
        var value = _localization?.Get(descriptor.DisplayNameKey);
        return value is not null && value != descriptor.DisplayNameKey
            ? value
            : descriptor.DisplayName;
    }

    private string Text(string key, string fallback)
    {
        var value = _localization?.Get(key);
        return value is not null && value != key ? value : fallback;
    }

    private sealed record ModuleButtonVisual(Button Button, FontIcon Icon);
}

public sealed class ModuleSelectedEventArgs(string moduleId) : EventArgs
{
    public string ModuleId { get; } = moduleId;
}
