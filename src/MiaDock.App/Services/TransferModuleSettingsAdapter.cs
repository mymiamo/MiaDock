using MiaDock.Modules.Transfers.Settings;

namespace MiaDock.App.Services;

public sealed class TransferModuleSettingsAdapter : ITransferModuleSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public TransferModuleSettingsAdapter(ISettingsService settings)
    {
        _settings = settings;
        Current = ReadCurrent();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public TransferModuleOptions Current { get; private set; }
    public event EventHandler<TransferModuleOptions>? Changed;

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var next = ReadCurrent();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    private TransferModuleOptions ReadCurrent() => TransferModuleOptions.FromEnvelope(
        _settings.Current.Modules.TryGetValue("transfers", out var envelope) ? envelope : null);
}
