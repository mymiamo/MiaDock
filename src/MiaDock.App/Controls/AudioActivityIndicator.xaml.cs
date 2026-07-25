using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MiaDock.App.Controls;

public sealed partial class AudioActivityIndicator : UserControl
{
    public static readonly DependencyProperty LeftLevelProperty = DependencyProperty.Register(
        nameof(LeftLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(0.18, OnVisualPropertyChanged));
    public static readonly DependencyProperty CenterLevelProperty = DependencyProperty.Register(
        nameof(CenterLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(0.18, OnVisualPropertyChanged));
    public static readonly DependencyProperty RightLevelProperty = DependencyProperty.Register(
        nameof(RightLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(0.18, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(AudioActivityIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsAudioAvailableProperty = DependencyProperty.Register(
        nameof(IsAudioAvailable), typeof(bool), typeof(AudioActivityIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    private bool _isLoaded;
    private bool _isFallbackRunning;

    public AudioActivityIndicator() => InitializeComponent();

    public double LeftLevel { get => (double)GetValue(LeftLevelProperty); set => SetValue(LeftLevelProperty, value); }
    public double CenterLevel { get => (double)GetValue(CenterLevelProperty); set => SetValue(CenterLevelProperty, value); }
    public double RightLevel { get => (double)GetValue(RightLevelProperty); set => SetValue(RightLevelProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool IsAudioAvailable { get => (bool)GetValue(IsAudioAvailableProperty); set => SetValue(IsAudioAvailableProperty, value); }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        UpdateVisuals();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        StopFallbackAnimation();
    }

    private static void OnVisualPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((AudioActivityIndicator)sender).UpdateVisuals();

    private void UpdateVisuals()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (!IsPlaying)
        {
            StopFallbackAnimation();
            ApplyLevels(0.18, 0.18, 0.18);
            return;
        }

        var peak = Math.Max(LeftLevel, Math.Max(CenterLevel, RightLevel));
        if (!IsAudioAvailable || peak <= 0.205)
        {
            StartFallbackAnimation();
            return;
        }

        StopFallbackAnimation();
        ApplyLevels(LeftLevel, CenterLevel, RightLevel);
    }

    private void StartFallbackAnimation()
    {
        if (_isFallbackRunning)
        {
            return;
        }

        FallbackStoryboard.Begin();
        _isFallbackRunning = true;
    }

    private void StopFallbackAnimation()
    {
        if (!_isFallbackRunning)
        {
            return;
        }

        FallbackStoryboard.Stop();
        _isFallbackRunning = false;
    }

    private void ApplyLevels(double left, double center, double right)
    {
        LeftScale.ScaleY = Math.Clamp(left, 0.18, 1);
        CenterScale.ScaleY = Math.Clamp(center, 0.18, 1);
        RightScale.ScaleY = Math.Clamp(right, 0.18, 1);
    }
}
