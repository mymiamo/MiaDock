using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MiaDock.Core.Settings;
using Windows.System;
using Windows.UI.Core;

namespace MiaDock.App.Controls;

public sealed partial class HotKeyRecorderControl : UserControl
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture),
        typeof(HotKeyGestureSetting),
        typeof(HotKeyRecorderControl),
        new PropertyMetadata(null, OnGestureChanged));

    public static readonly DependencyProperty DefaultGestureProperty = DependencyProperty.Register(
        nameof(DefaultGesture),
        typeof(HotKeyGestureSetting),
        typeof(HotKeyRecorderControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(HotKeyRecorderControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AccessibleNameProperty = DependencyProperty.Register(
        nameof(AccessibleName),
        typeof(string),
        typeof(HotKeyRecorderControl),
        new PropertyMetadata("Global kısayolu kaydet"));

    private bool _recording;

    public HotKeyRecorderControl()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public HotKeyGestureSetting? Gesture
    {
        get => (HotKeyGestureSetting?)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public HotKeyGestureSetting? DefaultGesture
    {
        get => (HotKeyGestureSetting?)GetValue(DefaultGestureProperty);
        set => SetValue(DefaultGestureProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string AccessibleName
    {
        get => (string)GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }

    private static void OnGestureChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((HotKeyRecorderControl)sender).UpdateDisplay();

    private void OnRecorderClicked(object sender, RoutedEventArgs args)
    {
        _recording = true;
        RecorderButton.Content = "Tuş kombinasyonuna basın…";
        RecorderButton.Focus(FocusState.Programmatic);
    }

    private void OnRecorderKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!_recording) return;

        if (args.Key == VirtualKey.Tab)
        {
            StopRecording();
            return;
        }

        args.Handled = true;
        if (args.Key == VirtualKey.Escape)
        {
            StopRecording();
            return;
        }

        if (args.Key is VirtualKey.Back or VirtualKey.Delete)
        {
            Gesture = null;
            StopRecording();
            return;
        }

        if (args.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or
            VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            return;
        }

        var modifiers = HotKeyModifiers.None;
        if (IsDown(VirtualKey.Control)) modifiers |= HotKeyModifiers.Control;
        if (IsDown(VirtualKey.Menu)) modifiers |= HotKeyModifiers.Alt;
        if (IsDown(VirtualKey.Shift)) modifiers |= HotKeyModifiers.Shift;
        var gesture = new HotKeyGestureSetting(modifiers, (int)args.Key);
        if (!HotKeyGestureValidator.IsValid(gesture))
        {
            Gesture = gesture;
            StopRecording();
            return;
        }

        Gesture = gesture;
        StopRecording();
    }

    private void OnRecorderLostFocus(object sender, RoutedEventArgs args)
    {
        if (_recording) StopRecording();
    }

    private void OnClearClicked(object sender, RoutedEventArgs args) => Gesture = null;

    private void OnRestoreDefaultClicked(object sender, RoutedEventArgs args)
    {
        if (DefaultGesture is not null)
        {
            Gesture = DefaultGesture;
        }
    }

    private void StopRecording()
    {
        _recording = false;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (RecorderButton is null || _recording) return;
        RecorderButton.Content = Gesture is null ? "Atanmamış" : Format(Gesture);
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private static string Format(HotKeyGestureSetting gesture)
    {
        var parts = new List<string>(4);
        if (gesture.Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(((VirtualKey)gesture.VirtualKey).ToString());
        return string.Join(" + ", parts);
    }
}
