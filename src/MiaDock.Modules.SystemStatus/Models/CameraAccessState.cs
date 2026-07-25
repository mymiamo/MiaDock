namespace MiaDock.Modules.SystemStatus.Models;

public enum CameraAccessState
{
    Unavailable,
    Allowed,
    DeniedByUser,
    DeniedBySystem,
    PromptRequired,
    NotDeclared,
    Unknown
}
