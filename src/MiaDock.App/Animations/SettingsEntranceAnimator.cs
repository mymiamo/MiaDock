using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Animations;

/// <summary>
/// Staggers the first-level settings surfaces after navigation without changing
/// their layout or intercepting input. Reduced-motion users get the final state.
/// </summary>
internal static class SettingsEntranceAnimator
{
    public static async Task RunAsync(FrameworkElement page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!new UISettings().AnimationsEnabled)
        {
            return;
        }

        var tasks = FindTargets(page)
            .Take(6)
            .Select((target, index) => AnimateTargetAsync(target, index, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private static async Task AnimateTargetAsync(
        FrameworkElement target,
        int index,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ElementCompositionPreview.SetIsTranslationEnabled(target, true);
        await Task.Delay(TimeSpan.FromMilliseconds(index * 26), cancellationToken);
        await AnimationBuilder.Create()
            .Opacity(1, from: 0.35, duration: TimeSpan.FromMilliseconds(180),
                easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
            .Translation(Axis.Y, 0, from: 8, duration: TimeSpan.FromMilliseconds(180),
                easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
            .StartAsync(target, cancellationToken);
    }

    private static IEnumerable<FrameworkElement> FindTargets(FrameworkElement page)
    {
        if (page is not UserControl { Content: ScrollViewer { Content: Panel panel } })
        {
            yield break;
        }

        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            yield return child;
        }
    }
}
