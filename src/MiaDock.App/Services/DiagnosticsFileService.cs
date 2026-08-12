using Microsoft.UI.Xaml;
using MiaDock.Core.Lifecycle;
using MiaDock.Core.Logging;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace MiaDock.App.Services;

public sealed class DiagnosticsFileService(
    ILogService logService,
    ILogArchiveService archiveService) : IDiagnosticsFileService
{
    public async Task<bool> OpenLogFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(logService.LogDirectoryPath);
        var folder = await StorageFolder.GetFolderFromPathAsync(logService.LogDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        return await Launcher.LaunchFolderAsync(folder);
    }

    public Task<bool> PickAndExportAsync(
        Window owner,
        CancellationToken cancellationToken = default) =>
        PickAndExportAsync(
            owner,
            $"MiaDock-logs-{DateTime.Now:yyyyMMdd-HHmmss}",
            cancellationToken);

    public async Task<bool> PickAndExportAsync(
        Window owner,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add("ZIP arşivi", [".zip"]);
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker,
            WinRT.Interop.WindowNative.GetWindowHandle(owner));
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return false;
        }

        await archiveService.ExportAsync(file.Path, cancellationToken);
        return true;
    }

    public async Task<bool> PickAndExportCrashReportAsync(
        Window owner,
        CrashStateRecord crash,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(crash);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add("ZIP arşivi", [".zip"]);
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker,
            WinRT.Interop.WindowNative.GetWindowHandle(owner));
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return false;
        }

        await archiveService.ExportAsync(file.Path, cancellationToken);
        await AddCrashContextAsync(file.Path, crash, cancellationToken);
        return true;
    }

    private static async Task AddCrashContextAsync(
        string archivePath,
        CrashStateRecord crash,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        archive.GetEntry("crash-report.json")?.Delete();
        archive.GetEntry("CRASH-REPORT.txt")?.Delete();

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(4) ?? "unknown";
        var report = new
        {
            Format = "MiaDock crash report v1",
            Product = "MiaDock",
            AppVersion = version,
            Crash = new
            {
                crash.CrashedAtUtc,
                crash.ExceptionType,
                crash.ExceptionMessage,
                HResult = crash.ExceptionHResult is { } hresult ? $"0x{hresult:X8}" : null,
                crash.ExceptionSource,
                crash.ExceptionStackTrace,
                crash.RestartCount
            },
            Environment = new
            {
                OperatingSystem = RuntimeInformation.OSDescription,
                Runtime = RuntimeInformation.FrameworkDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Culture = System.Globalization.CultureInfo.CurrentCulture.Name,
                UICulture = System.Globalization.CultureInfo.CurrentUICulture.Name
            },
            BugReportUrl = "https://mymiamo.net/bug"
        };

        var jsonEntry = archive.CreateEntry("crash-report.json", CompressionLevel.Fastest);
        await using (var stream = jsonEntry.Open())
        {
            await JsonSerializer.SerializeAsync(stream, report, cancellationToken: cancellationToken);
        }

        var textEntry = archive.CreateEntry("CRASH-REPORT.txt", CompressionLevel.Fastest);
        await using var textStream = textEntry.Open();
        await using var writer = new StreamWriter(textStream, new UTF8Encoding(false));
        await writer.WriteAsync($"""
MiaDock crash report

Application version: {version}
Crash time (UTC): {crash.CrashedAtUtc:O}
Exception: {crash.ExceptionType}
Message: {crash.ExceptionMessage}
HResult: {(crash.ExceptionHResult is { } crashHResult ? $"0x{crashHResult:X8}" : "unknown")}
Source: {crash.ExceptionSource}
Automatic restart attempt: {crash.RestartCount}

Submit this ZIP at: https://mymiamo.net/bug
The report includes technical logs and the exception context. It excludes media titles,
notification bodies, usernames, and personal file paths.
""");
    }
}
