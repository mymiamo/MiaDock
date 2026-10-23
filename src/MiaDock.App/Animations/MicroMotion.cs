using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MiaDock.App.Animations;

/// <summary>
/// Small XAML-facing bridge for press and state-change feedback.
/// </summary>
public static class MicroMotion
{
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.RegisterAttached(
        "IsInteractive", typeof(bool), typeof(MicroMotion), new PropertyMetadata(false, OnInteractiveChanged));

    public static readonly DependencyProperty StateKeyProperty = DependencyProperty.RegisterAttached(
        "StateKey", typeof(object), typeof(MicroMotion), new PropertyMetadata(null, OnStateKeyChanged));

    private static readonly DependencyProperty IsWiredProperty = DependencyProperty.RegisterAttached(
        "IsWired", typeof(bool), typeof(MicroMotion), new PropertyMetadata(false));

    public static bool GetIsInteractive(DependencyObject element) => (bool)element.GetValue(IsInteractiveProperty);

    public static void SetIsInteractive(DependencyObject element, bool value) => element.SetValue(IsInteractiveProperty, value);

    public static object? GetStateKey(DependencyObject element) => element.GetValue(StateKeyProperty);

    public static void SetStateKey(DependencyObject element, object? value) => element.SetValue(StateKeyProperty, value);

    public static void PlayAttention(FrameworkElement element) { if (IsPresented(element)) Coordinator?.PlayAttention(element); }

    public static void PlayNotification(FrameworkElement element) { if (IsPresented(element)) Coordinator?.PlayNotification(element); }
    internal static bool IsPresented(FrameworkElement element)
    {
        if (!element.IsLoaded) return false;
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }
    public static void CancelSubtree(DependencyObject root)
    {
        if (root is FrameworkElement element) Coordinator?.Cancel(element);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) CancelSubtree(VisualTreeHelper.GetChild(root, i));
    }

    private static IMicroInteractionCoordinator? Coordinator =>
        App.Services?.GetService<IMicroInteractionCoordinator>();

    private static void OnInteractiveChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.PointerPressed -= OnPointerPressed;
        control.KeyDown -= OnKeyDown;
        if (!(bool)args.NewValue) { Coordinator?.Cancel(control); return; }
        EnsureWired(control);
        control.PointerPressed += OnPointerPressed;
        control.KeyDown += OnKeyDown;
    }

    private static void OnStateKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement element && IsPresented(element) && args.OldValue is not null)
        {
            Coordinator?.PlayStateChange(element);
        }
    }

    private static void EnsureWired(FrameworkElement element)
    {
        if ((bool)element.GetValue(IsWiredProperty))
        {
            return;
        }

        element.SetValue(IsWiredProperty, true);
        element.Unloaded += OnUnloaded;
    }

    private static void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            Coordinator?.PlayPress(element);
        }
    }

    private static void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!args.KeyStatus.WasKeyDown && sender is FrameworkElement element)
        {
            Coordinator?.PlayPress(element);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            Coordinator?.Cancel(element);
        }
    }
}
