using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.HotKeys;

namespace MiaDock.Platform.Windows.Tests.HotKeys;

[TestClass]
public sealed class WindowsGlobalHotKeyServiceTests
{
    [TestMethod]
    public void Apply_WhenDisabled_DoesNotCallNativeRegistration()
    {
        var registrationCalls = 0;
        using var service = CreateService((_, _, _, _) =>
        {
            registrationCalls++;
            return true;
        });

        var statuses = service.Apply(new GlobalHotKeySettings(
            false,
            GlobalHotKeySettings.RecommendedBindings));

        Assert.AreEqual(0, registrationCalls);
        Assert.IsTrue(statuses.Values.All(status => status == HotKeyRegistrationStatus.Disabled));
    }

    [TestMethod]
    public void Apply_InvalidGesture_IsReportedWithoutNativeRegistration()
    {
        var registrationCalls = 0;
        using var service = CreateService((_, _, _, _) =>
        {
            registrationCalls++;
            return true;
        });
        var settings = new GlobalHotKeySettings(true, new Dictionary<HotKeyAction, HotKeyGestureSetting>
        {
            [HotKeyAction.ToggleDock] = new(HotKeyModifiers.Control, 0x7B)
        });

        var statuses = service.Apply(settings);

        Assert.AreEqual(0, registrationCalls);
        Assert.AreEqual(HotKeyRegistrationStatus.Invalid, statuses[HotKeyAction.ToggleDock]);
    }

    [TestMethod]
    public void Apply_RegisterHotKeyFailure_ReturnsConflictWithoutThrowing()
    {
        using var service = CreateService((_, _, _, _) => false);
        var settings = SettingsWith(
            HotKeyAction.ToggleDock,
            GlobalHotKeySettings.RecommendedFor(HotKeyAction.ToggleDock));

        var statuses = service.Apply(settings);

        Assert.AreEqual(HotKeyRegistrationStatus.Conflict, statuses[HotKeyAction.ToggleDock]);
    }

    [TestMethod]
    public void Apply_ReplacesRegistrationsImmediately()
    {
        var registeredIds = new List<int>();
        var unregisteredIds = new List<int>();
        using var service = new WindowsGlobalHotKeyService(
            (_, id, _, _) =>
            {
                registeredIds.Add(id);
                return true;
            },
            (_, id) =>
            {
                unregisteredIds.Add(id);
                return true;
            });

        service.Apply(SettingsWith(
            HotKeyAction.ToggleDock,
            GlobalHotKeySettings.RecommendedFor(HotKeyAction.ToggleDock)));
        service.Apply(SettingsWith(
            HotKeyAction.NextModule,
            GlobalHotKeySettings.RecommendedFor(HotKeyAction.NextModule)));

        CollectionAssert.AreEqual(new[] { 0x5100, 0x5102 }, registeredIds);
        CollectionAssert.AreEqual(new[] { 0x5100 }, unregisteredIds);
        Assert.AreEqual(
            HotKeyRegistrationStatus.Registered,
            service.RegistrationStatuses[HotKeyAction.NextModule]);
        Assert.AreEqual(
            HotKeyRegistrationStatus.Disabled,
            service.RegistrationStatuses[HotKeyAction.ToggleDock]);
    }

    private static WindowsGlobalHotKeyService CreateService(
        Func<nint, int, uint, uint, bool> register) =>
        new(register, (_, _) => true);

    private static GlobalHotKeySettings SettingsWith(
        HotKeyAction action,
        HotKeyGestureSetting gesture) =>
        new(true, new Dictionary<HotKeyAction, HotKeyGestureSetting>
        {
            [action] = gesture
        });
}
