using System.Text.Json;
using MiaDock.Core.Clipboard;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class ClipboardPeekSettingsAdapter : IClipboardPeekSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public ClipboardPeekSettingsAdapter(ISettingsService settings)
    {
        _settings = settings;
        Current = ReadCurrent();
        settings.SettingsChanged += OnSettingsChanged;
    }

    public ClipboardPeekOptions Current { get; private set; }
    public TimeSpan EventDuration => TimeSpan.FromSeconds(Math.Clamp(ReadEnvelope().EventDurationSeconds, 1, 10));
    public event EventHandler<ClipboardPeekOptions>? Changed;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var next = ReadCurrent();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    private ClipboardPeekOptions ReadCurrent()
    {
        var envelope = ReadEnvelope();
        var options = envelope.Options;
        var history = ReadInt(options, "historyLimit", 5);
        var mode = ReadString(options, "eventMode") switch
        {
            "everything" => ClipboardPeekEventMode.Everything,
            "never" => ClipboardPeekEventMode.Never,
            _ => ClipboardPeekEventMode.SmartOnly
        };
        return new ClipboardPeekOptions(NormalizeHistoryLimit(history), mode,
            ReadBool(options, "showImageEvents", true));
    }

    private ModuleSettingsEnvelope ReadEnvelope() =>
        _settings.Current.Modules.TryGetValue("clipboard-peek", out var value)
            ? value
            : ModuleSettingsEnvelope.ClipboardPeekDefault;

    internal static int NormalizeHistoryLimit(int value)
    {
        int[] allowed = [0, 5, 10, 20];
        return allowed.OrderBy(candidate => Math.Abs(candidate - value)).ThenByDescending(candidate => candidate).First();
    }

    private static int ReadInt(IReadOnlyDictionary<string, JsonElement>? options, string key, int fallback) =>
        options?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;
    private static string? ReadString(IReadOnlyDictionary<string, JsonElement>? options, string key) =>
        options?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool ReadBool(IReadOnlyDictionary<string, JsonElement>? options, string key, bool fallback) =>
        options?.TryGetValue(key, out var value) == true && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
