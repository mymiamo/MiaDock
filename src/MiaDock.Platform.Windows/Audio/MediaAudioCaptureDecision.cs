using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

internal static class MediaAudioCaptureDecision
{
    internal static MediaAudioLevelSnapshot Select(
        MediaAudioLevelSnapshot processSpectrum,
        bool processIsHealthy,
        MediaAudioLevelSnapshot endpointSpectrum,
        bool endpointIsHealthy,
        MediaAudioLevelSnapshot sessionPeak)
    {
        if (processIsHealthy && processSpectrum.Source == MediaAudioLevelSource.ProcessSpectrum)
        {
            return processSpectrum;
        }

        if (endpointIsHealthy && endpointSpectrum.Source == MediaAudioLevelSource.EndpointSpectrum)
        {
            return endpointSpectrum;
        }

        return sessionPeak.IsAvailable ? sessionPeak : MediaAudioLevelSnapshot.Silent;
    }
}
