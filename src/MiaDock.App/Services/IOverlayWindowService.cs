namespace MiaDock.App.Services;

public interface IOverlayWindowService
{
    OverlayWindow Current { get; }

    bool IsDockVisible { get; }

    void ShowNoActivate();

    void ShowDock();

    void HideDock();

    void ToggleDock();

    void ToggleExpandedFromShortcut();

    void SelectNextModuleFromShortcut();

    void CloseForShutdown();
}
