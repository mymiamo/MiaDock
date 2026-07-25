using System.Runtime.InteropServices;
using System.Text;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using MiaDock.Modules.Notifications.Models;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Core.Threading;

namespace MiaDock.Platform.Windows.Notifications;

public sealed class WindowsSystemNotificationService : ISystemNotificationService
{
    private const int AppModelErrorNoPackage = 15700;
    private const int MaximumRememberedNotificationIds = 4096;
    private const int MaximumPendingNotificationDispatches = 128;
    private readonly IUiDispatcher _dispatcher;
    private readonly object _gate = new();
    private readonly HashSet<uint> _knownNotificationIds = [];
    private readonly Dictionary<string, NotificationSourceInfo> _sources = new(StringComparer.Ordinal);
    private UserNotificationListener? _listener;
    private bool _subscribed;
    private bool _initialized;
    private bool _disposed;
    private int _pendingNotificationDispatches;

    public WindowsSystemNotificationService(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public NotificationAccessState AccessState { get; private set; } = NotificationAccessState.Uninitialized;
    public IReadOnlyList<NotificationSourceInfo> Sources
    {
        get
        {
            lock (_gate)
            {
                return _sources.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }
    }

    public event EventHandler<NotificationAccessState>? AccessStateChanged;
    public event EventHandler<IReadOnlyList<NotificationSourceInfo>>? SourcesChanged;
    public event EventHandler<SystemNotificationSnapshot>? NotificationReceived;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
        {
            SetAccessState(NotificationAccessState.Unsupported);
            _initialized = true;
            return;
        }

        if (!HasPackageIdentity())
        {
            SetAccessState(NotificationAccessState.PackageIdentityRequired);
            _initialized = true;
            return;
        }

        try
        {
            _listener = UserNotificationListener.Current;
            var status = Map(_listener.GetAccessStatus());
            SetAccessState(status);
            if (status == NotificationAccessState.Allowed)
            {
                await BeginListeningAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            SetAccessState(NotificationAccessState.Faulted);
        }

        _initialized = true;
    }

    public async Task<NotificationAccessState> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) await InitializeAsync(cancellationToken);
        if (_listener is null) return AccessState;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var status = Map(await _listener.RequestAccessAsync());
            cancellationToken.ThrowIfCancellationRequested();
            SetAccessState(status);
            if (status == NotificationAccessState.Allowed)
            {
                await BeginListeningAsync(cancellationToken).ConfigureAwait(false);
            }
            return status;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            SetAccessState(NotificationAccessState.Faulted);
            return AccessState;
        }
    }

    private async Task BeginListeningAsync(CancellationToken cancellationToken)
    {
        if (_listener is null) return;
        if (!_subscribed)
        {
            _listener.NotificationChanged += OnNotificationChanged;
            _subscribed = true;
        }

        var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var notification in notifications)
        {
            RememberNotification(notification.Id);
            AddSource(notification);
        }
        PublishSources();
    }

    private void OnNotificationChanged(
        UserNotificationListener sender,
        UserNotificationChangedEventArgs args)
    {
        try
        {
            if (sender.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            {
                SetAccessState(Map(sender.GetAccessStatus()));
                return;
            }

            if (args.ChangeKind == UserNotificationChangedKind.Removed)
            {
                lock (_gate) _knownNotificationIds.Remove(args.UserNotificationId);
                return;
            }

            var notification = sender.GetNotification(args.UserNotificationId);
            if (notification is null) return;
            if (!RememberNotification(notification.Id)) return;

            var snapshot = Parse(notification);
            if (snapshot is null) return;
            if (AddSource(notification))
            {
                PublishSources();
            }
            PublishNotification(snapshot);
        }
        catch
        {
            // Notification content is intentionally never written to technical logs.
        }
    }

    private static SystemNotificationSnapshot? Parse(UserNotification notification)
    {
        var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
        if (binding is null) return null;
        var text = binding.GetTextElements().Select(item => item.Text?.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        var sourceId = notification.AppInfo.AppUserModelId ?? string.Empty;
        var sourceName = notification.AppInfo.DisplayInfo.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(sourceName)) return null;
        return new SystemNotificationSnapshot(
            notification.Id,
            sourceId,
            sourceName,
            text.FirstOrDefault() ?? "Yeni bildirim",
            string.Join(Environment.NewLine, text.Skip(1)),
            notification.CreationTime.ToUniversalTime());
    }

    private bool AddSource(UserNotification notification)
    {
        var id = notification.AppInfo.AppUserModelId;
        var name = notification.AppInfo.DisplayInfo.DisplayName;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return false;
        var source = new NotificationSourceInfo(id, name.Trim());
        lock (_gate)
        {
            if (_sources.TryGetValue(id, out var previous) && previous == source)
            {
                return false;
            }

            _sources[id] = source;
            return true;
        }
    }

    private bool RememberNotification(uint id)
    {
        lock (_gate)
        {
            if (!_knownNotificationIds.Add(id))
            {
                return false;
            }

            if (_knownNotificationIds.Count > MaximumRememberedNotificationIds)
            {
                var expiredId = _knownNotificationIds.First(candidate => candidate != id);
                _knownNotificationIds.Remove(expiredId);
            }

            return true;
        }
    }

    private void PublishSources()
    {
        var sources = Sources;
        Dispatch(() => SourcesChanged?.Invoke(this, sources));
    }

    private void PublishNotification(SystemNotificationSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            NotificationReceived?.Invoke(this, snapshot);
            return;
        }

        if (Interlocked.Increment(ref _pendingNotificationDispatches) >
            MaximumPendingNotificationDispatches)
        {
            Interlocked.Decrement(ref _pendingNotificationDispatches);
            return;
        }

        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    if (!_disposed)
                    {
                        NotificationReceived?.Invoke(this, snapshot);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingNotificationDispatches);
                }
            }))
        {
            Interlocked.Decrement(ref _pendingNotificationDispatches);
        }
    }

    private void Dispatch(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(() =>
            {
                if (!_disposed)
                {
                    action();
                }
            });
        }
    }

    private void SetAccessState(NotificationAccessState value)
    {
        if (AccessState == value) return;
        AccessState = value;
        Dispatch(() => AccessStateChanged?.Invoke(this, value));
    }

    private static NotificationAccessState Map(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => NotificationAccessState.Allowed,
        UserNotificationListenerAccessStatus.Denied => NotificationAccessState.Denied,
        UserNotificationListenerAccessStatus.Unspecified => NotificationAccessState.Unspecified,
        _ => NotificationAccessState.Faulted
    };

    private static bool HasPackageIdentity()
    {
        uint length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        if (result == AppModelErrorNoPackage) return false;
        if (result != 122 || length == 0) return result == 0;
        var builder = new StringBuilder(checked((int)length));
        return GetCurrentPackageFullName(ref length, builder) == 0;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        if (_listener is not null && _subscribed)
        {
            _listener.NotificationChanged -= OnNotificationChanged;
        }
        _subscribed = false;
        _listener = null;
        return ValueTask.CompletedTask;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
