using Microsoft.UI.Xaml;
using MiaDock.Core.Lifecycle;

namespace MiaDock.App.Services;

public interface IDiagnosticsFileService
{
    Task<bool> OpenLogFolderAsync(CancellationToken cancellationToken = default);

    Task<bool> PickAndExportAsync(
        Window owner,
        CancellationToken cancellationToken = default);

    Task<bool> PickAndExportAsync(
        Window owner,
        string suggestedFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Prompts the user and writes a ZIP containing logs plus the consumed crash context.</summary>
    Task<bool> PickAndExportCrashReportAsync(
        Window owner,
        CrashStateRecord crash,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
