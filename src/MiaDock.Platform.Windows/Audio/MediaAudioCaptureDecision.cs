using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

internal static class MediaAudioCaptureDecision
{
    internal static MediaAudioLevelSnapshot Select(
        bool spectrumEnabled,
        MediaAudioLevelSnapshot systemSpectrum,
        bool spectrumIsFresh,
        MediaAudioLevelSnapshot sessionPeak)
    {
        if (spectrumEnabled &&
            spectrumIsFresh &&
            systemSpectrum.Source == MediaAudioLevelSource.SystemSpectrum)
        {
            return systemSpectrum;
        }

        return sessionPeak.IsAvailable ? sessionPeak : MediaAudioLevelSnapshot.Silent;
    }
}
