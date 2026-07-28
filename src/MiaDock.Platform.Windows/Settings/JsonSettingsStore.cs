using System.Text;
using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Platform.Windows.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _ioGate = new(1, 1);

    public JsonSettingsStore(ISettingsPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        SettingsFilePath = pathProvider.GetSettingsFilePath();
    }

    public string SettingsFilePath { get; }

    public async Task<MiaDockSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return MiaDockSettings.Default;
            }

            try
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<MiaDockSettings>(json, SerializerOptions);
                return SettingsValidator.Normalize(settings);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken).ConfigureAwait(false);
                    var recovered = RecoverSettings(json);
                    await WriteSettingsFileAsync(recovered, cancellationToken).ConfigureAwait(false);
                    return recovered;
                }
                catch (Exception recoveryException) when (recoveryException is JsonException or NotSupportedException)
                {
                    QuarantineCorruptFile();
                    return MiaDockSettings.Default;
                }
                catch (Exception recoveryException) when (
                    recoveryException is IOException or UnauthorizedAccessException)
                {
                    return MiaDockSettings.Default;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return MiaDockSettings.Default;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task SaveAsync(MiaDockSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = SettingsValidator.Normalize(settings);
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteSettingsFileAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async Task WriteSettingsFileAsync(
        MiaDockSettings settings,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath)
            ?? throw new InvalidOperationException("The settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{SettingsFilePath}.tmp";
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);

        if (File.Exists(SettingsFilePath))
        {
            try
            {
                File.Replace(temporaryPath, SettingsFilePath, null);
            }
            catch (IOException)
            {
                File.Move(temporaryPath, SettingsFilePath, overwrite: true);
            }
        }
        else
        {
            File.Move(temporaryPath, SettingsFilePath);
        }
    }

    private static MiaDockSettings RecoverSettings(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The settings root must be an object.");
        }

        var root = document.RootElement;
        var defaults = MiaDockSettings.Default;
        var recovered = defaults with
        {
            SchemaVersion = ReadValue(root, nameof(MiaDockSettings.SchemaVersion), defaults.SchemaVersion),
            General = ReadValue(root, nameof(MiaDockSettings.General), defaults.General),
            Appearance = ReadValue(root, nameof(MiaDockSettings.Appearance), defaults.Appearance),
            Media = ReadValue(root, nameof(MiaDockSettings.Media), defaults.Media),
            Fullscreen = ReadValue(root, nameof(MiaDockSettings.Fullscreen), defaults.Fullscreen),
            Monitor = ReadValue(root, nameof(MiaDockSettings.Monitor), defaults.Monitor),
            Tray = ReadValue(root, nameof(MiaDockSettings.Tray), defaults.Tray),
            StartupShutdown = ReadValue(root, nameof(MiaDockSettings.StartupShutdown), defaults.StartupShutdown),
            Onboarding = ReadValue(root, nameof(MiaDockSettings.Onboarding), defaults.Onboarding),
            HotKeys = ReadValue(root, nameof(MiaDockSettings.HotKeys), defaults.HotKeys),
            Privacy = ReadValue(root, nameof(MiaDockSettings.Privacy), defaults.Privacy),
            StoreUpdates = ReadValue(root, nameof(MiaDockSettings.StoreUpdates), defaults.StoreUpdates),
            Modules = RecoverModules(root, defaults.Modules)
        };
        return SettingsValidator.Normalize(recovered);
    }

    private static IReadOnlyDictionary<string, ModuleSettingsEnvelope> RecoverModules(
        JsonElement root,
        IReadOnlyDictionary<string, ModuleSettingsEnvelope> defaults)
    {
        if (!TryGetProperty(root, nameof(MiaDockSettings.Modules), out var modulesElement) ||
            modulesElement.ValueKind != JsonValueKind.Object)
        {
            return defaults;
        }

        var modules = new Dictionary<string, ModuleSettingsEnvelope>(StringComparer.Ordinal);
        foreach (var property in modulesElement.EnumerateObject())
        {
            try
            {
                var envelope = property.Value.Deserialize<ModuleSettingsEnvelope>(SerializerOptions);
                if (envelope is not null)
                {
                    modules[property.Name] = envelope;
                    continue;
                }
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
            }

            if (defaults.TryGetValue(property.Name, out var fallback))
            {
                modules[property.Name] = fallback;
            }
        }

        foreach (var pair in defaults)
        {
            modules.TryAdd(pair.Key, pair.Value);
        }
        return modules;
    }

    private static T ReadValue<T>(JsonElement root, string name, T fallback)
    {
        if (!TryGetProperty(root, name, out var element))
        {
            return fallback;
        }

        try
        {
            return element.Deserialize<T>(SerializerOptions) ?? fallback;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return fallback;
        }
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (directory is null || !File.Exists(SettingsFilePath))
            {
                return;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var corruptPath = Path.Combine(directory, $"settings.corrupt-{timestamp}.json");
            File.Move(SettingsFilePath, corruptPath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
