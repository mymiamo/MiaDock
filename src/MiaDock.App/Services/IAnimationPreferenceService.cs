namespace MiaDock.App.Services;

public interface IAnimationPreferenceService : IDisposable
{
    bool AnimationsEnabled { get; }

    event EventHandler? AnimationsEnabledChanged;
}
