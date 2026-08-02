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
        MotionPreset preset,
        double intensity,
        double springiness,
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
        var safeIntensity = (float)Math.Clamp(intensity, 0, 1);
        var safeSpringiness = (float)Math.Clamp(springiness, 0, 1);
        var initialScale = preset switch
        {
            MotionPreset.Minimal => 1 - (0.008f * safeIntensity),
            MotionPreset.Springy => 1 - ((0.025f + 0.02f * safeSpringiness) * safeIntensity),
            MotionPreset.Dynamic => 1 - ((0.035f + 0.025f * safeSpringiness) * safeIntensity),
            _ => 1 - (0.02f * safeIntensity)
        };

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

    public Task AnimateContentAsync(
        FrameworkElement element,
        TimeSpan duration,
        MotionPreset preset,
        double intensity,
        double springiness,
        MotionDirection direction,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        Stop(visual);
        if (duration <= TimeSpan.Zero || preset == MotionPreset.Off)
        {
            Reset(visual);
            return Task.CompletedTask;
        }

        return RunAsync();

        async Task RunAsync()
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(
                preset is MotionPreset.Fluid or MotionPreset.Dynamic
                    ? new Vector2(0.16f, 1f)
                    : new Vector2(0.2f, 0.8f),
                preset is MotionPreset.Springy or MotionPreset.Dynamic
                    ? new Vector2(0.24f, 1f)
                    : new Vector2(0.3f, 1f));
            var directionSign = direction == MotionDirection.Previous ? -1f : 1f;
            var distance = direction == MotionDirection.None
                ? 0
                : (10f + 18f * (float)Math.Clamp(intensity, 0, 1)) * directionSign;
            var scaleOffset = (0.008f + 0.018f * (float)Math.Clamp(springiness, 0, 1)) *
                              (float)Math.Clamp(intensity, 0, 1);

            visual.Opacity = preset == MotionPreset.Minimal ? 0.72f : 0.18f;
            SetTranslation(visual, new Vector3(distance, 0, 0));
            visual.Scale = new Vector3(1 - scaleOffset, 1 - scaleOffset, 1);
            StartScalar(visual, nameof(visual.Opacity), visual.Opacity, 1, duration, easing);
            StartVector3(visual, "Translation", new Vector3(distance, 0, 0), Vector3.Zero, duration, easing);
            StartVector3(visual, nameof(visual.Scale), visual.Scale, Vector3.One, duration, easing);
            try
            {
                await Task.Delay(duration, cancellationToken);
                Reset(visual);
            }
            catch (OperationCanceledException)
            {
                Reset(visual);
                throw;
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
                Reset(visual);
                throw;
            }
        }
    }

    public static void Stop(Visual visual)
    {
        visual.StopAnimation(nameof(visual.Opacity));
        visual.StopAnimation(nameof(visual.Scale));
        try
        {
            visual.StopAnimation("Translation");
        }
        catch (ArgumentException)
        {
            // Translation is registered lazily only for content that uses it.
        }
    }

    public static void Reset(Visual visual)
    {
        Stop(visual);
        visual.Opacity = 1;
        visual.Scale = Vector3.One;
        SetTranslation(visual, Vector3.Zero);
    }

    private static void SetTranslation(Visual visual, Vector3 value) =>
        visual.Properties.InsertVector3("Translation", value);

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
