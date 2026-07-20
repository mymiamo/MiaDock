using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Controls;

public sealed partial class IslandShell : UserControl
{
    private readonly CollapsedIslandView _collapsedView;
    private readonly HoverIslandView _hoverView;
    private readonly ExpandedMusicView _expandedView;
    private readonly TrackNotificationView _notificationView;
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(string),
        typeof(IslandShell),
        new PropertyMetadata(nameof(IslandVisualState.Collapsed), OnStateChanged));

    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme),
        typeof(string),
        typeof(IslandShell),
        new PropertyMetadata(string.Empty, OnThemeChanged));

    public static readonly DependencyProperty MediaContextProperty = DependencyProperty.Register(
        nameof(MediaContext),
        typeof(object),
        typeof(IslandShell),
        new PropertyMetadata(null, OnMediaContextChanged));

    public IslandShell()
    {
        InitializeComponent();

        _collapsedView = new CollapsedIslandView();
        _hoverView = new HoverIslandView();
        _expandedView = new ExpandedMusicView();
        _notificationView = new TrackNotificationView();

        ContentHost.Children.Add(_collapsedView);
        ContentHost.Children.Add(_hoverView);
        ContentHost.Children.Add(_expandedView);
        ContentHost.Children.Add(_notificationView);

        ApplyState(State);
    }

    public string State
    {
        get => (string)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public object? MediaContext
    {
        get => GetValue(MediaContextProperty);
        set => SetValue(MediaContextProperty, value);
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is IslandShell shell && args.NewValue is string state)
        {
            shell.ApplyState(state);
        }
    }

    private static void OnThemeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is IslandShell shell)
        {
            // Theme resources on the visual tree update automatically.
        }
    }

    private static void OnMediaContextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is IslandShell shell)
        {
            shell.ContentHost.DataContext = args.NewValue;
        }
    }

    private void ApplyState(string stateName)
    {
        if (!Enum.TryParse<IslandVisualState>(stateName, out var state))
        {
            state = IslandVisualState.Collapsed;
        }

        (LayoutRoot.Width, LayoutRoot.Height) = state switch
        {
            IslandVisualState.Collapsed => (184, 40),
            IslandVisualState.Hover => (300, 72),
            IslandVisualState.ExpandedMusic => (440, 210),
            IslandVisualState.TrackNotification => (360, 92),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
        _collapsedView.Visibility = state == IslandVisualState.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        _hoverView.Visibility = state == IslandVisualState.Hover ? Visibility.Visible : Visibility.Collapsed;
        _expandedView.Visibility = state == IslandVisualState.ExpandedMusic ? Visibility.Visible : Visibility.Collapsed;
        _notificationView.Visibility = state == IslandVisualState.TrackNotification ? Visibility.Visible : Visibility.Collapsed;
    }

}
