using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class CommunicationActivityClassifierTests
{
    [TestMethod]
    public void ActiveMicrophoneAndCommunicationProcess_ReturnsPossible()
    {
        var result = CommunicationActivityClassifier.Classify(true, ["ms-teams", "audiodg"]);

        Assert.AreEqual(CallActivityState.Possible, result);
    }

    [TestMethod]
    public void CommunicationProcessWithoutMicrophone_ReturnsNone()
    {
        var result = CommunicationActivityClassifier.Classify(false, ["zoom"]);

        Assert.AreEqual(CallActivityState.None, result);
    }

    [TestMethod]
    public void ActiveMicrophoneWithUnrecognizedProcess_DoesNotClaimCall()
    {
        var result = CommunicationActivityClassifier.Classify(true, ["obs64", "chrome"]);

        Assert.AreEqual(CallActivityState.None, result);
    }
}
