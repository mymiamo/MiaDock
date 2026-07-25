using Windows.UI.ViewManagement;

namespace MiaDock.App.Services;

public sealed class WindowsAnimationPreferenceService : IAnimationPreferenceService
{
    private readonly UISettings _settings = new();
    private bool _disposed;

    public WindowsAnimationPreferenceService()
    {
        _settings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
    }

    public bool AnimationsEnabled => _settings.AnimationsEnabled;

    public event EventHandler? AnimationsEnabledChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
    }

    private void OnAnimationsEnabledChanged(UISettings sender, object args) =>
        AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty);
}
