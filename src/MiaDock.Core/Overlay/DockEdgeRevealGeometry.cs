namespace MiaDock.Core.Overlay;

public static class DockEdgeRevealGeometry
{
    public static OverlayPlacement HideTowardAttachedEdge(
        OverlayPlacement placement,
        OverlayWorkArea displayBounds,
        OverlayPosition position,
        int visibleStripPixels)
    {
        var strip = Math.Clamp(visibleStripPixels, 1, Math.Min(placement.Width, placement.Height));
        return position switch
        {
            OverlayPosition.TopCenter or OverlayPosition.TopLeft or OverlayPosition.TopRight =>
                placement with { Y = displayBounds.Y - placement.Height + strip },
            OverlayPosition.BottomCenter or OverlayPosition.BottomLeft or OverlayPosition.BottomRight =>
                placement with { Y = displayBounds.Y + displayBounds.Height - strip },
            OverlayPosition.LeftCenter =>
                placement with { X = displayBounds.X - placement.Width + strip },
            OverlayPosition.RightCenter =>
                placement with { X = displayBounds.X + displayBounds.Width - strip },
            _ => placement
        };
    }

    public static bool IsPointerAtActivationEdge(
        int pointerX,
        int pointerY,
        OverlayWorkArea displayBounds,
        OverlayPlacement visiblePlacement,
        OverlayPosition position,
        int edgeThicknessPixels,
        int spanPaddingPixels)
    {
        var thickness = Math.Max(1, edgeThicknessPixels);
        var padding = Math.Max(0, spanPaddingPixels);
        var right = displayBounds.X + displayBounds.Width;
        var bottom = displayBounds.Y + displayBounds.Height;
        var withinHorizontalSpan = pointerX >= visiblePlacement.X - padding &&
                                   pointerX < visiblePlacement.X + visiblePlacement.Width + padding;
        var withinVerticalSpan = pointerY >= visiblePlacement.Y - padding &&
                                 pointerY < visiblePlacement.Y + visiblePlacement.Height + padding;

        return position switch
        {
            OverlayPosition.TopCenter or OverlayPosition.TopLeft or OverlayPosition.TopRight =>
                withinHorizontalSpan && pointerY >= displayBounds.Y && pointerY < displayBounds.Y + thickness,
            OverlayPosition.BottomCenter or OverlayPosition.BottomLeft or OverlayPosition.BottomRight =>
                withinHorizontalSpan && pointerY >= bottom - thickness && pointerY < bottom,
            OverlayPosition.LeftCenter =>
                withinVerticalSpan && pointerX >= displayBounds.X && pointerX < displayBounds.X + thickness,
            OverlayPosition.RightCenter =>
                withinVerticalSpan && pointerX >= right - thickness && pointerX < right,
            _ => false
        };
    }
}
