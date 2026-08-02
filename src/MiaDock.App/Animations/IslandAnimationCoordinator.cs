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
    private IslandMotionOptions _options;
    private IslandLayoutOptions _layoutOptions;
    private readonly IslandBoundsAnimator _boundsAnimator = new();
    private readonly CompositionAnimationFactory _compositionAnimations = new();
    private CancellationTokenSource? _animationCancellation;
    private CancellationTokenSource? _contentCancellation;
    private FrameworkElement? _contentAnimationTarget;
    private IslandVisualState _activeState = IslandVisualState.Collapsed;
    private long _sequence;
    private long _contentSequence;
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

    public void UpdateOptions(IslandMotionOptions options, IslandLayoutOptions layoutOptions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(layoutOptions);
        options.Validate();
        _options = options;
        _layoutOptions = layoutOptions;
        ++_sequence;
        CancelActiveAnimation();
        ApplyFinalState(_activeState);
    }

    public void RequestLayoutTransition(IslandLayoutOptions layoutOptions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutOptions);
        _layoutOptions = layoutOptions;
        var sequence = ++_sequence;
        CancelActiveAnimation();

        if (_activeState != IslandVisualState.ExpandedModule ||
            !_animationPreferences.AnimationsEnabled ||
            _options.Preset == MotionPreset.Off)
        {
            ApplyFinalState(_activeState);
            return;
        }

        _animationCancellation = new CancellationTokenSource();
        _ = RunLayoutTransitionAsync(sequence, _animationCancellation.Token);
    }

    private async Task RunLayoutTransitionAsync(long sequence, CancellationToken cancellationToken)
    {
        try
        {
            var from = new IslandVisualMetrics(
                _layoutRoot.Width,
                _layoutRoot.Height,
                _surface.CornerRadius.TopLeft);
            var to = IslandAnimationProfile.ForState(_activeState, _layoutOptions);
            await _boundsAnimator.AnimateAsync(
                from,
                to,
                _options.ContentRefreshDuration,
                _applyMetrics,
                cancellationToken);

            if (sequence == _sequence && !cancellationToken.IsCancellationRequested)
            {
                ApplyFinalState(_activeState);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!_disposed && sequence == _sequence)
            {
                ApplyFinalState(_activeState);
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

    public void RequestContentTransition(FrameworkElement element, MotionDirection direction) =>
        StartContentAnimation(element, direction, refresh: false);

    public void RequestContentRefresh(FrameworkElement element) =>
        StartContentAnimation(element, MotionDirection.None, refresh: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _animationPreferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        CancelActiveAnimation();
        CancelContentAnimation();
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
                    _options.Preset,
                    _options.Intensity,
                    _options.Springiness,
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

    private void StartContentAnimation(
        FrameworkElement element,
        MotionDirection direction,
        bool refresh)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(element);
        CancelContentAnimation();
        if (!_animationPreferences.AnimationsEnabled || _options.Preset == MotionPreset.Off)
        {
            CompositionAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            return;
        }

        _contentCancellation = new CancellationTokenSource();
        _contentAnimationTarget = element;
        var token = _contentCancellation.Token;
        var sequence = ++_contentSequence;
        _ = RunContentAnimationAsync(element, direction, refresh, sequence, token);
    }

    private async Task RunContentAnimationAsync(
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
                await _compositionAnimations.RefreshAsync(
                    element,
                    _options.ContentRefreshDuration,
                    cancellationToken);
            }
            else
            {
                await _compositionAnimations.AnimateContentAsync(
                    element,
                    _options.ContentRefreshDuration,
                    _options.Preset,
                    _options.Intensity,
                    _options.Springiness,
                    direction,
                    _options.ContentDelay,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!_disposed)
            {
                CompositionAnimationFactory.Reset(ElementCompositionPreview.GetElementVisual(element));
            }
        }
        finally
        {
            if (sequence == _contentSequence)
            {
                _contentCancellation?.Dispose();
                _contentCancellation = null;
                _contentAnimationTarget = null;
            }
        }
    }

    private void CancelContentAnimation()
    {
        ++_contentSequence;
        _contentCancellation?.Cancel();
        _contentCancellation?.Dispose();
        _contentCancellation = null;
        if (_contentAnimationTarget is not null)
        {
            CompositionAnimationFactory.Reset(
                ElementCompositionPreview.GetElementVisual(_contentAnimationTarget));
            _contentAnimationTarget = null;
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
            CancelContentAnimation();
            ApplyFinalState(_activeState);
        });
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
