namespace MiaDock.Core.Focus;

public sealed class FocusPolicyService : IFocusPolicyService
{
    private readonly IFocusService _focus;
    private bool _disposed;

    public FocusPolicyService(IFocusService focus)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        Current = CreateSnapshot(focus.Current);
        _focus.FocusChanged += OnFocusChanged;
    }

    public FocusPolicySnapshot Current { get; private set; }

    public event EventHandler? PolicyChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _focus.FocusChanged -= OnFocusChanged;
    }

    private void OnFocusChanged(object? sender, FocusChangedEventArgs args)
    {
        var next = CreateSnapshot(args.Current);
        if (Equivalent(next, Current))
        {
            return;
        }

        Current = next;
        PolicyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FocusPolicySnapshot CreateSnapshot(FocusSnapshot snapshot)
    {
        if (!snapshot.IsActive || snapshot.ActiveProfile is not { } profile)
        {
            return FocusPolicySnapshot.Inactive;
        }

        var behavior = profile.Behavior;
        return new FocusPolicySnapshot(
            true,
            profile.Id,
            behavior.DockVisibility,
            new HashSet<string>(
                behavior.AllowedModuleIds ?? Array.Empty<string>(),
                StringComparer.Ordinal),
            behavior.MinimumEventPriority,
            behavior.AllowFullscreenNotifications,
            behavior.AllowSensitiveContentInFullscreen,
            behavior.AllowSensitiveContentWhenLocked);
    }

    private static bool Equivalent(
        FocusPolicySnapshot left,
        FocusPolicySnapshot right) =>
        left.IsActive == right.IsActive &&
        string.Equals(left.ProfileId, right.ProfileId, StringComparison.Ordinal) &&
        left.DockVisibility == right.DockVisibility &&
        left.MinimumEventPriority == right.MinimumEventPriority &&
        left.AllowFullscreenNotifications == right.AllowFullscreenNotifications &&
        left.AllowSensitiveContentInFullscreen == right.AllowSensitiveContentInFullscreen &&
        left.AllowSensitiveContentWhenLocked == right.AllowSensitiveContentWhenLocked &&
        left.AllowedModuleIds.SetEquals(right.AllowedModuleIds);
}
