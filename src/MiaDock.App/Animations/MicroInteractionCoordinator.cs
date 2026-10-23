using System.Numerics;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using MiaDock.App.Services;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;

namespace MiaDock.App.Animations;

/// <summary>
/// Runs one short composition animation per target. Every new request cancels
/// the previous request and restores the visual to its neutral state.
/// </summary>
public sealed class MicroInteractionCoordinator : IMicroInteractionCoordinator
{
    private readonly ISettingsService _settings;
    private readonly IAnimationPreferenceService _preferences;
    private readonly object _gate = new();
    private readonly Dictionary<FrameworkElement, Session> _sessions = new();
    private long _sequence;
    private bool _disposed;

    public MicroInteractionCoordinator(
        ISettingsService settings,
        IAnimationPreferenceService preferences)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _settings.SettingsChanged += OnSettingsChanged;
        _preferences.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
    }

    public void PlayPress(FrameworkElement target) => Start(target, MicroInteractionKind.Press);

    public void PlayStateChange(FrameworkElement target) => Start(target, MicroInteractionKind.StateChange);

    public void PlayAttention(FrameworkElement target) => Start(target, MicroInteractionKind.Attention);

    public void PlayNotification(FrameworkElement target) => Start(target, MicroInteractionKind.Notification);

    public void Cancel(FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.DispatcherQueue.HasThreadAccess) CancelCore(target);
        else target.DispatcherQueue.TryEnqueue(() => CancelCore(target));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.SettingsChanged -= OnSettingsChanged;
        _preferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        CancelAll();
    }

    private void Start(FrameworkElement target, MicroInteractionKind kind)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.DispatcherQueue.HasThreadAccess)
        {
            target.DispatcherQueue.TryEnqueue(() => Start(target, kind));
            return;
        }
        if (_disposed || !CanAnimate || !MicroMotion.IsPresented(target))
        {
            CancelCore(target);
            return;
        }

        var cancellation = new CancellationTokenSource();
        Session session;
        lock (_gate)
        {
            if (_sessions.Remove(target, out var previous))
            {
                previous.Cancellation.Cancel();
                previous.Cancellation.Dispose();
            }

            session = new Session(Interlocked.Increment(ref _sequence), cancellation);
            _sessions[target] = session;
        }

        _ = RunAsync(target, kind, session);
    }

    private async Task RunAsync(FrameworkElement target, MicroInteractionKind kind, Session session)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        ElementCompositionPreview.SetIsTranslationEnabled(target, true);
        ToolkitAnimationFactory.Reset(visual);
        visual.CenterPoint = new Vector3(
            (float)(Math.Max(target.ActualWidth, 1) / 2),
            (float)(Math.Max(target.ActualHeight, 1) / 2),
            0);

        try
        {
            var options = ResolveMotion(_settings.Current.Appearance);
            var intensity = Math.Clamp(options.Intensity, 0, 1);
            var speed = ResolveSpeed(options);
            var token = session.Cancellation.Token;

            switch (kind)
            {
                case MicroInteractionKind.Press:
                    await AnimationBuilder.Create()
                        .Scale(1 - (0.035 * Math.Max(intensity, 0.45)), duration: Scale(TimeSpan.FromMilliseconds(90), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .StartAsync(target, token);
                    await AnimationBuilder.Create()
                        .Scale(1, duration: Scale(TimeSpan.FromMilliseconds(140), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .StartAsync(target, token);
                    break;

                case MicroInteractionKind.Notification:
                    await AnimationBuilder.Create()
                        .Opacity(1, from: 0.6, duration: Scale(TimeSpan.FromMilliseconds(200), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .Scale(1, from: 0.98, duration: Scale(TimeSpan.FromMilliseconds(200), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .Translation(Axis.Y, 0, from: 3, duration: Scale(TimeSpan.FromMilliseconds(200), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .StartAsync(target, token);
                    break;

                case MicroInteractionKind.Attention:
                    await AnimationBuilder.Create()
                        .Scale(1, from: 0.965, duration: Scale(TimeSpan.FromMilliseconds(180), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .Opacity(1, from: 0.72, duration: Scale(TimeSpan.FromMilliseconds(180), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .StartAsync(target, token);
                    break;

                default:
                    await AnimationBuilder.Create()
                        .Opacity(1, from: 0.7, duration: Scale(TimeSpan.FromMilliseconds(160), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .Scale(1, from: 0.985, duration: Scale(TimeSpan.FromMilliseconds(160), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .Translation(Axis.Y, 0, from: 2, duration: Scale(TimeSpan.FromMilliseconds(160), speed), easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
                        .StartAsync(target, token);
                    break;
            }
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
        {
            // A newer state owns this target now.
        }
        finally
        {
            lock (_gate)
            {
                if (_sessions.TryGetValue(target, out var active) && active.Sequence == session.Sequence)
                {
                    _sessions.Remove(target);
                    session.Cancellation.Dispose();
                    ToolkitAnimationFactory.Reset(visual);
                }
            }
        }
    }

    private bool CanAnimate
    {
        get
        {
            var motion = ResolveMotion(_settings.Current.Appearance);
            return _preferences.AnimationsEnabled &&
                   motion.EnableIconAnimations &&
                   motion.Preset != MotionPreset.Off;
        }
    }

    private static double ResolveSpeed(MotionSettings options) =>
        Math.Clamp(options.Speed, 0.5, 2);

    private static MotionSettings ResolveMotion(AppearanceSettings appearance) =>
        appearance.Motion ?? MotionSettings.FromLegacy(appearance.AnimationKind, appearance.AnimationSpeed);

    private static TimeSpan Scale(TimeSpan duration, double speed) =>
        TimeSpan.FromMilliseconds(Math.Max(1, duration.TotalMilliseconds / speed));

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (ResolveMotion(args.Previous.Appearance) != ResolveMotion(args.Current.Appearance))
        {
            CancelAll();
        }
    }

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args) => CancelAll();

    private void CancelAll()
    {
        KeyValuePair<FrameworkElement, Session>[] targets;
        lock (_gate)
        {
            targets = _sessions.ToArray();
        }

        foreach (var (target, session) in targets)
        {
            if (target.DispatcherQueue.HasThreadAccess)
            {
                CancelCore(target, session.Sequence);
            }
            else
            {
                target.DispatcherQueue.TryEnqueue(() => CancelCore(target, session.Sequence));
            }
        }
    }

    private void CancelCore(FrameworkElement target, long? expectedSequence = null)
    {
        Session? session = null;
        lock (_gate)
        {
            if (expectedSequence is { } expected &&
                (!_sessions.TryGetValue(target, out var current) || current.Sequence != expected)) return;
            if (_sessions.Remove(target, out var active))
            {
                session = active;
            }
        }

        if (session is not null)
        {
            session.Cancellation.Cancel();
            session.Cancellation.Dispose();
        }

        if (target.DispatcherQueue.HasThreadAccess)
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(target));
        }
    }

    private sealed record Session(long Sequence, CancellationTokenSource Cancellation);

    private enum MicroInteractionKind
    {
        Press,
        StateChange,
        Attention,
        Notification
    }
}
