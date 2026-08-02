using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using Windows.System;

namespace MiaDock.App.Controls;

public sealed partial class ModuleSwitcher : UserControl
{
    private ILocalizationService? _localization;
    private readonly Dictionary<string, ModuleButtonVisual> _moduleButtons =
        new(StringComparer.Ordinal);
    public static readonly DependencyProperty ModulesProperty = DependencyProperty.Register(
        nameof(Modules), typeof(IReadOnlyList<ModuleDisplayState>), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnModulesChanged));
    public static readonly DependencyProperty SelectedModuleIdProperty = DependencyProperty.Register(
        nameof(SelectedModuleId), typeof(string), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnSelectionChanged));

    public ModuleSwitcher()
    {
        InitializeComponent();
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
            var content = CreateButtonContent(module.Descriptor.IconGlyph, out var indicator);
            var button = new Button
            {
                Width = 44,
                Height = 44,
                Style = ResourceStyle("DockIconButtonStyle"),
                Tag = module.Descriptor.Id,
                UseSystemFocusVisuals = true,
                Content = content
            };
            var displayName = LocalizedModuleName(module.Descriptor);
            ToolTipService.SetToolTip(button, displayName);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, displayName);
            button.Click += OnModuleClick;
            ModuleButtons.Children.Add(button);
            _moduleButtons[module.Descriptor.Id] = new ModuleButtonVisual(button, indicator);
        }

        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        var defaultSelected = SelectedModuleId is null;
        DefaultButton.Opacity = defaultSelected ? 1 : 0.72;
        DefaultButton.Background = defaultSelected ? ResourceBrush("IslandControlFillBrush") : null;
        DefaultIndicator.Visibility = defaultSelected ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (moduleId, visual) in _moduleButtons)
        {
            var selected = string.Equals(moduleId, SelectedModuleId, StringComparison.Ordinal);
            visual.Button.Opacity = selected ? 1 : 0.72;
            visual.Button.Background = selected ? ResourceBrush("IslandControlFillBrush") : null;
            visual.Indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshLocalizedLabels()
    {
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
    }

    private static UIElement CreateButtonContent(string glyph, out Border indicator)
    {
        var content = new Grid();
        content.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        indicator = new Border
        {
            Width = 4,
            Height = 2,
            Margin = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(1),
            Background = ResourceBrush("IslandStyleAccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Collapsed
        };
        content.Children.Add(indicator);

        return content;
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

    private sealed record ModuleButtonVisual(Button Button, Border Indicator);
}

public sealed class ModuleSelectedEventArgs(string moduleId) : EventArgs
{
    public string ModuleId { get; } = moduleId;
}
