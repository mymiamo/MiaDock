namespace MiaDock.Core.Overlay;

public readonly record struct OverlayWorkArea(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}
