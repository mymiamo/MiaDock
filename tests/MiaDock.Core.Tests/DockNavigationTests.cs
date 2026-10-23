using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DockNavigationTests
{
    [TestMethod]
    public void OldSettingsGetQuickFavorites_AndEmptySelectionStaysEmpty()
    {
        CollectionAssert.AreEqual(new[] { "media", "volume", "timer" },
            SettingsValidator.Normalize(MiaDockSettings.Default).EffectiveNavigation.PinnedModuleIds.ToArray());
        Assert.AreEqual(0, SettingsValidator.Normalize(MiaDockSettings.Default with { Navigation = new([]) }).EffectiveNavigation.PinnedModuleIds.Count);
    }

    [TestMethod]
    public void PinMoveAndUnpin_PreserveOrderAndRemoveDuplicates()
    {
        var navigation = DockNavigationSettings.Normalize(new(["media", " media ", "", "volume", "timer"]));
        navigation = navigation.Move("timer", -1).Toggle("network").Toggle("media");
        CollectionAssert.AreEqual(new[] { "timer", "volume", "network" }, navigation.PinnedModuleIds.ToArray());
        CollectionAssert.AreEqual(navigation.PinnedModuleIds.ToArray(), navigation.Move("missing", -1).PinnedModuleIds.ToArray());
    }
}
