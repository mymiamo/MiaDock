using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public sealed class CompositionAnimationFactory
{
    public Task AnimateTransitionAsync(
        FrameworkElement incoming,
        FrameworkElement? outgoing,
        TimeSpan duration,
        IslandAnimationKind animationKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        cancellationToken.ThrowIfCancellationRequested();

        var incomingVisual = ElementCompositionPreview.GetElementVisual(incoming);
        var outgoingVisual = outgoing is null ? null : ElementCompositionPreview.GetElementVisual(outgoing);
        Stop(incomingVisual);
        if (outgoingVisual is not null && outgoingVisual != incomingVisual)
        {
            Stop(outgoingVisual);
        }

        if (duration <= TimeSpan.Zero)
        {
            Reset(incomingVisual);
            if (outgoingVisual is not null && outgoingVisual != incomingVisual)
            {
                outgoingVisual.Opacity = 0;
            }

            return Task.CompletedTask;
        }

        var compositor = incomingVisual.Compositor;
        incomingVisual.CenterPoint = new Vector3(
            (float)(incoming.ActualWidth / 2),
            (float)(incoming.ActualHeight / 2),
            0);
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f),
            new Vector2(0.3f, 1f));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        var initialScale = animationKind == IslandAnimationKind.Spring ? 0.965f : 0.98f;

        incomingVisual.Opacity = 0;
        incomingVisual.Scale = new Vector3(initialScale, initialScale, 1);
        StartScalar(incomingVisual, nameof(incomingVisual.Opacity), 0, 1, duration, easing);
        StartVector3(
            incomingVisual,
            nameof(incomingVisual.Scale),
            new Vector3(initialScale, initialScale, 1),
            Vector3.One,
            duration,
            easing);

        if (outgoingVisual is not null && outgoingVisual != incomingVisual)
        {
            StartScalar(outgoingVisual, nameof(outgoingVisual.Opacity), outgoingVisual.Opacity, 0, duration, easing);
        }

        batch.End();
        batch.Completed += OnCompleted;
        var registration = cancellationToken.Register(() =>
        {
            Stop(incomingVisual);
            if (outgoingVisual is not null)
            {
                Stop(outgoingVisual);
            }

            completion.TrySetCanceled(cancellationToken);
        });

        return AwaitCompletionAsync();

        void OnCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            batch.Completed -= OnCompleted;
            Reset(incomingVisual);
            if (outgoingVisual is not null && outgoingVisual != incomingVisual)
            {
                outgoingVisual.Opacity = 0;
            }

            completion.TrySetResult();
        }

        async Task AwaitCompletionAsync()
        {
            try
            {
                await completion.Task;
            }
            finally
            {
                registration.Dispose();
                batch.Completed -= OnCompleted;
            }
        }
    }

    public Task RefreshAsync(
        FrameworkElement element,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        Stop(visual);

        if (duration <= TimeSpan.Zero)
        {
            Reset(visual);
            return Task.CompletedTask;
        }

        var compositor = visual.Compositor;
        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, 0.55f);
        animation.InsertKeyFrame(1, 1);
        visual.StartAnimation(nameof(visual.Opacity), animation);
        return WaitAndResetAsync();

        async Task WaitAndResetAsync()
        {
            try
            {
                await Task.Delay(duration, cancellationToken);
                Reset(visual);
            }
            catch (OperationCanceledException)
            {
                Stop(visual);
                throw;
            }
        }
    }

    public static void Stop(Visual visual)
    {
        visual.StopAnimation(nameof(visual.Opacity));
        visual.StopAnimation(nameof(visual.Scale));
    }

    public static void Reset(Visual visual)
    {
        Stop(visual);
        visual.Opacity = 1;
        visual.Scale = Vector3.One;
    }

    private static void StartScalar(
        Visual visual,
        string property,
        float from,
        float to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }

    private static void StartVector3(
        Visual visual,
        string property,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }
}
