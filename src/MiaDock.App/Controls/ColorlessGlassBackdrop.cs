using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MiaDock.App.Controls;

/// <summary>
/// Element scoped colorless Acrylic. Hosting the controller on the backdrop
/// element instead of the window keeps the HWND rectangle unpainted, so the
/// dock silhouette stays anti-aliased at the corner radius from settings.
/// </summary>
internal sealed partial class ColorlessGlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;

    public static bool IsSupported => DesktopAcrylicController.IsSupported();

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        try
        {
            // The dock intentionally never takes activation. A permanently
            // active configuration prevents Acrylic from swapping the live
            // desktop sample for its opaque inactive fallback.
            _configuration ??= new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = SystemBackdropTheme.Dark
            };
            _controller ??= new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Thin,
                FallbackColor = Color.FromArgb(0, 0, 0, 0),
                TintColor = Color.FromArgb(255, 128, 128, 128),
                TintOpacity = 0.02f,
                LuminosityOpacity = 0.10f
            };
            _controller.SetSystemBackdropConfiguration(_configuration);
            _controller.AddSystemBackdropTarget(connectedTarget);
        }
        catch (Exception)
        {
            // Without Acrylic the surface overlay alone remains visible, which
            // is preferable to tearing down the dock.
            ReleaseController();
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        try
        {
            _controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        }
        catch (Exception)
        {
        }

        ReleaseController();
    }

    private void ReleaseController()
    {
        _controller?.Dispose();
        _controller = null;
        _configuration = null;
    }
}
