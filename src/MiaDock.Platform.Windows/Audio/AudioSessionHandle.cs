using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Audio;

internal sealed class AudioSessionHandle : IDisposable
{
    private readonly IAudioSessionControl _control;
    private readonly IAudioSessionEvents _events;
    private bool _disposed;

    public AudioSessionHandle(
        IAudioSessionControl control,
        AudioDataFlow flow,
        Action refreshRequested)
    {
        _control = control;
        Flow = flow;
        Control2 = (IAudioSessionControl2)control;
        Volume = (ISimpleAudioVolume)control;
        CoreAudioNative.ThrowIfFailed(Control2.GetProcessId(out var processId));
        ProcessId = processId;
        ProcessName = ResolveProcessName(processId);
        _events = new AudioSessionEventsCallback(refreshRequested);
        CoreAudioNative.ThrowIfFailed(_control.RegisterAudioSessionNotification(_events));
        Refresh();
    }

    public AudioDataFlow Flow { get; }
    public IAudioSessionControl2 Control2 { get; }
    public ISimpleAudioVolume Volume { get; }
    public uint ProcessId { get; }
    public string ProcessName { get; }
    public AudioSessionState State { get; private set; }
    public float VolumeLevel { get; private set; }
    public bool IsMuted { get; private set; }

    public void Refresh()
    {
        if (Control2.GetState(out var state) >= 0)
        {
            State = state;
        }

        if (Volume.GetMasterVolume(out var volume) >= 0)
        {
            VolumeLevel = volume;
        }

        if (Volume.GetMute(out var muted) >= 0)
        {
            IsMuted = muted;
        }
    }

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

    private static string ResolveProcessName(uint processId)
    {
        if (processId == 0 || processId == Environment.ProcessId)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    [ComVisible(true)]
    private sealed class AudioSessionEventsCallback(Action refreshRequested) : IAudioSessionEvents
    {
        public int OnDisplayNameChanged(string displayName, ref Guid eventContext) => 0;
        public int OnIconPathChanged(string iconPath, ref Guid eventContext) => 0;

        public int OnSimpleVolumeChanged(float volume, bool isMuted, ref Guid eventContext)
        {
            refreshRequested();
            return 0;
        }

        public int OnChannelVolumeChanged(uint channelCount, nint channelVolumes, uint changedChannel, ref Guid eventContext) => 0;
        public int OnGroupingParamChanged(ref Guid groupingId, ref Guid eventContext) => 0;

        public int OnStateChanged(AudioSessionState state)
        {
            refreshRequested();
            return 0;
        }

        public int OnSessionDisconnected(AudioSessionDisconnectReason reason)
        {
            refreshRequested();
            return 0;
        }
    }
}
