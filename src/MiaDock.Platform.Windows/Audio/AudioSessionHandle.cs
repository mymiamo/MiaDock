using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Audio;

internal sealed class AudioSessionHandle : IDisposable
{
    private readonly IAudioSessionControl _control;
    private readonly IAudioSessionEvents _events;
    private readonly ISimpleAudioVolume? _volume;
    private readonly IAudioMeterInformation? _meter;
    private bool _disposed;

    public AudioSessionHandle(
        IAudioSessionControl control,
        AudioDataFlow flow,
        Action refreshRequested,
        Action rebindRequested)
    {
        _control = control;
        Flow = flow;
        Control2 = (IAudioSessionControl2)control;
        CoreAudioNative.ThrowIfFailed(Control2.GetProcessId(out var processId));
        ProcessId = processId;
        IsSystemSounds = Control2.IsSystemSoundsSession() == 0;
        var process = ResolveProcess(processId);
        ProcessName = process.Name;
        ProcessExecutablePath = process.ExecutablePath;
        SessionId = ResolveSessionId();
        MixerKey = IsSystemSounds
            ? "system-sounds"
            : ProcessId > 0
                ? $"process:{ProcessId}"
                : $"session:{SessionId}";
        _volume = TryCast<ISimpleAudioVolume>(control);
        _meter = TryCast<IAudioMeterInformation>(control);
        _events = new AudioSessionEventsCallback(refreshRequested, rebindRequested);
        CoreAudioNative.ThrowIfFailed(_control.RegisterAudioSessionNotification(_events));
        Refresh();
    }

    public AudioDataFlow Flow { get; }
    public IAudioSessionControl2 Control2 { get; }
    public uint ProcessId { get; }
    public string ProcessName { get; }
    public string ProcessExecutablePath { get; }
    public string SessionId { get; }
    public string MixerKey { get; }
    public string DisplayName { get; private set; } = string.Empty;
    public string IconPath { get; private set; } = string.Empty;
    public bool IsSystemSounds { get; }
    public bool CanControlVolume => _volume is not null;
    public AudioSessionState State { get; private set; }
    public float VolumeLevel { get; private set; }
    public bool IsMuted { get; private set; }
    public float PeakLevel { get; private set; }

    public void Refresh()
    {
        if (Control2.GetState(out var state) >= 0)
        {
            State = state;
        }

        if (_control.GetDisplayName(out var displayName) >= 0)
        {
            DisplayName = displayName ?? string.Empty;
        }

        if (_control.GetIconPath(out var iconPath) >= 0)
        {
            IconPath = !string.IsNullOrWhiteSpace(iconPath)
                ? iconPath
                : ProcessExecutablePath;
        }

        if (_volume is not null && _volume.GetMasterVolume(out var volume) >= 0)
        {
            VolumeLevel = volume;
        }

        if (_volume is not null && _volume.GetMute(out var muted) >= 0)
        {
            IsMuted = muted;
        }
    }

    public bool SetVolume(float volume, ref Guid eventContext) =>
        _volume is not null &&
        _volume.SetMasterVolume(Math.Clamp(volume, 0, 1), ref eventContext) >= 0;

    public bool ToggleMute(ref Guid eventContext)
    {
        if (_volume is null || _volume.GetMute(out var muted) < 0)
        {
            return false;
        }

        return _volume.SetMute(!muted, ref eventContext) >= 0;
    }

    public bool SetMute(bool muted, ref Guid eventContext) =>
        _volume is not null &&
        _volume.SetMute(muted, ref eventContext) >= 0;

    public float SamplePeak()
    {
        if (_meter is null || _meter.GetPeakValue(out var peak) < 0)
        {
            PeakLevel = 0;
            return PeakLevel;
        }

        PeakLevel = Math.Clamp(peak, 0, 1);
        return PeakLevel;
    }

    public void ResetPeak() => PeakLevel = 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _control.UnregisterAudioSessionNotification(_events);
        }
        catch (COMException)
        {
        }

        if (Marshal.IsComObject(_control))
        {
            Marshal.FinalReleaseComObject(_control);
        }
    }

    private string ResolveSessionId()
    {
        try
        {
            if (Control2.GetSessionInstanceIdentifier(out var sessionId) >= 0 &&
                !string.IsNullOrWhiteSpace(sessionId))
            {
                return sessionId;
            }
        }
        catch (COMException)
        {
        }

        return $"{ProcessId}:{Guid.NewGuid():N}";
    }

    private static (string Name, string ExecutablePath) ResolveProcess(uint processId)
    {
        if (processId == 0 || processId == Environment.ProcessId)
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            string executablePath;
            try
            {
                executablePath = process.MainModule?.FileName ?? string.Empty;
            }
            catch (Exception)
            {
                executablePath = string.Empty;
            }

            return (process.ProcessName, executablePath);
        }
        catch (Exception)
        {
            return (string.Empty, string.Empty);
        }
    }

    private static T? TryCast<T>(object value) where T : class
    {
        try
        {
            return value as T;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    [ComVisible(true)]
    private sealed class AudioSessionEventsCallback(
        Action refreshRequested,
        Action rebindRequested) : IAudioSessionEvents
    {
        public int OnDisplayNameChanged(string displayName, ref Guid eventContext)
        {
            refreshRequested();
            return 0;
        }

        public int OnIconPathChanged(string iconPath, ref Guid eventContext)
        {
            refreshRequested();
            return 0;
        }

        public int OnSimpleVolumeChanged(float volume, bool isMuted, ref Guid eventContext)
        {
            refreshRequested();
            return 0;
        }

        public int OnChannelVolumeChanged(uint channelCount, nint channelVolumes, uint changedChannel, ref Guid eventContext) => 0;
        public int OnGroupingParamChanged(ref Guid groupingId, ref Guid eventContext) => 0;

        public int OnStateChanged(AudioSessionState state)
        {
            rebindRequested();
            return 0;
        }

        public int OnSessionDisconnected(AudioSessionDisconnectReason reason)
        {
            rebindRequested();
            return 0;
        }
    }
}
