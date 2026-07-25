namespace MiaDock.Platform.Windows.Tray;

public interface ITrayIconService : IDisposable
{
    bool IsVisible { get; }

    event EventHandler<int>? CommandInvoked;

    event EventHandler? PrimaryInvoked;

    void Initialize(string toolTip);

    void SetMenu(IReadOnlyList<TrayMenuItem> items);

    void SetVisible(bool visible);
}
