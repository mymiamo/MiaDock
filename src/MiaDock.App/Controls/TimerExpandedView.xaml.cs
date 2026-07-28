using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;

namespace MiaDock.App.Controls;

public sealed partial class TimerExpandedView : UserControl
{
    private readonly IAppLocalizationService? _localization;

    public TimerExpandedView(IAppLocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    private void OnToolSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_localization is not null)
        {
            DispatcherQueue.TryEnqueue(() => _localization.Apply(this));
        }
    }
}
