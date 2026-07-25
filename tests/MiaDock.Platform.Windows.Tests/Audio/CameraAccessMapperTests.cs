using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Platform.Windows.Audio;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class CameraAccessMapperTests
{
    [TestMethod]
    public void Map_PreservesPermissionAndPromptStates()
    {
        Assert.AreEqual(
            CameraAccessState.Allowed,
            CameraAccessMapper.Map(AppCapabilityAccessStatus.Allowed));
        Assert.AreEqual(
            CameraAccessState.DeniedByUser,
            CameraAccessMapper.Map(AppCapabilityAccessStatus.DeniedByUser));
        Assert.AreEqual(
            CameraAccessState.PromptRequired,
            CameraAccessMapper.Map(AppCapabilityAccessStatus.UserPromptRequired));
        Assert.AreEqual(
            CameraAccessState.NotDeclared,
            CameraAccessMapper.Map(AppCapabilityAccessStatus.NotDeclaredByApp));
    }
}
