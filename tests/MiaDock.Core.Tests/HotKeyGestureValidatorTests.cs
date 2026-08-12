using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class HotKeyGestureValidatorTests
{
    [TestMethod]
    public void ValidCombination_RequiresSupportedModifier() =>
        Assert.IsTrue(HotKeyGestureValidator.IsValid(
            new HotKeyGestureSetting(HotKeyModifiers.Control | HotKeyModifiers.Shift, 0x4D)));

    [TestMethod]
    public void WindowsModifierAndF12_AreRejected()
    {
        Assert.IsFalse(HotKeyGestureValidator.IsValid(
            new HotKeyGestureSetting(HotKeyModifiers.Windows, 0x4D)));
        Assert.IsFalse(HotKeyGestureValidator.IsValid(
            new HotKeyGestureSetting(HotKeyModifiers.Control, 0x7B)));
    }

    [TestMethod]
    public void Normalize_RemovesDuplicateGesture()
    {
        var gesture = new HotKeyGestureSetting(HotKeyModifiers.Control, 0x4D);
        var settings = MiaDockSettings.Default with
        {
            HotKeys = new GlobalHotKeySettings(true, new Dictionary<HotKeyAction, HotKeyGestureSetting>
            {
                [HotKeyAction.ToggleDock] = gesture,
                [HotKeyAction.NextModule] = gesture
            })
        };

        var normalized = SettingsValidator.Normalize(settings);

        Assert.HasCount(1, normalized.HotKeys.Bindings);
    }

    [TestMethod]
    public void DuplicateCheck_IgnoresTheActionBeingEdited()
    {
        var gesture = new HotKeyGestureSetting(HotKeyModifiers.Control, 0x4D);
        var bindings = new Dictionary<HotKeyAction, HotKeyGestureSetting>
        {
            [HotKeyAction.ToggleDock] = gesture
        };

        Assert.IsFalse(HotKeyGestureValidator.IsDuplicate(
            bindings,
            HotKeyAction.ToggleDock,
            gesture));
        Assert.IsTrue(HotKeyGestureValidator.IsDuplicate(
            bindings,
            HotKeyAction.NextModule,
            gesture));
    }

    [TestMethod]
    public void RecommendedBindings_AreValidAndUnique()
    {
        var bindings = GlobalHotKeySettings.RecommendedBindings;

        Assert.HasCount(Enum.GetValues<HotKeyAction>().Length, bindings);
        Assert.IsTrue(bindings.Values.All(HotKeyGestureValidator.IsValid));
        Assert.AreEqual(bindings.Count, bindings.Values.Distinct().Count());
    }
}
