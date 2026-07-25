namespace MiaDock.App.Services;

public interface ISettingsWindowService
{
    bool IsVisible { get; }

    void Show();

    void Hide();

    void CloseForShutdown();
}
