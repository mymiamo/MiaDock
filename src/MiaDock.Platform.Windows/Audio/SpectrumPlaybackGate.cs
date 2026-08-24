using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

internal sealed class SpectrumPlaybackGate
{
    internal const int StopGraceMilliseconds = 1000;

    private bool _running;
    private long _stopAt;

    public bool Evaluate(
        bool featureEnabled,
        bool consumerActive,
        bool hasMedia,
        PlaybackStatus playbackStatus,
        bool outputSignalActive,
        long now)
    {
        if (!featureEnabled || !consumerActive)
        {
            Reset();
            return false;
        }

        if (hasMedia &&
            (playbackStatus == PlaybackStatus.Playing || outputSignalActive))
        {
            _running = true;
            _stopAt = 0;
            return true;
        }

        if (!_running)
        {
            return false;
        }

        if (_stopAt == 0)
        {
            _stopAt = now + StopGraceMilliseconds;
        }

        if (now < _stopAt)
        {
            return true;
        }

        Reset();
        return false;
    }

    private void Reset()
    {
        _running = false;
        _stopAt = 0;
    }
}
