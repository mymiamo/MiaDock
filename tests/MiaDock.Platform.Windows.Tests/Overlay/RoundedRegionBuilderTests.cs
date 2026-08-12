using MiaDock.Core.Presentation;
using MiaDock.Platform.Windows.Overlay;

namespace MiaDock.Platform.Windows.Tests.Overlay;

[TestClass]
public sealed class RoundedRegionBuilderTests
{
    [TestMethod]
    public void Create_UniformRegion_DoesNotThrow()
    {
        var region = RoundedRegionBuilder.Create(292, 46, 0, DockCornerRadii.Uniform(23));
        Assert.AreNotEqual(nint.Zero, region);
        _ = MiaDock.Platform.Windows.Interop.NativeMethods.DeleteObject(region);
    }

    [TestMethod]
    public void Create_AsymmetricRegion_DoesNotThrow()
    {
        var region = RoundedRegionBuilder.Create(
            320,
            80,
            0,
            new DockCornerRadii(24, 8, 16, 4));
        Assert.AreNotEqual(nint.Zero, region);
        _ = MiaDock.Platform.Windows.Interop.NativeMethods.DeleteObject(region);
    }
}
