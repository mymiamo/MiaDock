using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class PortableCustomizationTests
{
    [TestMethod]
    public void RoundTrip_PreservesPersonalization_AndLeavesMachineSettingsAlone()
    {
        var source = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with { AccentColor = "#123456", HorizontalOffset = -120 },
            Navigation = new(["timer", "media"]),
            DisplayProfiles = [new("profile", "My display", "machine-display", IslandPositionSetting.BottomCenter, AppearanceSettings.Default)]
        };
        var destination = MiaDockSettings.Default with { Monitor = new(MonitorSelectionMode.Fixed, "destination-display") };
        var json = PortableCustomization.Export(source);
        Assert.DoesNotContain("machine-display", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveState", json, StringComparison.Ordinal);
        var result = PortableCustomization.Apply(destination, PortableCustomization.Read(json));
        Assert.AreEqual("#123456", result.Appearance.AccentColor);
        Assert.AreEqual(-120, result.Appearance.HorizontalOffset);
        Assert.AreEqual(destination.Monitor, result.Monitor);
        Assert.AreEqual(destination.Privacy, result.Privacy);
        Assert.AreEqual(destination.HotKeys, result.HotKeys);
        Assert.AreEqual("timer", result.EffectiveNavigation.PinnedModuleIds[0]);
        Assert.IsNull(result.DisplayProfiles![0].DisplayId);
        Assert.IsNull(result.Focus.ActiveState);
    }

    [TestMethod]
    public void InvalidOrNewerPackages_DoNotMutateCurrentSettings()
    {
        Assert.Throws<JsonException>(() => PortableCustomization.Read("{"));
        Assert.Throws<InvalidDataException>(() => PortableCustomization.Read("{\"Format\":\"miadock-personalization\",\"Version\":99}"));
        Assert.Throws<InvalidDataException>(() => PortableCustomization.Read(new string(' ', PortableCustomization.MaximumBytes + 1)));
        var json = PortableCustomization.Export(MiaDockSettings.Default);
        var invalidPackage = PortableCustomization.Read(json) with { FocusProfiles = [null!] };
        Assert.Throws<InvalidDataException>(() => PortableCustomization.Apply(MiaDockSettings.Default, invalidPackage));
        Assert.Throws<InvalidDataException>(() => PortableCustomization.Read(JsonSerializer.Serialize(invalidPackage)));
    }

    [TestMethod]
    public void MonitorRemovalFallsBack_AndDuplicateAssignmentsAreNormalized()
    {
        var saved = AppearanceSettings.Default with { CollapsedWidth = 400 };
        var settings = SettingsValidator.Normalize(MiaDockSettings.Default with
        {
            DisplayProfiles = [new("a", "First", "external", IslandPositionSetting.BottomCenter, saved),
                new("b", "Second", "external", IslandPositionSetting.TopCenter, saved)]
        });
        Assert.IsNull(settings.DisplayProfiles![1].DisplayId);
        Assert.AreEqual(IslandPositionSetting.BottomCenter, MonitorAppearanceProfile.Resolve(settings, "external").General.Position);
        Assert.AreEqual(settings.Appearance, MonitorAppearanceProfile.Resolve(settings, "primary").Appearance);
    }
}
