using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Modules.Time.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class TimerExpandedView : UserControl
{
    private readonly IAppLocalizationService? _localization;
    private TimeToolsViewModel? _viewModel;

    public TimerExpandedView(IAppLocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => AttachViewModel();

    private void OnUnloaded(object sender, RoutedEventArgs args) => DetachViewModel();

    private void OnDurationEditorGotFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.Begin(sender);

    private void OnDurationEditorLostFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.End(sender);

    private void OnPresetClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: TimerPresetOption preset } &&
            _viewModel?.StartPresetCommand.CanExecute(preset) == true)
        {
            _viewModel.StartPresetCommand.Execute(preset);
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        DetachViewModel();
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_viewModel == DataContext as TimeToolsViewModel)
        {
            ApplyLocalization();
            return;
        }

        _viewModel = DataContext as TimeToolsViewModel;
        ApplyLocalization();
    }

    private void DetachViewModel()
    {
        DockInteractionSession.End(HoursBox);
        DockInteractionSession.End(MinutesBox);
        DockInteractionSession.End(SecondsBox);
        _viewModel = null;
    }

    private void ApplyLocalization()
    {
        if (_localization is not null)
        {
            DispatcherQueue.TryEnqueue(() => _localization.Apply(this));
        }
    }
}
