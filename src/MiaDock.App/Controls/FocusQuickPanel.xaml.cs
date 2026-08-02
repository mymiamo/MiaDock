using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;

namespace MiaDock.App.Controls;

public sealed partial class FocusQuickPanel : UserControl
{
    private FocusDockViewModel? _viewModel;
    private bool _isLoaded;

    public FocusQuickPanel() : this(null)
    {
    }

    public FocusQuickPanel(FocusDockViewModel? viewModel)
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

        if (DurationButton.Flyout is { } flyout)
        {
            DockInteractionSession.End(flyout);
        }
    }

    private void OnDurationFlyoutOpening(object sender, object args) =>
        DockInteractionSession.Begin(sender);

    private void OnDurationFlyoutClosed(object sender, object args) =>
        DockInteractionSession.End(sender);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        Refresh();

    private void OnDurationClick(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: string duration })
        {
            _viewModel?.SetDurationCommand.Execute(duration);
        }
    }

    private void Refresh()
    {
        var isActive = _viewModel?.IsActive == true;
        DurationButton.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        DeactivateButton.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        if (_viewModel is not null)
        {
            Duration15Item.Text = _viewModel.Duration15Label;
            Duration30Item.Text = _viewModel.Duration30Label;
            Duration60Item.Text = _viewModel.Duration60Label;
            Duration120Item.Text = _viewModel.Duration120Label;
            IndefiniteItem.Text = _viewModel.IndefiniteLabel;
            AutomationProperties.SetName(Root, _viewModel.StatusText);
        }
    }
}
