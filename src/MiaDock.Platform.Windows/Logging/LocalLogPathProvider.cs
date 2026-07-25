using MiaDock.Platform.Windows.Settings;

namespace MiaDock.Platform.Windows.Logging;

public sealed class LocalLogPathProvider(ISettingsPathProvider settingsPathProvider) : ILogPathProvider
{
    public string GetLogDirectoryPath()
    {
        var settingsDirectory = Path.GetDirectoryName(settingsPathProvider.GetSettingsFilePath())
            ?? throw new InvalidOperationException("The settings directory is unavailable.");
        return Path.Combine(settingsDirectory, "Logs");
    }
}
