using MiaDock.Core.Settings;

namespace MiaDock.Core.Focus;

public static class FocusAccessPolicy
{
    public static bool RequiresTrayEscape(
        FocusSnapshot focus,
        IslandVisibilityMode globalVisibility)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (!focus.IsActive || focus.ActiveProfile is not { } profile)
        {
            return false;
        }

        return profile.Behavior.DockVisibility switch
        {
            FocusDockVisibility.AlwaysVisible => false,
            FocusDockVisibility.UseGlobalSetting =>
                globalVisibility == IslandVisibilityMode.EventsOnly,
            FocusDockVisibility.EventsOnly or FocusDockVisibility.Hidden => true,
            _ => true
        };
    }
}
