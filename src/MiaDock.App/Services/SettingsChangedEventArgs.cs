using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class SettingsChangedEventArgs(MiaDockSettings previous, MiaDockSettings current) : EventArgs
{
    public MiaDockSettings Previous { get; } = previous;

    public MiaDockSettings Current { get; } = current;
}
