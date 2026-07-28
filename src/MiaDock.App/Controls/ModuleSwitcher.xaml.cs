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
    public static readonly DependencyProperty ModulesProperty = DependencyProperty.Register(
        nameof(Modules), typeof(IReadOnlyList<ModuleDisplayState>), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnDataChanged));
    public static readonly DependencyProperty SelectedModuleIdProperty = DependencyProperty.Register(
        nameof(SelectedModuleId), typeof(string), typeof(ModuleSwitcher),
        new PropertyMetadata(null, OnDataChanged));

    public ModuleSwitcher()
    {
        InitializeComponent();
        UpdateVisualState();
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
        UpdateVisualState();
    }

    public void RefreshLocalizedContent() => UpdateVisualState();

    private static void OnDataChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ModuleSwitcher)sender).UpdateVisualState();

    private void UpdateVisualState()
    {
        Visibility = Modules is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        var defaultSelected = SelectedModuleId is null;
        DefaultButton.Opacity = defaultSelected ? 1 : 0.66;
        DefaultButton.BorderThickness = defaultSelected ? new Thickness(1.5) : new Thickness(0);
        DefaultButton.BorderBrush = defaultSelected ? ResourceBrush("IslandStyleAccentBrush") : null;
        ModuleButtons.Children.Clear();
        foreach (var module in Modules ?? Array.Empty<ModuleDisplayState>())
        {
            var selected = module.Descriptor.Id == SelectedModuleId;
            var button = new Button
            {
                Width = 36,
                Height = 36,
                Style = ResourceStyle("IslandCompactIconButtonStyle"),
                Tag = module.Descriptor.Id,
                Opacity = selected ? 1 : 0.66,
                BorderThickness = selected ? new Thickness(1.5) : new Thickness(0),
                BorderBrush = selected ? ResourceBrush("IslandStyleAccentBrush") : null,
                UseSystemFocusVisuals = true,
                Content = new FontIcon { Glyph = module.Descriptor.IconGlyph, FontSize = 11 }
            };
            var displayName = LocalizedModuleName(module.Descriptor);
            ToolTipService.SetToolTip(button, displayName);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, displayName);
            button.Click += OnModuleClick;
            ModuleButtons.Children.Add(button);
        }
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
}

public sealed class ModuleSelectedEventArgs(string moduleId) : EventArgs
{
    public string ModuleId { get; } = moduleId;
}
