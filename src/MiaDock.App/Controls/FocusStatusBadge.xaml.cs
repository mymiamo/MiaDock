using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class FocusStatusBadge : UserControl
{
    public static readonly DependencyProperty ShowDetailsProperty =
        DependencyProperty.Register(
            nameof(ShowDetails),
            typeof(bool),
            typeof(FocusStatusBadge),
            new PropertyMetadata(false, OnDisplayModeChanged));

    public static readonly DependencyProperty ShowDeactivateButtonProperty =
        DependencyProperty.Register(
            nameof(ShowDeactivateButton),
            typeof(bool),
            typeof(FocusStatusBadge),
            new PropertyMetadata(false, OnDisplayModeChanged));

    private FocusDockViewModel? _viewModel;
    private bool _isLoaded;

    public FocusStatusBadge() : this(null)
    {
    }

    public FocusStatusBadge(FocusDockViewModel? viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public void Configure(FocusDockViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_isLoaded && _viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.SetPresentationActive(this, false);
        }

        _viewModel = viewModel;
        DataContext = viewModel;
        if (_isLoaded)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.SetPresentationActive(this, true);
        }

        Refresh();
    }

    public bool ShowDetails
    {
        get => (bool)GetValue(ShowDetailsProperty);
        set => SetValue(ShowDetailsProperty, value);
    }

    public bool ShowDeactivateButton
    {
        get => (bool)GetValue(ShowDeactivateButtonProperty);
        set => SetValue(ShowDeactivateButtonProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.SetPresentationActive(this, true);
        }

        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _isLoaded = false;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.SetPresentationActive(this, false);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        Refresh();

    private void Refresh()
    {
        var isActive = _viewModel?.IsActive == true;
        Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = isActive && ShowDetails
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeactivateButton.Visibility = isActive && ShowDeactivateButton
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!isActive || _viewModel is null)
        {
            return;
        }

        var color = ColorParser.ParseRgb(_viewModel.ActiveColor);
        var brush = new SolidColorBrush(color);
        BadgeBorder.BorderBrush = brush;
        ProfileIcon.Foreground = brush;
        ProfileIcon.Glyph = _viewModel.ActiveIconGlyph;
        ToolTipService.SetToolTip(BadgeBorder, _viewModel.StatusText);
        AutomationProperties.SetName(BadgeBorder, _viewModel.StatusText);
        ToolTipService.SetToolTip(DeactivateButton, _viewModel.TurnOffLabel);
        AutomationProperties.SetName(DeactivateButton, _viewModel.TurnOffLabel);
    }

    private static void OnDisplayModeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args) =>
        ((FocusStatusBadge)dependencyObject).Refresh();
}
