namespace MiaDock.Core.Advertising;

/// <summary>UI-owned window session. All time is monotonic visible time supplied by the host.</summary>
public sealed class AdSliderSession
{
    public static readonly TimeSpan MinimumExposure = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan AutomaticExposure = TimeSpan.FromSeconds(6);
    private readonly HashSet<Uri> _failed = [];
    private List<MiamoAd> _ads = [];
    private List<MiamoAd>? _pending;
    private int _index = -1;
    private TimeSpan _last, _exposure;
    private bool _hasTime;
    public MiamoAd? Current => _index >= 0 && _index < _ads.Count ? _ads[_index] : null;
    public long Revision { get; private set; }
    public bool ImageLoaded { get; private set; }
    public bool IsVisible { get; private set; }
    public TimeSpan VisibleTime => _exposure;
    public int Count => _ads.Count(ad => !_failed.Contains(ad.CreativeUrl));
    public int Position => Current is null ? 0 : _ads.Take(_index + 1).Count(ad => !_failed.Contains(ad.CreativeUrl));
    public bool CanMove => IsVisible && ImageLoaded && _exposure >= MinimumExposure && Count > 1;
    public MiamoAd? Next => Find(1, includeCurrent: false) is var index && index >= 0 ? _ads[index] : null;

    public void QueueAds(IEnumerable<MiamoAd> ads, TimeSpan now)
    {
        Accrue(now);
        _pending = ads.Where(MiamoAdPolicy.IsValid).DistinctBy(ad => ad.Key).ToList();
        if (Current is null) ApplyPending(now, direction: 0);
        // An active image retains its full minimum exposure even if the new list is empty.
    }

    public void MarkImageLoaded(string key, TimeSpan now)
    {
        Accrue(now);
        if (Current?.Key == key) ImageLoaded = true;
    }

    public void MarkImageFailed(Uri uri, TimeSpan now)
    {
        Accrue(now);
        _failed.Add(uri);
        if (Current?.CreativeUrl == uri) ImageLoaded = false;
    }

    public void SetVisible(bool visible, TimeSpan now)
    {
        Accrue(now);
        IsVisible = visible;
    }

    public void Tick(TimeSpan now, bool autoAdvance = true)
    {
        Accrue(now);
        if (!IsVisible || Current is null) return;
        if (_failed.Contains(Current.CreativeUrl)) { Advance(now, 1); return; }
        if (!ImageLoaded) return;
        if (_pending is not null && _exposure >= MinimumExposure || autoAdvance && Count > 1 && _exposure >= AutomaticExposure)
            Advance(now, 1);
    }

    public bool Move(int direction, TimeSpan now)
    {
        Accrue(now);
        if (!CanMove) return false;
        Advance(now, direction < 0 ? -1 : 1);
        return true;
    }

    /// <summary>Failure skipping is allowed before a first rendered frame, only while the pane can present.</summary>
    public void SkipFailed(TimeSpan now, bool canPresent)
    {
        if (canPresent && Current is { } ad && _failed.Contains(ad.CreativeUrl)) Advance(now, 1);
    }

    private void Accrue(TimeSpan now)
    {
        if (_hasTime && now >= _last && IsVisible && ImageLoaded)
            _exposure = TimeSpan.FromTicks(Math.Min(TimeSpan.FromHours(1).Ticks, _exposure.Ticks + (now - _last).Ticks));
        _hasTime = true;
        _last = now;
    }

    private void Advance(TimeSpan now, int direction)
    {
        if (_pending is not null) { ApplyPending(now, direction); return; }
        Select(Find(direction, includeCurrent: true), now);
    }

    private void ApplyPending(TimeSpan now, int direction)
    {
        var old = Current;
        _ads = _pending!; _pending = null; _failed.Clear();
        _index = old is null ? -1 : _ads.FindIndex(ad => ad.Key == old.Key);
        var found = _index;
        var next = direction == 0 ? (_ads.Count > 0 ? 0 : -1) : Find(direction, includeCurrent: true);
        // A refreshed single creative remains static; token updates do not create another exposure.
        if (old is not null && found >= 0 && next == found) return;
        _index = -1;
        Select(next, now);
    }

    private int Find(int direction, bool includeCurrent)
    {
        if (_ads.Count == 0) return -1;
        for (var step = 1; step <= _ads.Count; step++)
        {
            var index = ((_index + step * direction) % _ads.Count + _ads.Count) % _ads.Count;
            if (!includeCurrent && index == _index) continue;
            if (!_failed.Contains(_ads[index].CreativeUrl)) return index;
        }
        return -1;
    }

    private void Select(int index, TimeSpan now)
    {
        _index = index; Revision++; ImageLoaded = false; IsVisible = false;
        _exposure = TimeSpan.Zero; _last = now; _hasTime = true;
    }
}

/// <summary>Campaign deduplication and bounded retries belong to a window, not to a navigation page.</summary>
public sealed class AdImpressionLedger
{
    private sealed class Entry(MiamoImpression impression)
    {
        public readonly MiamoImpression Impression = impression;
        public int Attempts;
        public bool InFlight, Finished;
        public TimeSpan NextAttempt;
    }
    private readonly Dictionary<string, Entry> _campaigns = new(StringComparer.Ordinal);

    public MiamoImpression? TryBegin(AdSliderSession session, TimeSpan now)
    {
        if (!session.IsVisible || !session.ImageLoaded || session.VisibleTime < TimeSpan.FromSeconds(1) || session.Current is not { } ad) return null;
        if (!_campaigns.TryGetValue(ad.CampaignId, out var entry))
            _campaigns[ad.CampaignId] = entry = new(new(ad.CampaignId, Guid.NewGuid(), ad.ImpressionToken));
        if (entry.Finished || entry.InFlight || entry.Attempts >= 3 || now < entry.NextAttempt) return null;
        entry.InFlight = true; entry.Attempts++;
        return entry.Impression;
    }

    public void Complete(MiamoImpression impression, ImpressionResult result, TimeSpan now)
    {
        if (!_campaigns.TryGetValue(impression.CampaignId, out var entry) || entry.Impression.EventId != impression.EventId) return;
        entry.InFlight = false;
        entry.Finished = result.Accepted || !result.Retryable || entry.Attempts >= 3;
        entry.NextAttempt = now + TimeSpan.FromSeconds(entry.Attempts == 1 ? 2 : 10);
    }
}
