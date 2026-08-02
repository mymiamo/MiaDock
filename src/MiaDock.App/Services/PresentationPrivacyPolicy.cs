using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class PresentationPrivacyPolicy
{
    private readonly IFocusPolicyService? _focusPolicy;

    public PresentationPrivacyPolicy(IFocusPolicyService? focusPolicy = null)
    {
        _focusPolicy = focusPolicy;
    }

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

        var focus = _focusPolicy?.Current ?? FocusPolicySnapshot.Inactive;
        if (isSessionLocked &&
            (!settings.Privacy.ShowSensitiveContentWhenLocked ||
             !focus.AllowSensitiveContentWhenLocked))
        {
            return false;
        }

        return !isFullscreen ||
               (settings.Privacy.ShowSensitiveContentInFullscreen &&
                focus.AllowSensitiveContentInFullscreen);
    }
}
