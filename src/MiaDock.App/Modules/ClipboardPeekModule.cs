using System.Security.Cryptography;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Core.Clipboard;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;

namespace MiaDock.App.Modules;

public sealed class ClipboardPeekModule : IIslandModule, IAsyncDisposable
{
    public const string ModuleId = "clipboard-peek";
    private const int MaximumTokens = 32;
    private readonly IClipboardPeekService _service;
    private readonly ClipboardPeekViewModel _viewModel;
    private readonly IClipboardPeekSettings _settings;
    private readonly IOverlayWindowHandleProvider _windowHandle;
    private readonly ILocalizationService? _localization;
    private readonly Dictionary<string, CommandToken> _tokens = new(StringComparer.Ordinal);
    private readonly object _tokenGate = new();
    private CancellationTokenSource? _tokenLifetime;
    private bool _isEnabled = true;

    public ClipboardPeekModule(
        IClipboardPeekService service,
        ClipboardPeekViewModel viewModel,
        IClipboardPeekSettings settings,
        IOverlayWindowHandleProvider windowHandle,
        ILocalizationService? localization = null)
    {
        _service = service;
        _viewModel = viewModel;
        _settings = settings;
        _windowHandle = windowHandle;
        _localization = localization;
        _service.StateChanged += OnStateChanged;
        _service.ItemCaptured += OnItemCaptured;
        if (localization is not null) localization.LanguageChanged += OnLanguageChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Clipboard Peek",
        285,
        "ClipboardPeekCompactView",
        "ClipboardPeekExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
        TimeSpan.FromSeconds(3),
        notificationViewKey: "ClipboardPeekNotificationView",
        persistentPriority: 285,
        isPersistent: true,
        iconGlyph: "\uE8C8",
        minimumExpandedHeight: 360,
        displayNameKey: "ClipboardPeek.Title");

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation =>
        LifecycleState != ModuleLifecycleState.Active || _service.Current.CurrentItem is not { } item
            ? null
            : Presentation(item, []);

    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId)
    {
        lock (_tokenGate)
        {
            PruneTokensLocked(DateTimeOffset.UtcNow);
            return _tokens.ContainsKey(commandId);
        }
    }

    public async ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default)
    {
        CommandToken? token;
        lock (_tokenGate)
        {
            PruneTokensLocked(DateTimeOffset.UtcNow);
            if (!_tokens.Remove(commandId, out token)) return false;
        }

        var result = token.Action switch
        {
            ClipboardCommandAction.Copy => await _service.CopyAsync(token.Item, cancellationToken),
            ClipboardCommandAction.Open => await _service.OpenAsync(token.Item, cancellationToken),
            ClipboardCommandAction.OpenFolder => await _service.OpenContainingFolderAsync(token.Item, cancellationToken),
            ClipboardCommandAction.SaveImage when _windowHandle.WindowHandle != 0 =>
                await _service.SaveImageAsync(token.Item, _windowHandle.WindowHandle, cancellationToken),
            _ => ClipboardPeekActionResult.Unavailable
        };
        return result is ClipboardPeekActionResult.Succeeded or ClipboardPeekActionResult.Cancelled;
    }

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Active;
        _tokenLifetime = new CancellationTokenSource();
        await _service.StartAsync(cancellationToken);
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Inactive;
        _tokenLifetime?.Cancel();
        _tokenLifetime?.Dispose();
        _tokenLifetime = null;
        lock (_tokenGate) _tokens.Clear();
        _viewModel.ClearReveal();
        await _service.StopAsync(cancellationToken);
        PresentationChanged?.Invoke(this, null);
    }

    private void OnStateChanged(object? sender, ClipboardPeekState state)
    {
        if (LifecycleState == ModuleLifecycleState.Active)
            PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    private void OnItemCaptured(object? sender, ClipboardPeekItem item)
    {
        if (LifecycleState != ModuleLifecycleState.Active || !IsEnabled || !ShouldNotify(item)) return;
        var now = DateTimeOffset.UtcNow;
        var duration = _settings.EventDuration;
        var expiresAt = now.Add(duration);
        var commands = CreateCommands(item, expiresAt);
        var eventToken = CreateToken();
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            ModuleEventKind.StatusChanged,
            Presentation(item, commands),
            duration,
            now,
            ModuleEventPriority.Normal,
            $"clipboard:{eventToken}",
            expiresAt,
            isFullscreenEligible: false));
        _ = ExpireTokensAsync(expiresAt, _tokenLifetime?.Token ?? CancellationToken.None);
    }

    private bool ShouldNotify(ClipboardPeekItem item)
    {
        if (item.Type == ClipboardPeekContentType.Image && !_settings.Current.ShowImageEvents) return false;
        return _settings.Current.EventMode switch
        {
            ClipboardPeekEventMode.Everything => true,
            ClipboardPeekEventMode.Never => false,
            _ => item.Type is ClipboardPeekContentType.Url or ClipboardPeekContentType.Email or
                ClipboardPeekContentType.Color or ClipboardPeekContentType.File or ClipboardPeekContentType.Folder or
                ClipboardPeekContentType.Image or ClipboardPeekContentType.Sensitive
        };
    }

    private IReadOnlyList<ModuleCommandState> CreateCommands(ClipboardPeekItem item, DateTimeOffset expiresAt)
    {
        var commands = new List<ModuleCommandState>(2);
        void Add(ClipboardCommandAction action, string textKey, string fallback, string glyph)
        {
            if (commands.Count >= 2) return;
            var id = CreateToken();
            lock (_tokenGate)
            {
                PruneTokensLocked(DateTimeOffset.UtcNow);
                while (_tokens.Count >= MaximumTokens)
                {
                    var oldest = _tokens.MinBy(pair => pair.Value.ExpiresAt);
                    if (oldest.Key is null) break;
                    _tokens.Remove(oldest.Key);
                }
                _tokens[id] = new CommandToken(item, action, expiresAt);
            }
            commands.Add(new ModuleCommandState(id, Text(textKey, fallback), glyph, true));
        }

        if (item.AvailableActions.HasFlag(ClipboardPeekCapabilities.Open) ||
            item.AvailableActions.HasFlag(ClipboardPeekCapabilities.ComposeEmail))
            Add(ClipboardCommandAction.Open,
                item.Type == ClipboardPeekContentType.Email ? "ClipboardPeek.ComposeEmail" : "ClipboardPeek.Open",
                item.Type == ClipboardPeekContentType.Email ? "Compose email" : "Open", "\uE8A7");
        if (item.AvailableActions.HasFlag(ClipboardPeekCapabilities.OpenFolder))
            Add(ClipboardCommandAction.OpenFolder, "ClipboardPeek.ShowInFolder", "Show in folder", "\uE838");
        if (item.AvailableActions.HasFlag(ClipboardPeekCapabilities.SaveImage))
            Add(ClipboardCommandAction.SaveImage, "ClipboardPeek.SaveImage", "Save image", "\uE74E");
        if (item.AvailableActions.HasFlag(ClipboardPeekCapabilities.Copy))
            Add(ClipboardCommandAction.Copy, "ClipboardPeek.Copy", "Copy", "\uE8C8");
        return commands;
    }

    private ModulePresentation Presentation(
        ClipboardPeekItem item,
        IReadOnlyList<ModuleCommandState> commands) => new(
        ModuleId,
        Text("ClipboardPeek.Title", "Clipboard Peek"),
        SafeSummary(item),
        Glyph(item.Type),
        item.IsSensitive ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
        valueText: Text($"ClipboardPeek.Type.{item.Type}", item.Type.ToString()),
        isSensitive: true,
        presentationKind: ModulePresentationKind.Status,
        commands: commands);

    private string SafeSummary(ClipboardPeekItem item)
    {
        if (item.IsSensitive) return Text("ClipboardPeek.SensitiveContent", "Sensitive content");
        if (item.ItemCount is { } count)
            return string.Format(Text("ClipboardPeek.MultipleItems", "{0} items copied"), count);
        return item.DisplayText;
    }

    private static string Glyph(ClipboardPeekContentType type) => type switch
    {
        ClipboardPeekContentType.Url => "\uE774",
        ClipboardPeekContentType.Email => "\uE715",
        ClipboardPeekContentType.Color => "\uE790",
        ClipboardPeekContentType.File => "\uE8A5",
        ClipboardPeekContentType.Folder => "\uE8B7",
        ClipboardPeekContentType.Image => "\uEB9F",
        ClipboardPeekContentType.Sensitive => "\uE72E",
        _ => "\uE8C8"
    };

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task ExpireTokensAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        try
        {
            var delay = expiresAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            lock (_tokenGate) PruneTokensLocked(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void PruneTokensLocked(DateTimeOffset now)
    {
        foreach (var key in _tokens.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray())
            _tokens.Remove(key);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public async ValueTask DisposeAsync()
    {
        _service.StateChanged -= OnStateChanged;
        _service.ItemCaptured -= OnItemCaptured;
        if (_localization is not null) _localization.LanguageChanged -= OnLanguageChanged;
        _tokenLifetime?.Cancel();
        _tokenLifetime?.Dispose();
        await _service.DisposeAsync();
    }

    private enum ClipboardCommandAction
    {
        Copy,
        Open,
        OpenFolder,
        SaveImage
    }

    private sealed record CommandToken(
        ClipboardPeekItem Item,
        ClipboardCommandAction Action,
        DateTimeOffset ExpiresAt);
}
