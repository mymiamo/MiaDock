using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

internal delegate void SystemLoopbackDataHandler(
    ReadOnlySpan<byte> buffer,
    AudioClientBufferFlags flags);

internal interface ISystemLoopbackCapture : IDisposable
{
    WaveFormat WaveFormat { get; }
    string? EndpointId { get; }
    string? EndpointName { get; }
    event SystemLoopbackDataHandler? DataAvailable;
    event EventHandler<StoppedEventArgs>? RecordingStopped;
    void Start();
    void Stop();
}

internal interface ISystemLoopbackCaptureFactory
{
    ISystemLoopbackCapture CreateRenderCapture(string? endpointId);
}

internal sealed class WindowsSystemLoopbackCaptureFactory : ISystemLoopbackCaptureFactory
{
    public ISystemLoopbackCapture CreateRenderCapture(string? endpointId)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = string.IsNullOrWhiteSpace(endpointId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : enumerator.GetDevice(endpointId);
        try
        {
            var recorder = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithEventSync()
                .WithLoopbackCapture()
                .WithBufferLength(40)
                .Build();
            return new NAudioSystemLoopbackCapture(device, recorder);
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }
}

internal sealed class NAudioSystemLoopbackCapture : ISystemLoopbackCapture
{
    private readonly MMDevice _device;
    private readonly WasapiRecorder _recorder;
    private readonly CaptureDataAvailableHandler _dataHandler;
    private bool _disposed;

    public NAudioSystemLoopbackCapture(MMDevice device, WasapiRecorder recorder)
    {
        _device = device;
        _recorder = recorder;
        _dataHandler = (buffer, flags, _, _) => DataAvailable?.Invoke(buffer, flags);
        _recorder.DataAvailable += _dataHandler;
        _recorder.RecordingStopped += OnRecordingStopped;
    }

    public WaveFormat WaveFormat => _recorder.WaveFormat;
    public string? EndpointId => _recorder.DeviceId ?? _device.ID;
    public string? EndpointName => _recorder.DeviceFriendlyName ?? _device.FriendlyName;
    public event SystemLoopbackDataHandler? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
    public void Start() => _recorder.StartRecording();
    public void Stop() => _recorder.StopRecording();

    private void OnRecordingStopped(object? sender, StoppedEventArgs args) =>
        RecordingStopped?.Invoke(this, args);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _recorder.DataAvailable -= _dataHandler;
        _recorder.RecordingStopped -= OnRecordingStopped;
        try
        {
            _recorder.Dispose();
        }
        finally
        {
            _device.Dispose();
        }
    }
}
