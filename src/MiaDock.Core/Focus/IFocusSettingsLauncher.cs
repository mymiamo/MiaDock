namespace MiaDock.Core.Focus;

public interface IFocusSettingsLauncher
{
    Task<bool> OpenWindowsFocusSettingsAsync(
        CancellationToken cancellationToken = default);
}
