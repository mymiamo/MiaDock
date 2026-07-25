namespace MiaDock.App.Services;

public interface IOnboardingWindowService
{
    bool IsVisible { get; }

    Task<bool> ShowAsync(CancellationToken cancellationToken = default);

    void Activate();

    void CloseForShutdown();
}
