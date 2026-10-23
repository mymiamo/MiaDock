namespace MiaDock.App;

// The fixture does not start MiaDock, hardware providers or user settings.
internal static class App
{
    public static IServiceProvider? Services { get; set; }
}
