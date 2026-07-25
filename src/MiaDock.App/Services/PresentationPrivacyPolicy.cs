using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class PresentationPrivacyPolicy
{
    public bool CanPresent(
        ModulePresentation? presentation,
        MiaDockSettings settings,
        bool isFullscreen,
        bool isSessionLocked)
    {
        if (presentation is null || !presentation.IsSensitive)
        {
            return true;
        }

        if (isSessionLocked && !settings.Privacy.ShowSensitiveContentWhenLocked)
        {
            return false;
        }

        return !isFullscreen || settings.Privacy.ShowSensitiveContentInFullscreen;
    }
}
