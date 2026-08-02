namespace MiaDock.Core.Focus;

public enum FocusChangeReason
{
    Initialized,
    Activated,
    Switched,
    Deactivated,
    Expired,
    SettingsChanged,
    ProfileRemoved
}
