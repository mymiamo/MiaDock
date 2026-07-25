namespace MiaDock.Modules.Media.Models;

public enum MediaServiceState
{
    NotInitialized,
    Initializing,
    Ready,
    Unavailable,
    AccessDenied,
    Faulted
}
