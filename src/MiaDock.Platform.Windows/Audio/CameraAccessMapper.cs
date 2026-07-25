using MiaDock.Modules.SystemStatus.Models;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace MiaDock.Platform.Windows.Audio;

public static class CameraAccessMapper
{
    public static CameraAccessState Map(AppCapabilityAccessStatus status) => status switch
    {
        AppCapabilityAccessStatus.Allowed => CameraAccessState.Allowed,
        AppCapabilityAccessStatus.DeniedByUser => CameraAccessState.DeniedByUser,
        AppCapabilityAccessStatus.DeniedBySystem => CameraAccessState.DeniedBySystem,
        AppCapabilityAccessStatus.UserPromptRequired => CameraAccessState.PromptRequired,
        AppCapabilityAccessStatus.NotDeclaredByApp => CameraAccessState.NotDeclared,
        _ => CameraAccessState.Unknown
    };
}
