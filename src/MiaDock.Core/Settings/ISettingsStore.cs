namespace MiaDock.Core.Settings;

public interface ISettingsStore
{
    string SettingsFilePath { get; }

    Task<MiaDockSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MiaDockSettings settings, CancellationToken cancellationToken = default);
}
