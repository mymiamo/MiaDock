namespace MiaDock.Modules.SystemStatus.Models;

public enum SystemActivityServiceState
{
    NotInitialized,
    Initializing,
    Ready,
    PartiallyAvailable,
    Unavailable,
    Faulted
}
