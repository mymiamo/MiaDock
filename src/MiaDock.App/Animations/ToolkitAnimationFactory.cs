using System.Numerics;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

/// <summary>
/// Dock content and shell motion via CommunityToolkit.WinUI.Animations.
/// Island bounds remain on <see cref="IslandBoundsAnimator"/>.
/// </summary>
public sealed class ToolkitAnimationFactory
{
    public Task AnimateTransitionAsync(
        FrameworkElement incoming,
        FrameworkElement? outgoing,
        TimeSpan duration,
        MotionPreset preset,
        IslandAnimationKind animationKind,
        double intensity,
        double springiness,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        cancellationToken.ThrowIfCancellationRequested();

        ElementCompositionPreview.SetIsTranslationEnabled(incoming, true);
        if (outgoing is not null)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(outgoing, true);
        }

        var incomingVisual = ElementCompositionPreview.GetElementVisual(incoming);
        var outgoingVisual = outgoing is null ? null : ElementCompositionPreview.GetElementVisual(outgoing);
        Stop(incomingVisual);
        if (outgoingVisual is not null && outgoingVisual != incomingVisual)
        {
            Stop(outgoingVisual);
        }

        if (duration <= TimeSpan.Zero || preset == MotionPreset.Off)
        {
            Reset(incomingVisual);
            if (outgoingVisual is not null && outgoingVisual != incomingVisual)
            {
                outgoingVisual.Opacity = 0;
            }

            return Task.CompletedTask;
        }

        incomingVisual.CenterPoint = CenterOf(incoming);

        var safeIntensity = Math.Clamp(intensity, 0, 1);
        var safeSpringiness = Math.Clamp(springiness, 0, 1);
        var recipe = ResolveRecipe(animationKind, preset, safeIntensity, safeSpringiness);
        var easing = ResolveEasing(preset, animationKind);

        var incomingBuilder = AnimationBuilder.Create()
            .Opacity(1, from: 0, duration: duration, easingType: easing.Type, easingMode: easing.Mode);

        if (recipe.UseScale)
        {
            incomingBuilder = incomingBuilder.Scale(
                1,
                from: recipe.IncomingScaleFrom,
                duration: duration,
                easingType: easing.Type,
                easingMode: easing.Mode);
        }

        if (recipe.UseSlide)
        {
            incomingBuilder = incomingBuilder.Translation(
                Axis.Y,
                0,
                from: recipe.IncomingOffsetY,
                duration: duration,
                easingType: easing.Type,
                easingMode: easing.Mode);
        }

        var incomingTask = incomingBuilder.StartAsync(incoming, cancellationToken);

        Task outgoingTask = Task.CompletedTask;
        if (outgoing is not null && outgoing != incoming)
        {
            var outgoingBuilder = AnimationBuilder.Create()
                .Opacity(0, duration: duration, easingType: easing.Type, easingMode: easing.Mode);

            if (recipe.UseSlide)
            {
                outgoingBuilder = outgoingBuilder.Translation(
                    Axis.Y,
                    -recipe.OutgoingOffsetY,
                    from: 0,
                    duration: duration,
                    easingType: easing.Type,
                    easingMode: easing.Mode);
            }

            if (recipe.UseScale)
            {
                outgoingBuilder = outgoingBuilder.Scale(
                    recipe.OutgoingScaleTo,
                    from: 1,
                    duration: duration,
                    easingType: easing.Type,
                    easingMode: easing.Mode);
            }

            outgoingTask = outgoingBuilder.StartAsync(outgoing, cancellationToken);
        }

