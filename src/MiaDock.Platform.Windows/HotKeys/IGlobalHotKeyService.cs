using MiaDock.Core.Settings;

namespace MiaDock.Platform.Windows.HotKeys;

public interface IGlobalHotKeyService : IDisposable
{
    event EventHandler<HotKeyAction>? Invoked;
    event EventHandler? RegistrationsChanged;

    IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> RegistrationStatuses { get; }

    IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> Apply(GlobalHotKeySettings settings);
}
