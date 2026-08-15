using System.Reflection;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsAudioDeviceCatalogInteropTests
{
    [TestMethod]
    public void EndpointEnumeration_UsesTheDocumentedTypedCollectionContract()
    {
        var assembly = typeof(WindowsAudioDeviceCatalog).Assembly;
        var enumerator = assembly.GetType("MiaDock.Platform.Windows.Audio.IMMDeviceEnumerator", throwOnError: true)!;
        var collection = assembly.GetType("MiaDock.Platform.Windows.Audio.IMMDeviceCollection", throwOnError: true)!;
        var method = enumerator.GetMethod("EnumAudioEndpoints", BindingFlags.Instance | BindingFlags.Public)!;
        var output = method.GetParameters()[2].ParameterType;

        Assert.IsTrue(output.IsByRef);
        Assert.AreEqual(collection, output.GetElementType());
        Assert.AreEqual(new Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), collection.GUID);
    }
}
