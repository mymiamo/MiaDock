using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Dispatching;
using MiaDock.App.Services;
using MiaDock.Modules.SystemStatus.ViewModels;
using System.Runtime.InteropServices;

namespace MiaDock.App.Controls;

public sealed partial class VolumeExpandedView : UserControl, IModuleViewActivationAware
{
    private static readonly TimeSpan VolumeCommitDelay = TimeSpan.FromMilliseconds(80);
    private readonly Dictionary<string, PendingSessionVolume> _pendingSessionVolumes =
        new(StringComparer.Ordinal);
    private DispatcherQueueTimer? _volumeCommitTimer;
    private double? _pendingMasterVolume;
    private bool _isPresentationActive;
    private bool _isFlushingVolumes;
    private bool _drainAllOnCurrentFlush;

    public VolumeExpandedView() => InitializeComponent();

    public void SetPresentationActive(bool isActive)
    {
        if (_isPresentationActive == isActive)
        {
            return;
        }

        _isPresentationActive = isActive;
        if (DataContext is VolumeModuleViewModel viewModel)
        {
            viewModel.SetMixerActive(isActive);
        }

        if (!isActive)
        {
            _volumeCommitTimer?.Stop();
            _ = FlushPendingVolumesAsync(drainAll: true);
        }
    }

    private void OnMasterVolumeChanged(
        object sender,
        RangeBaseValueChangedEventArgs args)
    {
        if (_isPresentationActive &&
            DataContext is VolumeModuleViewModel viewModel &&
            Math.Abs(args.NewValue - viewModel.Snapshot.MasterVolumePercent) >= 1)
        {
            _pendingMasterVolume = args.NewValue;
            ScheduleVolumeCommit();
        }
    }

    private void OnSessionVolumeChanged(
        object sender,
        RangeBaseValueChangedEventArgs args)
    {
        if (_isPresentationActive &&
            sender is FrameworkElement
            {
                DataContext: AudioMixerSessionViewModel session
            } &&
            session.Snapshot.CanControlVolume &&
            Math.Abs(args.NewValue - session.Snapshot.VolumePercent) >= 1)
        {
            _pendingSessionVolumes[session.SessionKey] =
                new PendingSessionVolume(session, args.NewValue);
            ScheduleVolumeCommit();
        }
    }

    private void ScheduleVolumeCommit()
    {
        _volumeCommitTimer ??= CreateVolumeCommitTimer();
        _volumeCommitTimer.Stop();
        _volumeCommitTimer.Start();
    }

    private DispatcherQueueTimer CreateVolumeCommitTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = VolumeCommitDelay;
        timer.IsRepeating = false;
        timer.Tick += OnVolumeCommitTimerTick;
        return timer;
    }

    private async void OnVolumeCommitTimerTick(
        DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        await FlushPendingVolumesAsync();
    }

    private async Task FlushPendingVolumesAsync(bool drainAll = false)
    {
        if (_isFlushingVolumes)
        {
            _drainAllOnCurrentFlush |= drainAll;
            return;
        }

        _isFlushingVolumes = true;
        _drainAllOnCurrentFlush = drainAll;
        try
        {
            while (_pendingMasterVolume is not null ||
                   _pendingSessionVolumes.Count > 0)
            {
                var masterVolume = _pendingMasterVolume;
                _pendingMasterVolume = null;
                var sessions = _pendingSessionVolumes.Values.ToArray();
                _pendingSessionVolumes.Clear();

                if (masterVolume is { } master &&
                    DataContext is VolumeModuleViewModel viewModel)
                {
                    await TrySetVolumeAsync(() =>
                        viewModel.SetMasterVolumeAsync(master));
                }

                foreach (var pending in sessions)
                {
                    await TrySetVolumeAsync(() =>
                        pending.Session.SetVolumeAsync(pending.Percent));
                }

                if (!_drainAllOnCurrentFlush)
                {
                    break;
                }
            }
        }
        finally
        {
            _drainAllOnCurrentFlush = false;
            _isFlushingVolumes = false;
            if (_isPresentationActive &&
                (_pendingMasterVolume is not null ||
                 _pendingSessionVolumes.Count > 0))
            {
                ScheduleVolumeCommit();
            }
        }
    }

    private static async Task TrySetVolumeAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (
            exception is COMException or
                InvalidOperationException or
                OperationCanceledException)
        {
            // Audio sessions may disappear while a pointer gesture is completing.
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        SetPresentationActive(false);

    private sealed record PendingSessionVolume(
        AudioMixerSessionViewModel Session,
        double Percent);
}
