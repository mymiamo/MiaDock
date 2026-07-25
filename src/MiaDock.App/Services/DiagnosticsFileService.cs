using Microsoft.UI.Xaml;
using MiaDock.Core.Logging;
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

    public async Task<bool> PickAndExportAsync(
        Window owner,
        CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"MiaDock-logs-{DateTime.Now:yyyyMMdd-HHmmss}"
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
}
