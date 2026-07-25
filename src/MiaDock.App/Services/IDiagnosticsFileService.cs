using Microsoft.UI.Xaml;

namespace MiaDock.App.Services;

public interface IDiagnosticsFileService
{
    Task<bool> OpenLogFolderAsync(CancellationToken cancellationToken = default);

    Task<bool> PickAndExportAsync(Window owner, CancellationToken cancellationToken = default);
}
