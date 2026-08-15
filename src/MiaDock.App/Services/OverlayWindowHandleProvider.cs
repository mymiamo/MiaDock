namespace MiaDock.App.Services;

public interface IOverlayWindowHandleProvider
{
    nint WindowHandle { get; }
}

public sealed class OverlayWindowHandleProvider : IOverlayWindowHandleProvider
{
    public nint WindowHandle { get; private set; }

    internal void SetWindowHandle(nint windowHandle) => WindowHandle = windowHandle;
}
