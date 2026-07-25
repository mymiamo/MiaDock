using Microsoft.UI.Xaml;

namespace MiaDock.App.Infrastructure;

internal static class WindowBranding
{
    internal static string IconPath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");

    internal static void ApplyIcon(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        try
        {
            if (File.Exists(IconPath))
            {
                window.AppWindow.SetIcon(IconPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The embedded executable icon remains the fallback.
        }
    }
}
