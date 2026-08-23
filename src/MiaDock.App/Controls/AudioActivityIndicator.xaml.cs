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
    private double _leftVisualLevel = 0.18;
    private double _centerVisualLevel = 0.18;
    private double _rightVisualLevel = 0.18;
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
            ApplyLevels(0.18, 0.18, 0.18);
            return;
        }

        var peak = Math.Max(LeftLevel, Math.Max(CenterLevel, RightLevel));
        if (!IsAudioAvailable || peak <= 0.205)
        {
            // A source can report playback before it exposes meter samples.
            // Keep the indicator intentional and stable instead of inventing
            // a perpetual unequal waveform.
            ApplyStableLevels(0.52, 0.52, 0.52);
            return;
        }

        ApplyLevels(LeftLevel, CenterLevel, RightLevel);
    }

    private void ApplyLevels(double left, double center, double right)
    {
        _leftVisualLevel = ResolveVisualLevel(_leftVisualLevel, left);
        _centerVisualLevel = ResolveVisualLevel(_centerVisualLevel, center);
        _rightVisualLevel = ResolveVisualLevel(_rightVisualLevel, right);
        LeftScale.ScaleY = _leftVisualLevel;
        CenterScale.ScaleY = _centerVisualLevel;
        RightScale.ScaleY = _rightVisualLevel;
    }

    private void ApplyStableLevels(double left, double center, double right)
    {
        _leftVisualLevel = Math.Clamp(left, 0.18, 1);
        _centerVisualLevel = Math.Clamp(center, 0.18, 1);
        _rightVisualLevel = Math.Clamp(right, 0.18, 1);
        LeftScale.ScaleY = _leftVisualLevel;
        CenterScale.ScaleY = _centerVisualLevel;
        RightScale.ScaleY = _rightVisualLevel;
    }

    private static double ResolveVisualLevel(double current, double target)
    {
        var safeTarget = Math.Clamp(target, 0.18, 1);
        return Math.Abs(safeTarget - 0.18) < 0.001
            ? safeTarget
            : Math.Clamp(current + ((safeTarget - current) * 0.45), 0.18, 1);
    }
}
