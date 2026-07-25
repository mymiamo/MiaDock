namespace MiaDock.Core.Logging;

public interface ILogArchiveService
{
    Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default);
}
