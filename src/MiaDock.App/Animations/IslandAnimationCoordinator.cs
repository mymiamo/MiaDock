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
    private readonly IslandBoundsAnimator _boundsAnimator = new();
    private readonly ToolkitAnimationFactory _toolkitAnimations = new();
    private IslandMotionOptions _options;
    private IslandLayoutOptions _layoutOptions;
    private CancellationTokenSource? _transitionCancellation;
    private FrameworkElement? _animatedContentTarget;
    private IslandVisualState _activeState = IslandVisualState.Collapsed;
    private long _transitionSequence;
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

    public bool IsAnimating => _boundsAnimator.IsRunning || _transitionCancellation is not null;

    public void ApplyInitialState(IslandVisualState state)
    {
        ThrowIfDisposed();
        InvalidateActiveTransition();
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

        var sequence = InvalidateActiveTransition();
        _activeState = transition.CurrentState;
        if (!ShouldAnimate)
        {
            ApplyFinalState(transition.CurrentState);
            return;
        }

        var cancellationToken = StartTransitionSession();
        _ = RunStateTransitionAsync(transition, sequence, cancellationToken);
    }

    public void UpdateOptions(IslandMotionOptions options, IslandLayoutOptions layoutOptions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(layoutOptions);
        options.Validate();
        _options = options;
        _layoutOptions = layoutOptions;
        InvalidateActiveTransition();
        ApplyFinalState(_activeState);
    }

    public void RequestLayoutTransition(IslandLayoutOptions layoutOptions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutOptions);
        _layoutOptions = layoutOptions;
        var sequence = InvalidateActiveTransition();
        NormalizeViews(_activeState);

        if (_activeState != IslandVisualState.ExpandedModule || !ShouldAnimate)
        {
            ApplyFinalState(_activeState);
            return;
        }

        var cancellationToken = StartTransitionSession();
        _ = RunLayoutTransitionAsync(sequence, cancellationToken);
    }

    public void RequestContentTransition(FrameworkElement element, MotionDirection direction) =>
        StartContentTransition(element, direction, refresh: false);

    public void RequestModuleTransition(
        FrameworkElement element,
        MotionDirection direction,
        IslandLayoutOptions layoutOptions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(layoutOptions);
        _layoutOptions = layoutOptions;
        var sequence = InvalidateActiveTransition();
        NormalizeViews(_activeState);

        if (_activeState != IslandVisualState.ExpandedModule || !ShouldAnimate)
        {
            ApplyFinalState(_activeState);
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            return;
        }

        _animatedContentTarget = element;
        var cancellationToken = StartTransitionSession();
        _ = RunModuleTransitionAsync(element, direction, sequence, cancellationToken);
    }

    public void RequestContentRefresh(FrameworkElement element) =>
        StartContentTransition(element, MotionDirection.None, refresh: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _animationPreferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        InvalidateActiveTransition();
        _boundsAnimator.Dispose();
    }

    private bool ShouldAnimate =>
        _animationPreferences.AnimationsEnabled && _options.Preset != MotionPreset.Off;

    private async Task RunStateTransitionAsync(
        IslandTransition transition,
        long sequence,
        CancellationToken cancellationToken)
    {
        var duration = IslandAnimationProfile.DurationFor(transition, _options);
        var isEventMorph = IslandAnimationProfile.IsEventMorph(transition);
        var boundsEasing = IslandAnimationProfile.BoundsEasingFor(transition, _options);

        try
        {
            var incoming = _views[transition.CurrentState];
            FrameworkElement? outgoing = null;
            if (transition.PreviousState != transition.CurrentState &&
                _views.TryGetValue(transition.PreviousState, out var previousView))
            {
                outgoing = previousView;
            }

            PrepareViews(incoming, outgoing);
            if (isEventMorph)
            {
                // Keep event content hidden until the shell morph has started,
                // then fade/slide it in after ContentDelay.
                ElementCompositionPreview.GetElementVisual(incoming).Opacity = 0;
            }

            var from = CurrentMetrics();
            var to = IslandAnimationProfile.ForState(transition.CurrentState, _layoutOptions);
            var expanding = to.Width * to.Height >= from.Width * from.Height;
            var boundsTask = _boundsAnimator.AnimateAsync(
                from,
                to,
                duration,
                _applyMetrics,
                boundsEasing,
                cancellationToken);

            var contentDelay = isEventMorph ? _options.ContentDelay : TimeSpan.Zero;
            var contentTask = RunDelayedContentTransitionAsync(
                incoming,
                outgoing,
                duration,
                contentDelay,
                cancellationToken);

            // Event morph relies on width/height/radius interpolation — not a
            // shell ScaleTransform — so skip the decorative shell pulse there.
            var shellTask = isEventMorph
                ? Task.CompletedTask
                : _toolkitAnimations.AnimateShellScaleAsync(
                    _surface,
                    duration,
                    _options.Preset,
                    _options.AnimationKind,
                    _options.Intensity,
                    _options.Springiness,
                    expanding,
                    cancellationToken);

            await Task.WhenAll(boundsTask, contentTask, shellTask);
            ApplyFinalStateIfCurrent(transition.CurrentState, sequence, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ApplyFinalStateIfCurrent(transition.CurrentState, sequence, cancellationToken);
        }
        finally
        {
            CompleteTransitionSession(sequence);
        }
    }

    private async Task RunDelayedContentTransitionAsync(
        FrameworkElement incoming,
        FrameworkElement? outgoing,
        TimeSpan duration,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        await _toolkitAnimations.AnimateTransitionAsync(
            incoming,
            outgoing,
            duration,
            _options.Preset,
            _options.AnimationKind,
            _options.Intensity,
            _options.Springiness,
            cancellationToken);
    }

    private async Task RunLayoutTransitionAsync(long sequence, CancellationToken cancellationToken)
    {
        try
        {
            var from = CurrentMetrics();
            var to = IslandAnimationProfile.ForState(_activeState, _layoutOptions);
            var expanding = to.Width * to.Height >= from.Width * from.Height;
            var boundsTask = _boundsAnimator.AnimateAsync(
                from,
                to,
                _options.ContentRefreshDuration,
                _applyMetrics,
                cancellationToken);
            var shellTask = _toolkitAnimations.AnimateShellScaleAsync(
                _surface,
                _options.ContentRefreshDuration,
                _options.Preset,
                _options.AnimationKind,
                _options.Intensity,
                _options.Springiness,
                expanding,
                cancellationToken);
            await Task.WhenAll(boundsTask, shellTask);
            ApplyFinalStateIfCurrent(_activeState, sequence, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ApplyFinalStateIfCurrent(_activeState, sequence, cancellationToken);
        }
        finally
        {
            CompleteTransitionSession(sequence);
        }
    }

    private async Task RunModuleTransitionAsync(
        FrameworkElement element,
        MotionDirection direction,
        long sequence,
        CancellationToken cancellationToken)
    {
        try
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            var from = CurrentMetrics();
            var to = IslandAnimationProfile.ForState(_activeState, _layoutOptions);
            var expanding = to.Width * to.Height >= from.Width * from.Height;
            var boundsTask = _boundsAnimator.AnimateAsync(
                from,
                to,
                _options.ContentRefreshDuration,
                _applyMetrics,
                cancellationToken);
            var shellTask = _toolkitAnimations.AnimateShellScaleAsync(
                _surface,
                _options.ContentRefreshDuration,
                _options.Preset,
                _options.AnimationKind,
                _options.Intensity,
                _options.Springiness,
                expanding,
                cancellationToken);
            await Task.WhenAll(boundsTask, shellTask);

            if (!IsCurrent(sequence, cancellationToken))
            {
                return;
            }

            await _toolkitAnimations.AnimateContentAsync(
                element,
                _options.ContentRefreshDuration,
                _options.Preset,
                _options.AnimationKind,
                _options.Intensity,
                _options.Springiness,
                direction,
                _options.ContentDelay,
                cancellationToken);

            if (IsCurrent(sequence, cancellationToken))
            {
                ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
                ApplyFinalState(_activeState);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (IsCurrent(sequence, cancellationToken))
            {
                ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
                ApplyFinalState(_activeState);
            }
        }
        finally
        {
            CompleteTransitionSession(sequence);
        }
    }

    private void StartContentTransition(
        FrameworkElement element,
        MotionDirection direction,
        bool refresh)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        var sequence = InvalidateActiveTransition();
        ApplyFinalState(_activeState);
        element.Visibility = Visibility.Visible;
        element.Opacity = 1;
        element.IsHitTestVisible = true;
        if (!ShouldAnimate)
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            element.InvalidateMeasure();
            element.InvalidateArrange();
            return;
        }

        _animatedContentTarget = element;
        var cancellationToken = StartTransitionSession();
        _ = RunContentTransitionAsync(element, direction, refresh, sequence, cancellationToken);
    }

    private async Task RunContentTransitionAsync(
        FrameworkElement element,
        MotionDirection direction,
        bool refresh,
        long sequence,
        CancellationToken cancellationToken)
    {
        try
        {
            if (refresh)
            {
                await _toolkitAnimations.RefreshAsync(
                    element,
                    _options.ContentRefreshDuration,
                    _options.AnimationKind,
                    _options.Intensity,
                    cancellationToken);
            }
            else
            {
                await _toolkitAnimations.AnimateContentAsync(
                    element,
                    _options.ContentRefreshDuration,
                    _options.Preset,
                    _options.AnimationKind,
                    _options.Intensity,
                    _options.Springiness,
                    direction,
                    _options.ContentDelay,
                    cancellationToken);
            }

            if (IsCurrent(sequence, cancellationToken))
            {
                ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (IsCurrent(sequence, cancellationToken))
            {
                ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            }
        }
        finally
        {
            CompleteTransitionSession(sequence);
        }
    }

    private IslandVisualMetrics CurrentMetrics() => new(
        _layoutRoot.Width,
        _layoutRoot.Height,
        new(
            _surface.CornerRadius.TopLeft,
            _surface.CornerRadius.TopRight,
            _surface.CornerRadius.BottomRight,
            _surface.CornerRadius.BottomLeft));

    private void PrepareViews(FrameworkElement incoming, FrameworkElement? outgoing)
    {
        foreach (var view in _views.Values)
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(view));
            view.Visibility = view == incoming || view == outgoing
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void NormalizeViews(IslandVisualState state)
    {
        foreach (var pair in _views)
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(pair.Value));
            pair.Value.Visibility = pair.Key == state ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyFinalState(IslandVisualState state)
    {
        _applyMetrics(IslandAnimationProfile.ForState(state, _layoutOptions));
        NormalizeViews(state);
    }

    private void ApplyFinalStateIfCurrent(
        IslandVisualState state,
        long sequence,
        CancellationToken cancellationToken)
    {
        if (IsCurrent(sequence, cancellationToken))
        {
            ApplyFinalState(state);
        }
    }

    private bool IsCurrent(long sequence, CancellationToken cancellationToken) =>
        !_disposed && sequence == _transitionSequence && !cancellationToken.IsCancellationRequested;

    private long InvalidateActiveTransition()
    {
        var sequence = ++_transitionSequence;
        _transitionCancellation?.Cancel();
        _transitionCancellation?.Dispose();
        _transitionCancellation = null;
        _boundsAnimator.Cancel();

        foreach (var view in _views.Values)
        {
            ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(view));
        }

        ToolkitAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(_surface));

        if (_animatedContentTarget is not null)
        {
            ToolkitAnimationFactory.Reset(
                ElementCompositionPreview.GetElementVisual(_animatedContentTarget));
            _animatedContentTarget = null;
        }

        return sequence;
    }

    private CancellationToken StartTransitionSession()
    {
        _transitionCancellation = new CancellationTokenSource();
        return _transitionCancellation.Token;
    }

    private void CompleteTransitionSession(long sequence)
    {
        if (sequence != _transitionSequence)
        {
            return;
        }

        _transitionCancellation?.Dispose();
        _transitionCancellation = null;
        _animatedContentTarget = null;
    }

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args)
    {
        _layoutRoot.DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _animationPreferences.AnimationsEnabled)
            {
                return;
            }

            InvalidateActiveTransition();
            ApplyFinalState(_activeState);
        });
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
