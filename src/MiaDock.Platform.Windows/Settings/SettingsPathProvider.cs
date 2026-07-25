using Windows.Storage;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Settings;

public sealed class SettingsPathProvider : ISettingsPathProvider
{
    public string GetSettingsFilePath()
    {
        try
        {
            var packagePath = ApplicationData.Current.LocalFolder.Path;
            if (!string.IsNullOrWhiteSpace(packagePath))
            {
                return Path.Combine(packagePath, "settings.json");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MiaDock", "settings.json");
    }
}
