using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using MiaDock.App.Services;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.Models;

namespace MiaDock.App.Controls;

/// <summary>
/// Renders the selected media session's three real audio levels. This control
/// owns only its bar visuals, so it never competes with island shell motion.
/// </summary>
public sealed partial class AudioActivityIndicator : UserControl
{
    private const double MinimumScale = 0.18;
    private static readonly TimeSpan AttackDuration = TimeSpan.FromMilliseconds(48);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(128);

    public static readonly DependencyProperty TrebleLevelProperty = DependencyProperty.Register(
        nameof(TrebleLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(MinimumScale, OnVisualPropertyChanged));
    public static readonly DependencyProperty BassLevelProperty = DependencyProperty.Register(
        nameof(BassLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(MinimumScale, OnVisualPropertyChanged));
    public static readonly DependencyProperty VocalLevelProperty = DependencyProperty.Register(
        nameof(VocalLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(MinimumScale, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(AudioActivityIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsAudioAvailableProperty = DependencyProperty.Register(
        nameof(IsAudioAvailable), typeof(bool), typeof(AudioActivityIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));
    public static readonly DependencyProperty AudioLevelSourceProperty = DependencyProperty.Register(
        nameof(AudioLevelSource), typeof(MediaAudioLevelSource), typeof(AudioActivityIndicator),
        new PropertyMetadata(MediaAudioLevelSource.Unavailable, OnVisualPropertyChanged));
    public static readonly DependencyProperty AmplitudeLevelProperty = DependencyProperty.Register(
        nameof(AmplitudeLevel), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(0d, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsSuppressedProperty = DependencyProperty.Register(
        nameof(IsSuppressed), typeof(bool), typeof(AudioActivityIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));
    public static readonly DependencyProperty BarWidthProperty = DependencyProperty.Register(
        nameof(BarWidth), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(2d, OnVisualPropertyChanged));
    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(14d, OnVisualPropertyChanged));
    public static readonly DependencyProperty BarSpacingProperty = DependencyProperty.Register(
        nameof(BarSpacing), typeof(double), typeof(AudioActivityIndicator),
        new PropertyMetadata(1.5d, OnVisualPropertyChanged));

    private readonly long _visibilityRegistration;
    private ISettingsService? _settings;
    private IAnimationPreferenceService? _animationPreferences;
    private Visual? _trebleVisual;
    private Visual? _bassVisual;
    private Visual? _vocalVisual;
    private bool _isLoaded;
    private double _trebleTarget = MinimumScale;
    private double _bassTarget = MinimumScale;
    private double _vocalTarget = MinimumScale;
    private double _amplitudeLeading = MinimumScale;
    private double _amplitudeCenter = MinimumScale;
    private double _amplitudeTrailing = MinimumScale;

    public AudioActivityIndicator()
    {
        InitializeComponent();
        _visibilityRegistration = RegisterPropertyChangedCallback(
            VisibilityProperty,
            (_, _) => UpdateVisuals());
        SizeChanged += OnSizeChanged;
    }

    public double TrebleLevel { get => (double)GetValue(TrebleLevelProperty); set => SetValue(TrebleLevelProperty, value); }
    public double BassLevel { get => (double)GetValue(BassLevelProperty); set => SetValue(BassLevelProperty, value); }
    public double VocalLevel { get => (double)GetValue(VocalLevelProperty); set => SetValue(VocalLevelProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool IsAudioAvailable { get => (bool)GetValue(IsAudioAvailableProperty); set => SetValue(IsAudioAvailableProperty, value); }
    public MediaAudioLevelSource AudioLevelSource { get => (MediaAudioLevelSource)GetValue(AudioLevelSourceProperty); set => SetValue(AudioLevelSourceProperty, value); }
    public double AmplitudeLevel { get => (double)GetValue(AmplitudeLevelProperty); set => SetValue(AmplitudeLevelProperty, value); }
    public bool IsSuppressed { get => (bool)GetValue(IsSuppressedProperty); set => SetValue(IsSuppressedProperty, value); }
    public double BarWidth { get => (double)GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }
    public double BarHeight { get => (double)GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double BarSpacing { get => (double)GetValue(BarSpacingProperty); set => SetValue(BarSpacingProperty, value); }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        _settings = App.Services?.GetService<ISettingsService>();
        _animationPreferences = App.Services?.GetService<IAnimationPreferenceService>();
        if (_settings is not null)
        {
            _settings.SettingsChanged += OnSettingsChanged;
        }

        if (_animationPreferences is not null)
        {
            _animationPreferences.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        }

        EnsureVisuals();
        UpdateVisuals();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        if (_settings is not null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
            _settings = null;
        }

        if (_animationPreferences is not null)
        {
            _animationPreferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
            _animationPreferences = null;
        }

        ResetVisuals();
    }

    private static void OnVisualPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((AudioActivityIndicator)sender).UpdateVisuals();

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_isLoaded)
        {
            EnsureVisuals();
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args) => UpdateVisuals();

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args) => UpdateVisuals();

    private void UpdateVisuals()
    {
        if (!_isLoaded || Visibility != Visibility.Visible)
        {
            ResetVisuals();
            return;
        }

        EnsureVisuals();
        if (IsSuppressed)
        {
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        if (!IsPlaying)
        {
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        if (AudioLevelSource is MediaAudioLevelSource.ProcessSpectrum or
            MediaAudioLevelSource.EndpointSpectrum)
        {
            ApplyLevels(TrebleLevel, BassLevel, VocalLevel, animate: CanAnimate);
            return;
        }

        if (AudioLevelSource == MediaAudioLevelSource.SessionPeak)
        {
            var levels = UpdateAmplitudeLevels(AmplitudeLevel);
            ApplyLevels(levels.Leading, levels.Center, levels.Trailing, animate: CanAnimate);
            return;
        }

        // No fabricated movement: when Windows exposes neither spectrum nor
        // session amplitude, use exactly the non-playing baseline state.
        ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
    }

    private (double Leading, double Center, double Trailing) UpdateAmplitudeLevels(double amplitude)
    {
        var normalized = Math.Clamp(amplitude, 0, 1);
        var leadingTarget = MinimumScale + normalized * 0.72;
        var centerTarget = MinimumScale + Math.Pow(normalized, 0.72) * (1 - MinimumScale);
        var trailingTarget = MinimumScale + normalized * 0.82;
        _amplitudeLeading = FollowAmplitude(_amplitudeLeading, leadingTarget, 0.9, 0.34);
        _amplitudeCenter = FollowAmplitude(_amplitudeCenter, centerTarget, 0.66, 0.18);
        _amplitudeTrailing = FollowAmplitude(_amplitudeTrailing, trailingTarget, 0.78, 0.25);
        return (_amplitudeLeading, _amplitudeCenter, _amplitudeTrailing);
    }

    private static double FollowAmplitude(
        double current,
        double target,
        double attack,
        double release) =>
        current + (target - current) * (target >= current ? attack : release);

    private void EnsureVisuals()
    {
        _trebleVisual ??= ElementCompositionPreview.GetElementVisual(LeadingBar);
        _bassVisual ??= ElementCompositionPreview.GetElementVisual(CenterBar);
        _vocalVisual ??= ElementCompositionPreview.GetElementVisual(TrailingBar);
        ConfigureVisual(_trebleVisual, LeadingBar);
        ConfigureVisual(_bassVisual, CenterBar);
        ConfigureVisual(_vocalVisual, TrailingBar);
    }

    private static void ConfigureVisual(Visual visual, FrameworkElement bar)
    {
        visual.CenterPoint = new Vector3(
            (float)(Math.Max(bar.ActualWidth, 1) / 2),
            (float)(Math.Max(bar.ActualHeight, 1) / 2),
            0);
    }

    private void ApplyLevels(double treble, double bass, double vocal, bool animate)
    {
        ApplyLevel(_trebleVisual, ref _trebleTarget, treble, animate);
        ApplyLevel(_bassVisual, ref _bassTarget, bass, animate);
        ApplyLevel(_vocalVisual, ref _vocalTarget, vocal, animate);
    }

    private static void ApplyLevel(Visual? visual, ref double previousTarget, double target, bool animate)
    {
        if (visual is null)
        {
            return;
        }

        var safeTarget = Math.Clamp(target, MinimumScale, 1);
        if (!animate)
        {
            visual.StopAnimation("Scale.Y");
            visual.Scale = new Vector3(1, (float)safeTarget, 1);
            previousTarget = safeTarget;
            return;
        }

        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = safeTarget >= previousTarget ? AttackDuration : ReleaseDuration;
        animation.InsertKeyFrame(
            1,
            (float)safeTarget,
            visual.Compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0),
                new Vector2(0, 1)));
        // A new value replaces the previous Scale.Y animation while Composition
        // continues from the level currently presented on screen.
        visual.StartAnimation("Scale.Y", animation);
        previousTarget = safeTarget;
    }

    private bool CanAnimate
    {
        get
        {
            var appearance = _settings?.Current.Appearance;
            var motion = appearance is null
                ? MotionSettings.Default
                : appearance.Motion ?? MotionSettings.FromLegacy(
                    appearance.AnimationKind,
                    appearance.AnimationSpeed);
            return _animationPreferences?.AnimationsEnabled == true &&
                   motion.EnableIconAnimations &&
                   motion.Preset != MotionPreset.Off;
        }
    }

    private void ResetVisuals()
    {
        ResetVisual(_trebleVisual);
        ResetVisual(_bassVisual);
        ResetVisual(_vocalVisual);
        _trebleTarget = _bassTarget = _vocalTarget = MinimumScale;
        _amplitudeLeading = _amplitudeCenter = _amplitudeTrailing = MinimumScale;
    }

    private static void ResetVisual(Visual? visual)
    {
        if (visual is null)
        {
            return;
        }

        visual.StopAnimation("Scale.Y");
        visual.Scale = new Vector3(1, (float)MinimumScale, 1);
    }
}
