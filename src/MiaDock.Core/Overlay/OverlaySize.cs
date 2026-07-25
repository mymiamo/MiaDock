namespace MiaDock.Core.Overlay;

public readonly record struct OverlaySize(double Width, double Height)
{
    public bool IsValid =>
        double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
