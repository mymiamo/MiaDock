using Microsoft.UI.Xaml;
using MiaDock.Core.Logging;

namespace MiaDock.App.Services;

public sealed class AppExceptionCoordinator(ILogService logService) : IDisposable
{
    private Application? _application;
    private bool _disposed;

    public void Attach(Application application)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_application is not null)
        {
            return;
        }

        _application = application;
        _application.UnhandledException += OnApplicationUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_application is not null)
        {
            _application.UnhandledException -= OnApplicationUnhandledException;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _application = null;
        _disposed = true;
    }

    private void OnApplicationUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args) =>
        logService.Write(
            TechnicalLogLevel.Critical,
            TechnicalEventIds.ApplicationUnhandled,
            "Application",
            "An unhandled WinUI exception occurred.",
            args.Exception);

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args) =>
        logService.Write(
            TechnicalLogLevel.Critical,
            TechnicalEventIds.AppDomainUnhandled,
            "Runtime",
            "An unhandled runtime exception occurred.",
            args.ExceptionObject as Exception,
            new Dictionary<string, object?> { ["state"] = args.IsTerminating ? "terminating" : "continuing" });

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        logService.Write(
            TechnicalLogLevel.Error,
            TechnicalEventIds.UnobservedTask,
            "Runtime",
            "An unobserved background task failed.",
            args.Exception);
        args.SetObserved();
    }
}