        return FinishTransitionAsync(incoming, outgoing, incomingTask, outgoingTask, cancellationToken);
    }

    public Task AnimateShellScaleAsync(
        FrameworkElement shell,
        TimeSpan duration,
        MotionPreset preset,
        IslandAnimationKind animationKind,
        double intensity,
        double springiness,
        bool expanding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);
        cancellationToken.ThrowIfCancellationRequested();

        var visual = ElementCompositionPreview.GetElementVisual(shell);
        Stop(visual);

        if (duration <= TimeSpan.Zero ||
            preset == MotionPreset.Off ||
            animationKind == IslandAnimationKind.SlideFade)
        {
            Reset(visual);
            return Task.CompletedTask;
        }

        return RunShellAsync();

        async Task RunShellAsync()
        {
            var safeIntensity = Math.Clamp(intensity, 0, 1);
            var safeSpringiness = Math.Clamp(springiness, 0, 1);
            var scaleDelta = animationKind switch
            {
                IslandAnimationKind.Spring => (0.045 + 0.04 * safeSpringiness) * safeIntensity,
                _ => (0.04 + 0.03 * safeSpringiness) * Math.Max(safeIntensity, 0.35)
            };
            var from = expanding ? 1 - scaleDelta : 1 + (scaleDelta * 0.55);
            var easing = ResolveEasing(preset, animationKind);
            visual.CenterPoint = CenterOf(shell);

            try
            {
                await AnimationBuilder.Create()
                    .Scale(1, from: from, duration: duration, easingType: easing.Type, easingMode: easing.Mode)
                    .StartAsync(shell, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                throw;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Reset(visual);
        }
    }

    public Task AnimateContentAsync(
        FrameworkElement element,
        TimeSpan duration,
        MotionPreset preset,
        IslandAnimationKind animationKind,
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

        return RunContentAsync();

        async Task RunContentAsync()
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var safeIntensity = Math.Clamp(intensity, 0, 1);
            var safeSpringiness = Math.Clamp(springiness, 0, 1);
            var recipe = ResolveRecipe(animationKind, preset, safeIntensity, safeSpringiness);
            var directionSign = direction == MotionDirection.Previous ? -1.0 : 1.0;
            var distance = direction == MotionDirection.None || !recipe.UseSlide
                ? 0
                : (10 + 18 * safeIntensity) * directionSign;
            var opacityFrom = preset == MotionPreset.Minimal ? 0.72 : 0.18;
            var easing = ResolveEasing(preset, animationKind);
            visual.CenterPoint = CenterOf(element);

            try
            {
                var builder = AnimationBuilder.Create()
                    .Opacity(1, from: opacityFrom, duration: duration, easingType: easing.Type, easingMode: easing.Mode);

                if (recipe.UseScale || animationKind == IslandAnimationKind.ScaleFade)
                {
                    builder = builder.Scale(
                        1,
                        from: recipe.ContentScaleFrom,
                        duration: duration,
                        easingType: easing.Type,
                        easingMode: easing.Mode);
                }

                if (distance != 0)
                {
                    builder = builder.Translation(
                        Axis.X,
                        0,
                        from: distance,
                        duration: duration,
                        easingType: easing.Type,
                        easingMode: easing.Mode);
                }

                await builder.StartAsync(element, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                throw;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Reset(visual);
        }
    }

    public Task RefreshAsync(
        FrameworkElement element,
        TimeSpan duration,
        IslandAnimationKind animationKind,
        double intensity,
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

        return RunRefreshAsync();

        async Task RunRefreshAsync()
        {
            var safeIntensity = Math.Clamp(intensity, 0, 1);
            var useScale = animationKind is IslandAnimationKind.ScaleFade or IslandAnimationKind.Spring;
            var scaleFrom = 1 - ((0.02 + 0.02 * safeIntensity) * (useScale ? 1 : 0));

            try
            {
                var builder = AnimationBuilder.Create()
                    .Opacity(1, from: 0.55, duration: duration, easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut);

                if (useScale && scaleFrom < 1)
                {
                    visual.CenterPoint = CenterOf(element);
                    builder = builder.Scale(
                        1,
                        from: scaleFrom,
                        duration: duration,
                        easingType: EasingType.Cubic,
                        easingMode: EasingMode.EaseOut);
                }

                await builder.StartAsync(element, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                throw;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Reset(visual);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Reset(visual);
        }
    }

    public static void Stop(Visual visual)
    {
        EnsureTranslationProperty(visual);
        visual.StopAnimation(nameof(visual.Opacity));
        visual.StopAnimation(nameof(visual.Scale));
        visual.StopAnimation("Translation");
    }

    public static void Reset(Visual visual)
    {
        Stop(visual);
        visual.Opacity = 1;
        visual.Scale = Vector3.One;
        SetTranslation(visual, Vector3.Zero);
    }

    private static async Task FinishTransitionAsync(
        FrameworkElement incoming,
        FrameworkElement? outgoing,
        Task incomingTask,
        Task outgoingTask,
        CancellationToken cancellationToken)
    {
        var incomingVisual = ElementCompositionPreview.GetElementVisual(incoming);
        var outgoingVisual = outgoing is null ? null : ElementCompositionPreview.GetElementVisual(outgoing);

        try
        {
            await Task.WhenAll(incomingTask, outgoingTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Reset(incomingVisual);
            if (outgoingVisual is not null && outgoingVisual != incomingVisual)
            {
                Reset(outgoingVisual);
            }

            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Reset(incomingVisual);
            if (outgoingVisual is not null && outgoingVisual != incomingVisual)
            {
                Reset(outgoingVisual);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        Reset(incomingVisual);
        if (outgoingVisual is not null && outgoingVisual != incomingVisual)
        {
            outgoingVisual.Opacity = 0;
            SetTranslation(outgoingVisual, Vector3.Zero);
            outgoingVisual.Scale = Vector3.One;
        }
    }

    private static MotionRecipe ResolveRecipe(
        IslandAnimationKind kind,
        MotionPreset preset,
        double intensity,
        double springiness)
    {
        var baseScale = preset switch
        {
            MotionPreset.Minimal => 0.012,
            MotionPreset.Springy => 0.03 + 0.025 * springiness,
            MotionPreset.Dynamic => 0.04 + 0.03 * springiness,
            _ => 0.025 + 0.015 * springiness
        };

        return kind switch
        {
            IslandAnimationKind.SlideFade => new MotionRecipe(
                UseScale: false,
                UseSlide: true,
                IncomingScaleFrom: 1,
                OutgoingScaleTo: 1,
                ContentScaleFrom: 1,
                IncomingOffsetY: 8 * intensity,
                OutgoingOffsetY: 6 * intensity),
            IslandAnimationKind.Spring => new MotionRecipe(
                UseScale: true,
                UseSlide: intensity > 0.15,
                IncomingScaleFrom: 1 - ((baseScale + 0.02) * Math.Max(intensity, 0.4)),
                OutgoingScaleTo: 1 - (0.02 * intensity),
                ContentScaleFrom: 1 - ((0.02 + 0.025 * springiness) * Math.Max(intensity, 0.35)),
                IncomingOffsetY: 4 * intensity,
                OutgoingOffsetY: 3 * intensity),
            _ => new MotionRecipe(
                UseScale: true,
                UseSlide: false,
                IncomingScaleFrom: 1 - (Math.Max(baseScale, 0.035) * Math.Max(intensity, 0.45)),
                OutgoingScaleTo: 1 - (0.025 * Math.Max(intensity, 0.35)),
                ContentScaleFrom: 1 - ((0.03 + 0.02 * springiness) * Math.Max(intensity, 0.4)),
                IncomingOffsetY: 0,
                OutgoingOffsetY: 0)
        };
    }

    private static (EasingType Type, EasingMode Mode) ResolveEasing(
        MotionPreset preset,
        IslandAnimationKind kind)
    {
        if (kind == IslandAnimationKind.Spring || preset == MotionPreset.Springy)
        {
            return (EasingType.Back, EasingMode.EaseOut);
        }

        return preset switch
        {
            MotionPreset.Minimal => (EasingType.Cubic, EasingMode.EaseOut),
            MotionPreset.Fluid or MotionPreset.Dynamic => (EasingType.Circle, EasingMode.EaseOut),
            _ => (EasingType.Cubic, EasingMode.EaseOut)
        };
    }

    private static Vector3 CenterOf(FrameworkElement element) =>
        new(
            (float)(Math.Max(element.ActualWidth, 1) / 2),
            (float)(Math.Max(element.ActualHeight, 1) / 2),
            0);

    private static void EnsureTranslationProperty(Visual visual)
    {
        if (visual.Properties.TryGetVector3("Translation", out _) != CompositionGetValueStatus.Succeeded)
        {
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
        }
    }

    private static void SetTranslation(Visual visual, Vector3 value) =>
        visual.Properties.InsertVector3("Translation", value);

    private readonly record struct MotionRecipe(
        bool UseScale,
        bool UseSlide,
        double IncomingScaleFrom,
        double OutgoingScaleTo,
        double ContentScaleFrom,
        double IncomingOffsetY,
        double OutgoingOffsetY);
}
