namespace MiaDock.Platform.Windows.Audio;

internal static class ProcessLoopbackHealth
{
    internal static bool ShouldReject(
        double sessionPeak,
        long elapsedMilliseconds,
        int packetCount,
        double maximumPcmSample,
        bool hasSpectralActivity = false) =>
        sessionPeak >= 0.004 &&
        elapsedMilliseconds >= 550 &&
        (packetCount == 0 ||
         maximumPcmSample < 0.00001 ||
         !hasSpectralActivity);

    internal static uint SelectCandidate(
        IEnumerable<(uint ProcessId, float SessionPeak)> candidates,
        IReadOnlyDictionary<uint, long> unhealthyUntil,
        long now) =>
        candidates
            .Where(candidate =>
                candidate.ProcessId != 0 &&
                (!unhealthyUntil.TryGetValue(candidate.ProcessId, out var retryAt) || retryAt <= now))
            .OrderByDescending(candidate => candidate.SessionPeak)
            .Select(candidate => candidate.ProcessId)
            .FirstOrDefault();
}
