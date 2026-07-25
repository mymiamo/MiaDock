using MiaDock.App.ViewModels;

namespace MiaDock.App.Services;

public sealed class OnboardingWindowService(
    OnboardingViewModel viewModel,
    ISettingsService settings,
    IApplicationLifetimeService lifetime,
    IAppLocalizationService localization) : IOnboardingWindowService
{
    private OnboardingWindow? _window;
    private TaskCompletionSource<bool>? _completion;

    public bool IsVisible => _window?.AppWindow.IsVisible == true;

    public async Task<bool> ShowAsync(CancellationToken cancellationToken = default)
    {
        if (settings.Current.Onboarding.IsCompleted)
        {
            return true;
        }

        if (_window is not null && _completion is not null)
        {
            _window.Activate();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        await viewModel.InitializeAsync(cancellationToken);
        _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _window = new OnboardingWindow(viewModel, localization);
        _window.Completed += OnCompleted;
        _window.Cancelled += OnCancelled;
        _window.Activate();
        return await _completion.Task.WaitAsync(cancellationToken);
    }

    public void Activate() => _window?.Activate();

    public void CloseForShutdown()
    {
        if (_window is null)
        {
            return;
        }

        DetachWindow();
        _window.AllowCloseAndClose();
        _window = null;
        _completion?.TrySetResult(false);
        _completion = null;
    }

    private void OnCompleted(object? sender, EventArgs args)
    {
        DetachWindow();
        _window = null;
        _completion?.TrySetResult(true);
        _completion = null;
    }

    private void OnCancelled(object? sender, EventArgs args)
    {
        DetachWindow();
        _window = null;
        _completion?.TrySetResult(false);
        _completion = null;
        lifetime.RequestExit();
    }

    private void DetachWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Completed -= OnCompleted;
        _window.Cancelled -= OnCancelled;
    }
}
