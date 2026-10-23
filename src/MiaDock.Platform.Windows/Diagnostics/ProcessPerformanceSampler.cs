using System.Diagnostics;
using MiaDock.Core.Diagnostics;

namespace MiaDock.Platform.Windows.Diagnostics;

public sealed class ProcessPerformanceSampler : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    public DateTime StartedAtUtc => _process.StartTime.ToUniversalTime();
    public ProcessCounters Read()
    {
        _process.Refresh();
        return new(_process.TotalProcessorTime, _process.WorkingSet64, _process.PrivateMemorySize64);
    }
    public void Dispose() => _process.Dispose();
}
