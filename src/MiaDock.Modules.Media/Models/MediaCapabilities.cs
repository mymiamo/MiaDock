namespace MiaDock.Modules.Media.Models;

public sealed record MediaCapabilities(
    bool CanPlay,
    bool CanPause,
    bool CanSkipPrevious,
    bool CanSkipNext,
    bool CanSeek,
    bool CanChangeVolume);
