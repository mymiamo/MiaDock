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

        var modifiers = HotKeyModifiers.None;
        if (IsDown(VirtualKey.Control)) modifiers |= HotKeyModifiers.Control;
        if (IsDown(VirtualKey.Menu)) modifiers |= HotKeyModifiers.Alt;
        if (IsDown(VirtualKey.Shift)) modifiers |= HotKeyModifiers.Shift;
        var gesture = new HotKeyGestureSetting(modifiers, (int)args.Key);
        if (!HotKeyGestureValidator.IsValid(gesture))
        {
            RecorderButton.Content = args.Key == VirtualKey.F12
                ? "F12 kullanılamaz"
                : "Ctrl, Alt veya Shift ekleyin";
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
