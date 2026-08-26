using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Platform.Windows.Audio;

public sealed class CommunicationActivityStabilizer
{
    public static readonly TimeSpan DefaultActivationDelay = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan DefaultEndGracePeriod = TimeSpan.FromMilliseconds(1500);

    private readonly TimeSpan _activationDelay;
    private readonly TimeSpan _endGracePeriod;
    private CallActivityInfo _current = CallActivityInfo.None;
    private CommunicationApp _pendingApp;
    private DateTimeOffset? _pendingSince;
    private DateTimeOffset? _evidenceLostAt;

    public CommunicationActivityStabilizer(
        TimeSpan? activationDelay = null,
        TimeSpan? endGracePeriod = null)
    {
        _activationDelay = activationDelay ?? DefaultActivationDelay;
        _endGracePeriod = endGracePeriod ?? DefaultEndGracePeriod;
    }

    public DateTimeOffset? NextEvaluationAt { get; private set; }

    public CallActivityInfo Update(CommunicationActivityResult evidence, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        NextEvaluationAt = null;

        if (_current.State == CallActivityState.Detected)
        {
            if (evidence.State == CallActivityState.Detected && evidence.App == _current.App)
            {
                _evidenceLostAt = null;
                _pendingApp = CommunicationApp.Unknown;
                _pendingSince = null;
                _current = evidence.ToCallInfo(_current.StartedAt);
                return _current;
            }

            _evidenceLostAt ??= now;
            var endAt = _evidenceLostAt.Value + _endGracePeriod;
            if (now < endAt)
            {
                NextEvaluationAt = endAt;
                return _current;
            }

            _current = CallActivityInfo.None;
            _evidenceLostAt = null;
            _pendingApp = CommunicationApp.Unknown;
            _pendingSince = null;
        }

        if (evidence.State != CallActivityState.Detected)
        {
            _pendingApp = CommunicationApp.Unknown;
            _pendingSince = null;
            _current = evidence.ToCallInfo();
            return _current;
        }

        if (_pendingApp != evidence.App)
        {
            _pendingApp = evidence.App;
            _pendingSince = now;
        }

        var activationAt = _pendingSince!.Value + _activationDelay;
        if (now < activationAt)
        {
            NextEvaluationAt = activationAt;
            _current = (evidence with { State = CallActivityState.Possible }).ToCallInfo();
            return _current;
        }

        _pendingApp = CommunicationApp.Unknown;
        _pendingSince = null;
        _current = evidence.ToCallInfo(now);
        return _current;
    }

    public void Reset()
    {
        _current = CallActivityInfo.None;
        _pendingApp = CommunicationApp.Unknown;
        _pendingSince = null;
        _evidenceLostAt = null;
        NextEvaluationAt = null;
    }
}
