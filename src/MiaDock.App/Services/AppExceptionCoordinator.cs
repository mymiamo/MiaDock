using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.Windows.AppLifecycle;
using MiaDock.Core.Lifecycle;
using MiaDock.Core.Logging;
using MiaDock.Platform.Windows.Lifecycle;
using System.Runtime.InteropServices;

namespace MiaDock.App.Services;

public sealed class AppExceptionCoordinator(
    ILogService logService,
    ICrashStateStore crashStateStore,
    ISingleInstanceService singleInstanceService) : IDisposable
{
    private Application? _application;
    private bool _disposed;
    private bool _restartRequested;

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

    private void OnApplicationUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        // Optional views and Windows device objects can disappear during a click.
        // Keep the shell alive for these recoverable UI failures only.
        var recoverable = args.Exception is XamlParseException or
            COMException or
            InvalidComObjectException or
            ObjectDisposedException;
        if (recoverable)
        {
            logService.Write(
                TechnicalLogLevel.Critical,
                TechnicalEventIds.ApplicationUnhandled,
                "Application",
                "An unhandled WinUI exception occurred.",
                args.Exception);
            FlushLogOnly();
            args.Handled = true;
            return;
        }

        logService.Write(
            TechnicalLogLevel.Critical,
            TechnicalEventIds.ApplicationUnhandled,
            "Application",
            "An unhandled WinUI exception occurred.",
            args.Exception);
        FlushCriticalCheckpoint(args.Exception);
        args.Handled = false;
        TryRestartAfterCrash(args.Exception);
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception;
        logService.Write(
            TechnicalLogLevel.Critical,
            TechnicalEventIds.AppDomainUnhandled,
            "Runtime",
            "An unhandled runtime exception occurred.",
            exception,
            new Dictionary<string, object?> { ["state"] = args.IsTerminating ? "terminating" : "continuing" });
        FlushCriticalCheckpoint(exception);
        if (args.IsTerminating)
        {
            TryRestartAfterCrash(exception);
        }
    }

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

    private void FlushLogOnly()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            logService.FlushAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception flushException) when (
            flushException is OperationCanceledException or IOException or UnauthorizedAccessException)
        {
        }
    }

    private void FlushCriticalCheckpoint(Exception? exception)
    {
        try
        {
            crashStateStore.MarkCrashed(exception);
        }
        catch
        {
            // The exception path must never throw while preserving its marker.
        }

        FlushLogOnly();
    }

    public void RequestRestartAfterStartupFailure(Exception exception)
    {
        FlushCriticalCheckpoint(exception);
        TryRestartAfterCrash(exception);
    }

    private void TryRestartAfterCrash(Exception? exception)
    {
        if (_restartRequested || _disposed)
        {
            return;
        }

        try
        {
            if (!crashStateStore.TryBeginRestart())
            {
                logService.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.ApplicationRestartRequested,
                    "Application",
                    "Automatic restart suppressed to avoid a restart loop.",
                    exception,
                    new Dictionary<string, object?>
                    {
                        ["maxRestarts"] = JsonCrashStateStore.MaxRestartsInWindow,
                        ["windowSeconds"] = JsonCrashStateStore.RestartLoopWindow.TotalSeconds
                    });
                return;
            }

            _restartRequested = true;
            logService.Write(
                TechnicalLogLevel.Critical,
                TechnicalEventIds.ApplicationRestartRequested,
                "Application",
                "Requesting automatic application restart after a crash.",
                exception);

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                logService.FlushAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception flushException) when (
                flushException is OperationCanceledException or IOException or UnauthorizedAccessException)
            {
            }

            try
            {
                singleInstanceService.Dispose();
            }
            catch
            {
                // Restart must proceed even if the single-instance key cannot be released cleanly.
            }

            _ = AppInstance.Restart(string.Empty);
        }
        catch (Exception restartException)
        {
            try
            {
                logService.Write(
                    TechnicalLogLevel.Error,
                    TechnicalEventIds.ApplicationRestartRequested,
                    "Application",
                    "Automatic restart failed.",
                    restartException);
            }
            catch
            {
                // Last-resort path: swallow logging failures.
            }
        }
    }
}
