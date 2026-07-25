using MiaDock.Core.Settings;

namespace MiaDock.Modules.Transfers.Settings;

public sealed record TransferModuleOptions(
    bool IsEnabled,
    TimeSpan EventDuration,
    bool ShowInFullscreen)
{
    public static TransferModuleOptions Default { get; } = new(true, TimeSpan.FromSeconds(5), false);

    public static TransferModuleOptions FromEnvelope(ModuleSettingsEnvelope? envelope) => envelope is null
        ? Default
        : new TransferModuleOptions(
            envelope.IsEnabled,
            TimeSpan.FromSeconds(Math.Clamp(envelope.EventDurationSeconds, 1, 60)),
            envelope.ShowInFullscreen);
}
