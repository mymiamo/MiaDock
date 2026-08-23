using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using MiaDock.App.Services;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;

namespace MiaDock.App.Controls;

/// <summary>
/// Renders the selected media session's three real audio levels. This control
/// owns only its bar visuals, so it never competes with island shell motion.
/// </summary>
public sealed partial class AudioActivityIndicator : UserControl
{
    private const double MinimumScale = 0.18;
    private const double RealSignalThreshold = MinimumScale + 0.025;
    private const long FallbackGracePeriodMilliseconds = 550;
    private static readonly TimeSpan AttackDuration = TimeSpan.FromMilliseconds(48);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(128);
    private static readonly TimeSpan FallbackFrameInterval = TimeSpan.FromMilliseconds(90);

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
    private DispatcherQueueTimer? _fallbackTimer;
    private bool _isLoaded;
    private bool _isFallbackAnimating;
    private long _flatSignalStartedAt;
    private double _fallbackPhase;
    private double _trebleTarget = MinimumScale;
    private double _bassTarget = MinimumScale;
    private double _vocalTarget = MinimumScale;

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
        StopFallbackMotion(resetPhase: true);
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
            StopFallbackMotion(resetPhase: true);
            ResetVisuals();
            return;
        }

        EnsureVisuals();
        if (IsSuppressed)
        {
            StopFallbackMotion(resetPhase: true);
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        if (!IsPlaying)
        {
            StopFallbackMotion(resetPhase: true);
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        if (HasRealSignal)
        {
            StopFallbackMotion(resetPhase: false);
            ApplyLevels(TrebleLevel, BassLevel, VocalLevel, animate: CanAnimate);
            return;
        }

        if (!CanAnimate)
        {
            StopFallbackMotion(resetPhase: true);
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        StartFallbackMotion();
        if (!_isFallbackAnimating)
        {
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
        }
    }

    private bool HasRealSignal =>
        IsAudioAvailable &&
        Math.Max(TrebleLevel, Math.Max(BassLevel, VocalLevel)) > RealSignalThreshold;

    private void StartFallbackMotion()
    {
        if (_flatSignalStartedAt == 0)
        {
            _flatSignalStartedAt = Environment.TickCount64;
        }

        if (_fallbackTimer is null)
        {
            _fallbackTimer = DispatcherQueue.CreateTimer();
            _fallbackTimer.Interval = FallbackFrameInterval;
            _fallbackTimer.IsRepeating = true;
            _fallbackTimer.Tick += OnFallbackTick;
        }

        if (!_fallbackTimer.IsRunning)
        {
            _fallbackTimer.Start();
        }
    }

    private void OnFallbackTick(DispatcherQueueTimer sender, object args)
    {
        if (!_isLoaded ||
            Visibility != Visibility.Visible ||
            IsSuppressed ||
            !IsPlaying ||
            !CanAnimate)
        {
            StopFallbackMotion(resetPhase: true);
            ApplyLevels(MinimumScale, MinimumScale, MinimumScale, animate: false);
            return;
        }

        if (HasRealSignal)
        {
            UpdateVisuals();
            return;
        }

        if (Environment.TickCount64 - _flatSignalStartedAt < FallbackGracePeriodMilliseconds)
        {
            return;
        }

        _isFallbackAnimating = true;
        _fallbackPhase += 0.55;
        var levels = CreateFallbackLevels(_fallbackPhase);
        ApplyLevels(levels.Treble, levels.Bass, levels.Vocal, animate: true);
    }

    internal static (double Treble, double Bass, double Vocal) CreateFallbackLevels(double phase)
    {
        // This deliberately looks like a quiet three-band meter instead of a
        // repeating pulse. It is used only when Windows supplies no usable
        // process/session/endpoint level while playback is still active.
        var treble = 0.28 + (0.42 * ((Math.Sin((phase * 1.73) + 1.2) + 1) / 2));
        var bass = 0.36 + (0.52 * ((Math.Sin(phase * 0.83) + 1) / 2));
        var vocal = 0.32 + (0.44 * ((Math.Sin((phase * 1.21) + 2.2) + 1) / 2));
        return (treble, bass, vocal);
    }

    private void StopFallbackMotion(bool resetPhase)
    {
        _fallbackTimer?.Stop();
        _flatSignalStartedAt = 0;
        _isFallbackAnimating = false;
        if (resetPhase)
        {
            _fallbackPhase = 0;
        }
    }

    private void EnsureVisuals()
    {
        _trebleVisual ??= ElementCompositionPreview.GetElementVisual(TrebleBar);
        _bassVisual ??= ElementCompositionPreview.GetElementVisual(BassBar);
        _vocalVisual ??= ElementCompositionPreview.GetElementVisual(VocalBar);
        ConfigureVisual(_trebleVisual, TrebleBar);
        ConfigureVisual(_bassVisual, BassBar);
        ConfigureVisual(_vocalVisual, VocalBar);
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
