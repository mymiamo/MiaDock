using System.Text.Json;
using MiaDock.Core.Lifecycle;
using MiaDock.Platform.Windows.Settings;

namespace MiaDock.Platform.Windows.Lifecycle;

public sealed class JsonCrashStateStore(ISettingsPathProvider settingsPathProvider) : ICrashStateStore
{
    public static readonly TimeSpan RestartLoopWindow = TimeSpan.FromSeconds(60);
    public const int MaxRestartsInWindow = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _gate = new();
    private readonly string _path = Path.Combine(
        Path.GetDirectoryName(settingsPathProvider.GetSettingsFilePath())
            ?? throw new InvalidOperationException("Local data directory is unavailable."),
        "crash-state.json");

    public void MarkSessionStarted()
    {
        lock (_gate)
        {
            var state = LoadUnsafe() ?? new CrashStateRecord();
            if (state.LastRestartUtc is { } lastRestart &&
                DateTimeOffset.UtcNow - lastRestart > RestartLoopWindow)
            {
                state.RestartCount = 0;
            }

            state.SessionActive = true;
            SaveUnsafe(state);
        }
    }

    public void MarkCleanShutdown()
    {
        lock (_gate)
        {
            var state = LoadUnsafe() ?? new CrashStateRecord();
            state.SessionActive = false;
            state.PendingCrash = false;
            state.ExceptionType = null;
            state.ExceptionMessage = null;
            state.CrashedAtUtc = null;
            SaveUnsafe(state);
        }
    }

    public void MarkCrashed(Exception? exception)
    {
        lock (_gate)
        {
            var state = LoadUnsafe() ?? new CrashStateRecord();
            state.PendingCrash = true;
            state.SessionActive = true;
            state.CrashedAtUtc = DateTimeOffset.UtcNow;
            state.ExceptionType = exception?.GetType().FullName;
            state.ExceptionMessage = Truncate(exception?.Message, 512);
            SaveUnsafe(state);
        }
    }

    public bool TryBeginRestart()
    {
        lock (_gate)
        {
            var state = LoadUnsafe() ?? new CrashStateRecord();
            var now = DateTimeOffset.UtcNow;
            if (state.LastRestartUtc is { } lastRestart &&
                now - lastRestart <= RestartLoopWindow &&
                state.RestartCount >= MaxRestartsInWindow)
            {
                return false;
            }

            if (state.LastRestartUtc is null ||
                now - state.LastRestartUtc.Value > RestartLoopWindow)
            {
                state.RestartCount = 0;
            }

            state.RestartCount++;
            state.LastRestartUtc = now;
            state.PendingCrash = true;
            SaveUnsafe(state);
            return true;
        }
    }

    public bool TryConsumePendingCrash(out CrashStateRecord record)
    {
        lock (_gate)
        {
            var state = LoadUnsafe() ?? new CrashStateRecord();

            // A leftover SessionActive flag only means the previous process did not
            // reach MarkCleanShutdown (task kill, last-window close, etc.). That is
            // not enough evidence for the crash-report UI and was causing a launch
            // loop when the recovery host window closed the process.
            if (!state.PendingCrash)
            {
                if (state.SessionActive)
                {
                    state.SessionActive = false;
                    SaveUnsafe(state);
                }

                record = new CrashStateRecord();
                return false;
            }

            record = new CrashStateRecord
            {
                PendingCrash = true,
                SessionActive = state.SessionActive,
                CrashedAtUtc = state.CrashedAtUtc,
                ExceptionType = state.ExceptionType,
                ExceptionMessage = state.ExceptionMessage,
                RestartCount = state.RestartCount,
                LastRestartUtc = state.LastRestartUtc
            };

            state.PendingCrash = false;
            state.SessionActive = false;
            state.ExceptionType = null;
            state.ExceptionMessage = null;
            state.CrashedAtUtc = null;
            SaveUnsafe(state);
            return true;
        }
    }

    private CrashStateRecord? LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<CrashStateRecord>(json, SerializerOptions);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void SaveUnsafe(CrashStateRecord state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";
            var json = JsonSerializer.Serialize(state, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Crash and shutdown paths must never throw from the state store.
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
