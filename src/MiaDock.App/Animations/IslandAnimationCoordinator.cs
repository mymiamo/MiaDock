using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using MiaDock.App.Services;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public sealed class IslandAnimationCoordinator : IIslandAnimationCoordinator
{
    private readonly FrameworkElement _layoutRoot;
    private readonly Border _surface;
    private readonly IReadOnlyDictionary<IslandVisualState, FrameworkElement> _views;
    private readonly Action<IslandVisualMetrics> _applyMetrics;
    private readonly IAnimationPreferenceService _animationPreferences;
    private readonly IslandMotionOptions _options;
    private readonly IslandLayoutOptions _layoutOptions;
    private readonly IslandBoundsAnimator _boundsAnimator = new();
    private readonly CompositionAnimationFactory _compositionAnimations = new();
    private CancellationTokenSource? _animationCancellation;
    private IslandVisualState _activeState = IslandVisualState.Collapsed;
    private long _sequence;
    private bool _disposed;

    public IslandAnimationCoordinator(
        FrameworkElement layoutRoot,
        Border surface,
        IReadOnlyDictionary<IslandVisualState, FrameworkElement> views,
        Action<IslandVisualMetrics> applyMetrics,
        IAnimationPreferenceService animationPreferences,
        IslandMotionOptions options,
        IslandLayoutOptions layoutOptions)
    {
        _layoutRoot = layoutRoot ?? throw new ArgumentNullException(nameof(layoutRoot));
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _views = views ?? throw new ArgumentNullException(nameof(views));
        _applyMetrics = applyMetrics ?? throw new ArgumentNullException(nameof(applyMetrics));
        _animationPreferences = animationPreferences ?? throw new ArgumentNullException(nameof(animationPreferences));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _layoutOptions = layoutOptions ?? throw new ArgumentNullException(nameof(layoutOptions));
        _options.Validate();
        _animationPreferences.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
    }

    public bool IsAnimating => _boundsAnimator.IsRunning || _animationCancellation is not null;

    public void ApplyInitialState(IslandVisualState state)
    {
        ThrowIfDisposed();
        CancelActiveAnimation();
        _activeState = state;
        ApplyFinalState(state);
    }

    public void RequestTransition(IslandTransition transition)
    {
        ThrowIfDisposed();

        if (!transition.Changed)
        {
            return;
        }

        var sequence = ++_sequence;
        CancelActiveAnimation();
        _activeState = transition.CurrentState;

        if (!_animationPreferences.AnimationsEnabled)
        {
            ApplyFinalState(transition.CurrentState);
            return;
        }

        _animationCancellation = new CancellationTokenSource();
        _ = RunTransitionAsync(transition, sequence, _animationCancellation.Token);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _animationPreferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        CancelActiveAnimation();
        _boundsAnimator.Dispose();
    }

    private async Task RunTransitionAsync(
        IslandTransition transition,
        long sequence,
        CancellationToken cancellationToken)
    {
        var duration = IslandAnimationProfile.DurationFor(transition, _options);
        var incoming = _views[transition.CurrentState];

        try
        {
            var outgoing = _views[transition.PreviousState];
            PrepareViews(incoming, outgoing);
            var from = new IslandVisualMetrics(
                _layoutRoot.Width,
                _layoutRoot.Height,
                _surface.CornerRadius.TopLeft);
            var to = IslandAnimationProfile.ForState(transition.CurrentState, _layoutOptions);

            await Task.WhenAll(
                _boundsAnimator.AnimateAsync(from, to, duration, _applyMetrics, cancellationToken),
                _compositionAnimations.AnimateTransitionAsync(
                    incoming,
                    outgoing,
                    duration,
                    _options.AnimationKind,
                    cancellationToken));

            if (sequence == _sequence && !cancellationToken.IsCancellationRequested)
            {
                ApplyFinalState(transition.CurrentState);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!_disposed && sequence == _sequence)
            {
                ApplyFinalState(transition.CurrentState);
            }
        }
        finally
        {
            if (sequence == _sequence)
            {
                _animationCancellation?.Dispose();
                _animationCancellation = null;
            }
        }
    }

    private void PrepareViews(FrameworkElement incoming, FrameworkElement outgoing)
    {
        foreach (var view in _views.Values)
        {
            var visual = ElementCompositionPreview.GetElementVisual(view);
            CompositionAnimationFactory.Reset(visual);
            view.Visibility = view == incoming || view == outgoing
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void ApplyFinalState(IslandVisualState state)
    {
        _applyMetrics(IslandAnimationProfile.ForState(state, _layoutOptions));
        foreach (var pair in _views)
        {
            var visual = ElementCompositionPreview.GetElementVisual(pair.Value);
            CompositionAnimationFactory.Reset(visual);
            pair.Value.Visibility = pair.Key == state ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void CancelActiveAnimation()
    {
        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();
        _animationCancellation = null;
        _boundsAnimator.Cancel();

        foreach (var view in _views.Values)
        {
            CompositionAnimationFactory.Stop(ElementCompositionPreview.GetElementVisual(view));
        }
    }

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args)
    {
        _layoutRoot.DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _animationPreferences.AnimationsEnabled)
            {
                return;
            }

            ++_sequence;
            CancelActiveAnimation();
            ApplyFinalState(_activeState);
        });
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
