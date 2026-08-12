using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Applications;
using MiaDock.Core.Lifecycle;
using MiaDock.Core.Logging;

namespace MiaDock.App.Services;

public sealed class CrashRecoveryCoordinator(
    ICrashStateStore crashStateStore,
    IDiagnosticsFileService diagnosticsFileService,
    IAppLocalizationService localization,
    IExternalUriLauncher uriLauncher,
    ILogService logService)
{
    private static readonly Uri BugReportUri = new("https://mymiamo.net/bug");

    public async Task ShowIfNeededAsync(
        Window owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();
        if (!crashStateStore.TryConsumePendingCrash(out var record))
        {
            return;
        }

        logService.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.ApplicationCrashDetected,
            "Application",
            "A previous crash was detected.",
            properties: new Dictionary<string, object?>
            {
                ["exceptionType"] = record.ExceptionType,
                ["crashedAtUtc"] = record.CrashedAtUtc?.ToString("O"),
                ["restartCount"] = record.RestartCount
            });

        var xamlRoot = await WaitForXamlRootAsync(owner, cancellationToken);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var report = await ShowDetectedDialogAsync(xamlRoot);
                if (!report)
                {
                    break;
                }

                bool exported;
                try
                {
                    exported = await diagnosticsFileService.PickAndExportCrashReportAsync(
                        owner,
                        record,
                        $"MiaDock-crash-{DateTime.Now:yyyyMMdd-HHmmss}",
                        cancellationToken);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    logService.Write(
                        TechnicalLogLevel.Error,
                        TechnicalEventIds.LogExportFailed,
                        "Application",
                        "Crash report export failed.",
                        exception);
                    exported = false;
                }

                if (!exported)
                {
                    // Cancelled save: return to the detection dialog.
                    continue;
                }

                logService.Write(
                    TechnicalLogLevel.Information,
                    TechnicalEventIds.ApplicationCrashReportSaved,
                    "Application",
                    "Crash report archive was saved by the user.");

                if (await ShowRedirectDialogAsync(xamlRoot))
                {
                    var opened = await uriLauncher.LaunchAsync(BugReportUri, cancellationToken);
                    if (!opened)
                    {
                        await ShowBugLinkFailureAsync(xamlRoot);
                    }
                }

                break;
            }
        }
        finally
        {
            // Ensure a dismissed recovery flow cannot re-trigger on the next launch.
            crashStateStore.MarkCleanShutdown();
            crashStateStore.MarkSessionStarted();
        }
    }

    private async Task<bool> ShowDetectedDialogAsync(XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Text("Dialog.Crash.Detected.Title", "Çökme Tespit Edildi"),
            Content = Text(
                "Dialog.Crash.Detected.Description",
                "MiaDock beklenmedik şekilde kapandı. Çökme raporu oluşturup bildirebilir veya kapatıp çalışmaya devam edebilirsiniz."),
            PrimaryButtonText = Text("Dialog.Crash.Report", "Bildir"),
            CloseButtonText = Text("Common.Close", "Kapat"),
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowRedirectDialogAsync(XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Text("Dialog.Crash.Redirect.Title", "Hata bildirimi"),
            Content = Text(
                "Dialog.Crash.Redirect.Description",
                "Bildirmek için mymiamo.net/bug sayfasına yönlendirileceksiniz."),
            PrimaryButtonText = Text("Dialog.Crash.Continue", "Devam Et"),
            CloseButtonText = Text("Common.Close", "Kapat"),
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowBugLinkFailureAsync(XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Text("Dialog.Link.OpenFailed.Title", "Bağlantı açılamadı"),
            Content = Text(
                "Dialog.BugReport.OpenFailed.Description",
                "Hata bildirim sayfası açılamadı. mymiamo.net/bug adresini tarayıcınızda açabilirsiniz."),
            CloseButtonText = Text("Common.Close", "Kapat")
        };
        await dialog.ShowAsync();
    }

    private string Text(string key, string fallback)
    {
        var value = localization.Get(key);
        return value != key ? value : fallback;
    }

    private static async Task<XamlRoot> WaitForXamlRootAsync(
        Window owner,
        CancellationToken cancellationToken)
    {
        if (owner.Content is FrameworkElement { XamlRoot: { } existing })
        {
            return existing;
        }

        if (owner.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException("Crash recovery requires a window with XAML content.");
        }

        if (root.XamlRoot is not null)
        {
            return root.XamlRoot;
        }

        var completion = new TaskCompletionSource<XamlRoot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender, RoutedEventArgs args)
        {
            root.Loaded -= OnLoaded;
            if (root.XamlRoot is { } loaded)
            {
                completion.TrySetResult(loaded);
            }
            else
            {
                completion.TrySetException(
                    new InvalidOperationException("Crash recovery window loaded without a XamlRoot."));
            }
        }

        root.Loaded += OnLoaded;
        if (root.XamlRoot is { } alreadyLoaded)
        {
            root.Loaded -= OnLoaded;
            return alreadyLoaded;
        }

        owner.Activate();
        return await completion.Task.WaitAsync(cancellationToken);
    }
}
