using MiaDock.Core.Modules;

namespace MiaDock.Core.Audio;

public interface IAudibleNotificationPlayer : IDisposable
{
    void Play(AudibleNotificationCue cue);

    void Preview(AudibleNotificationCue cue);

    void Stop();
}
