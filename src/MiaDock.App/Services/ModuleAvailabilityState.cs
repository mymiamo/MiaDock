namespace MiaDock.App.Services;

public enum ModuleAvailabilityState
{
    Ready,
    Disabled,
    PermissionRequired,
    PermissionDenied,
    ApiUnavailable,
    NoCompatibleDevice,
    TemporaryError
}

public sealed record ModuleAvailability(
    ModuleAvailabilityState State,
    string? Detail = null);
