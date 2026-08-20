using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.App.Services;
using MiaDock.Core.Clipboard;
using MiaDock.Core.Localization;
using Windows.Storage.Streams;
using Windows.UI;

namespace MiaDock.App.ViewModels;

public sealed partial class ClipboardPeekViewModel : ObservableObject, IDisposable
{
    private readonly IClipboardPeekService _service;
    private readonly IOverlayWindowHandleProvider _windowHandle;
    private readonly ILocalizationService? _localization;
    private InMemoryRandomAccessStream? _thumbnailStream;
    private CancellationTokenSource? _revealLifetime;
    private string? _revealedValue;
    private int _copyBusy;

    public ClipboardPeekViewModel(
        IClipboardPeekService service,
        IOverlayWindowHandleProvider windowHandle,
        ILocalizationService? localization = null)
    {
        _service = service;
        _windowHandle = windowHandle;
        _localization = localization;
        State = service.Current;
        SelectedItem = State.CurrentItem;
        RebuildHistory();
        service.StateChanged += OnStateChanged;
        if (localization is not null) localization.LanguageChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    public partial ClipboardPeekState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItem), nameof(HasCurrentItem), nameof(CurrentDisplayText),
        nameof(CurrentTypeText), nameof(CanCopy), nameof(CanOpen), nameof(CanOpenFolder),
        nameof(CanSaveImage), nameof(CanReveal), nameof(HasColorPreview), nameof(HasColorFormats),
        nameof(ColorHexText), nameof(ColorRgbText), nameof(ColorHslText), nameof(HasTextStats),
        nameof(TextStatsText), nameof(CompactDetailText))]
    public partial ClipboardPeekItem? SelectedItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    public partial ImageSource? Thumbnail { get; set; }

    [ObservableProperty]
    public partial Brush? ColorPreviewBrush { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ClipboardPeekHistoryEntry> HistoryEntries { get; set; } = [];

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCopy))]
    public partial bool IsCopyBusy { get; set; }

    public ClipboardPeekItem? CurrentItem => SelectedItem;
    public bool HasCurrentItem => CurrentItem is not null;
    public bool HasHistory => HistoryEntries.Count > 0;
    public bool HasThumbnail => Thumbnail is not null;
    public bool HasColorPreview => ColorPreviewBrush is not null;
    public bool HasColorFormats => ColorFormats is not null;
    public string ColorHexText => FormatColor("ClipboardPeek.Color.Hex", "HEX {0}", formats => formats.Hex);
    public string ColorRgbText => FormatColor("ClipboardPeek.Color.Rgb", "RGB {0}", formats => formats.RgbChannelsDisplay);
    public string ColorHslText => FormatColor("ClipboardPeek.Color.Hsl", "HSL {0}", formats => formats.HslDisplay);
    public bool HasTextStats => ClipboardTextStats.TryCreate(CurrentItem) is not null;
    public string TextStatsText => ClipboardTextStats.TryCreate(CurrentItem) is { } stats
        ? string.Format(Text("ClipboardPeek.TextStats", "{0} words · {1} characters · {2} lines"), stats.WordCount, stats.CharacterCount, stats.LineCount)
        : string.Empty;
    public string CompactDetailText => ColorFormats is { } formats
        ? formats.Hex
        : HasTextStats ? TextStatsText : CurrentTypeText;
    public bool CanCopy => CurrentItem?.AvailableActions.HasFlag(ClipboardPeekCapabilities.Copy) == true;
    public bool CanOpen => CurrentItem?.AvailableActions.HasFlag(ClipboardPeekCapabilities.Open) == true ||
                           CurrentItem?.AvailableActions.HasFlag(ClipboardPeekCapabilities.ComposeEmail) == true;
    public bool CanOpenFolder => CurrentItem?.AvailableActions.HasFlag(ClipboardPeekCapabilities.OpenFolder) == true;
    public bool CanSaveImage => CurrentItem?.AvailableActions.HasFlag(ClipboardPeekCapabilities.SaveImage) == true &&
                                _windowHandle.WindowHandle != 0;
    public bool CanReveal => CurrentItem?.IsRevealable == true && _revealedValue is null;
    public string CurrentDisplayText => _revealedValue ?? DisplayText(CurrentItem) ?? EmptyText;
    public string CurrentTypeText => CurrentItem is null ? string.Empty : TypeText(CurrentItem.Type);
    public string TitleText => Text("ClipboardPeek.Title", "Clipboard Peek");
    public string HistoryText => Text("ClipboardPeek.History", "Recent copies");
    public string ClearHistoryText => Text("ClipboardPeek.ClearHistory", "Clear history");
    public string CopyText => Text("ClipboardPeek.Copy", "Copy");
    public string OpenText => CurrentItem?.Type == ClipboardPeekContentType.Email
        ? Text("ClipboardPeek.ComposeEmail", "Compose email")
        : Text("ClipboardPeek.Open", "Open");
    public string OpenFolderText => CurrentItem?.Type == ClipboardPeekContentType.File
        ? Text("ClipboardPeek.ShowInFolder", "Show in folder")
        : Text("ClipboardPeek.OpenFolder", "Open folder");
    public string SaveImageText => Text("ClipboardPeek.SaveImage", "Save image");
    public string RevealText => Text("ClipboardPeek.Reveal", "Show once");
    public string EmptyText => Text("ClipboardPeek.Empty", "Copy something to see it here.");

    private ClipboardColorFormats? ColorFormats =>
        ClipboardColorFormats.TryParse(CurrentItem?.ColorValue, out var formats) ? formats : null;

    partial void OnSelectedItemChanged(ClipboardPeekItem? value)
    {
        ClearReveal();
        IsStatusOpen = false;
        UpdateColorPreview(value?.ColorValue);
        _ = LoadThumbnailAsync(value?.Image?.ThumbnailPng);
        foreach (var property in new[]
                 {
                     nameof(CurrentItem), nameof(HasCurrentItem), nameof(CurrentDisplayText), nameof(CurrentTypeText),
                     nameof(CanCopy), nameof(CanOpen), nameof(CanOpenFolder), nameof(CanSaveImage), nameof(CanReveal),
                     nameof(OpenText), nameof(OpenFolderText), nameof(HasColorPreview), nameof(HasColorFormats),
                     nameof(ColorHexText), nameof(ColorRgbText), nameof(ColorHslText), nameof(HasTextStats),
                     nameof(TextStatsText), nameof(CompactDetailText)
                 })
            OnPropertyChanged(property);
    }

    [RelayCommand]
    private void SelectItem(ClipboardPeekItem? item)
    {
        if (item is not null) SelectedItem = item;
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (CurrentItem is null || !TryBeginCopy()) return;
        try { ShowResult(await _service.CopyAsync(CurrentItem)); }
        finally { EndCopy(); }
    }

    [RelayCommand]
    private async Task CopyColorFormatAsync(string? format)
    {
        if (ColorFormats is not { } formats || !TryBeginCopy()) return;
        var text = format switch
        {
            "Hex" => formats.Hex,
            "Rgb" => formats.Rgb,
            "Hsl" => formats.Hsl,
            _ => null
        };
        if (string.IsNullOrEmpty(text)) { EndCopy(); return; }
        try { ShowResult(await _service.CopyTextAsync(text)); }
        finally { EndCopy(); }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (CurrentItem is not null) ShowResult(await _service.OpenAsync(CurrentItem));
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (CurrentItem is not null) ShowResult(await _service.OpenContainingFolderAsync(CurrentItem));
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        if (CurrentItem is not null && _windowHandle.WindowHandle != 0)
            ShowResult(await _service.SaveImageAsync(CurrentItem, _windowHandle.WindowHandle));
    }

    [RelayCommand]
    private async Task RevealAsync()
    {
        if (CurrentItem is null) return;
        var result = await _service.RevealSensitiveAsync(CurrentItem.Id);
        if (result.Result != ClipboardPeekActionResult.Succeeded || result.Value is null)
        {
            ShowResult(result.Result);
            return;
        }
        ClearReveal();
        _revealedValue = result.Value;
        OnPropertyChanged(nameof(CurrentDisplayText));
        OnPropertyChanged(nameof(CanReveal));
        _revealLifetime = new CancellationTokenSource();
        _ = ClearRevealAfterDelayAsync(_revealLifetime.Token);
    }

    [RelayCommand]
    private async Task ClearHistoryAsync() => ShowResult(await _service.ClearHistoryAsync());

    public void ClearReveal()
    {
        _revealLifetime?.Cancel();
        _revealLifetime?.Dispose();
        _revealLifetime = null;
        if (_revealedValue is null) return;
        _revealedValue = null;
        OnPropertyChanged(nameof(CurrentDisplayText));
        OnPropertyChanged(nameof(CanReveal));
    }

    private async Task ClearRevealAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            ClearReveal();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnStateChanged(object? sender, ClipboardPeekState state)
    {
        State = state;
        SelectedItem = state.CurrentItem;
        RebuildHistory();
    }

    private void RebuildHistory()
    {
        var now = DateTimeOffset.UtcNow;
        HistoryEntries = State.History.Select(item => new ClipboardPeekHistoryEntry(
            item,
            DisplayText(item) ?? string.Empty,
            TypeText(item.Type),
            RelativeTime(item.CreatedAt, now))).ToArray();
        OnPropertyChanged(nameof(HasHistory));
    }

    private string? DisplayText(ClipboardPeekItem? item)
    {
        if (item is null) return null;
        if (item.IsSensitive) return Text("ClipboardPeek.SensitiveContent", "Sensitive content");
        if (item.ItemCount is { } count)
            return string.Format(Text("ClipboardPeek.MultipleItems", "{0} items copied"), count);
        return item.RawText ?? item.DisplayText;
    }

    private string RelativeTime(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var elapsed = now - createdAt;
        if (elapsed < TimeSpan.FromMinutes(1)) return Text("ClipboardPeek.Time.Now", "Now");
        if (elapsed < TimeSpan.FromHours(1))
            return string.Format(Text("ClipboardPeek.Time.Minutes", "{0} min ago"), Math.Max(1, (int)elapsed.TotalMinutes));
        return string.Format(Text("ClipboardPeek.Time.Hours", "{0} h ago"), Math.Max(1, (int)elapsed.TotalHours));
    }

    private async Task LoadThumbnailAsync(byte[]? bytes)
    {
        _thumbnailStream?.Dispose();
        _thumbnailStream = null;
        Thumbnail = null;
        if (bytes is not { Length: > 0 }) return;
        try
        {
            var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            stream.Seek(0);
            var image = new BitmapImage();
            image.SetSource(stream);
            _thumbnailStream = stream;
            Thumbnail = image;
        }
        catch
        {
            _thumbnailStream?.Dispose();
            _thumbnailStream = null;
            Thumbnail = null;
        }
    }

    private void UpdateColorPreview(string? value)
    {
        ColorPreviewBrush = TryParseColor(value, out var color) ? new SolidColorBrush(color) : null;
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (!ClipboardColorFormats.TryParse(value, out var formats)) return false;
        color = Color.FromArgb(formats.Alpha, formats.Red, formats.Green, formats.Blue);
        return true;
    }

    private bool TryBeginCopy()
    {
        if (Interlocked.CompareExchange(ref _copyBusy, 1, 0) != 0) return false;
        IsCopyBusy = true;
        return true;
    }

    private void EndCopy()
    {
        Interlocked.Exchange(ref _copyBusy, 0);
        IsCopyBusy = false;
    }

    private void ShowResult(ClipboardPeekActionResult result)
    {
        IsStatusOpen = true;
        StatusSeverity = result == ClipboardPeekActionResult.Succeeded
            ? InfoBarSeverity.Success
            : result == ClipboardPeekActionResult.Cancelled
                ? InfoBarSeverity.Informational
                : InfoBarSeverity.Error;
        StatusMessage = Text($"ClipboardPeek.Action.{result}", result.ToString());
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildHistory();
        foreach (var name in new[]
                 {
                     nameof(TitleText), nameof(HistoryText), nameof(ClearHistoryText), nameof(CopyText),
                     nameof(OpenText), nameof(OpenFolderText), nameof(SaveImageText), nameof(RevealText), nameof(EmptyText),
                     nameof(CurrentDisplayText), nameof(CurrentTypeText), nameof(ColorHexText), nameof(ColorRgbText),
                     nameof(ColorHslText), nameof(TextStatsText), nameof(CompactDetailText)
                 })
            OnPropertyChanged(name);
    }

    private string TypeText(ClipboardPeekContentType type) =>
        Text($"ClipboardPeek.Type.{type}", type.ToString());

    private string FormatColor(string key, string fallback, Func<ClipboardColorFormats, string> selector) =>
        ColorFormats is { } formats ? string.Format(Text(key, fallback), selector(formats)) : string.Empty;

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public void Dispose()
    {
        _service.StateChanged -= OnStateChanged;
        if (_localization is not null) _localization.LanguageChanged -= OnLanguageChanged;
        ClearReveal();
        _thumbnailStream?.Dispose();
    }
}

public sealed record ClipboardPeekHistoryEntry(
    ClipboardPeekItem Item,
    string DisplayText,
    string TypeText,
    string RelativeTime);
