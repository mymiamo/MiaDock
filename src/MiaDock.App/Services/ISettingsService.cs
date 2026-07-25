using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public interface ISettingsService : IAsyncDisposable
{
    MiaDockSettings Current { get; }

    Exception? LastSaveFailure { get; }

    string SettingsFilePath { get; }

    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    void Update(Func<MiaDockSettings, MiaDockSettings> update);

    void Reset();

    Task FlushAsync(CancellationToken cancellationToken = default);
}
