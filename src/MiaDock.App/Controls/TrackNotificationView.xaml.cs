using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MiaDock.App.Controls;

public sealed partial class TrackNotificationView : UserControl
{
    public static readonly DependencyProperty ShowControlsProperty = DependencyProperty.Register(
        nameof(ShowControls), typeof(bool), typeof(TrackNotificationView),
        new PropertyMetadata(false, OnShowControlsChanged));

    public TrackNotificationView() => InitializeComponent();

    public bool ShowControls
    {
        get => (bool)GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    private static void OnShowControlsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((TrackNotificationView)sender).TransportControls.Visibility = (bool)args.NewValue
            ? Visibility.Visible
            : Visibility.Collapsed;
}
