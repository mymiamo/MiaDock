using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Modules.SystemStatus;

public sealed class PrivacyModule : IIslandModule, IDisposable
{
    public const string ModuleId = "privacy";

    private readonly IPrivacyUsageService _service;
    private readonly PrivacyModuleViewModel _viewModel;
    private readonly ILocalizationService? _localization;
    private PrivacyState? _previous;
    private bool _isEnabled = true;

    public PrivacyModule(
        IPrivacyUsageService service,
        PrivacyModuleViewModel viewModel,
        ILocalizationService? localization = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _localization = localization;
        _service.StateChanged += OnStateChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Gizlilik",
        190,
        "PrivacyCompactView",
        "PrivacyExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
        TimeSpan.FromSeconds(3.5),
        [],
        "PrivacyNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE72E",
        minimumExpandedHeight: 300);

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active
        ? CreatePresentation(_viewModel.State)
        : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => false;

    public ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _service.StartAsync(cancellationToken);
        LifecycleState = ModuleLifecycleState.Active;
        _previous = _service.Current;
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    private void OnStateChanged(object? sender, PrivacyState state)
    {
        var previous = _previous;
        _previous = state;
        if (LifecycleState != ModuleLifecycleState.Active)
        {
            return;
        }

        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (previous is null)
        {
            return;
        }

        var moduleEvent = CreateEvent(previous, state);
        if (moduleEvent is not null)
        {
            EventOccurred?.Invoke(this, moduleEvent);
        }
    }

    private ModuleEvent? CreateEvent(PrivacyState previous, PrivacyState current)
    {
        if (StatesEquivalent(previous, current))
        {
            return null;
        }

        if (!current.MicrophoneInUse && !current.CameraInUse)
        {
            return null;
        }

        var lead = current.ActiveApplications.FirstOrDefault();
        if (lead is null)
        {
            return null;
        }

        var title = lead.DisplayName;
        var secondary = DescribeUsage(lead);
        var glyph = lead.UsesCamera ? "\uE714" : "\uE720";
        return new ModuleEvent(
            ModuleId,
            ModuleEventKind.StatusChanged,
            new ModulePresentation(
                ModuleId,
                title,
                secondary,
                glyph,
                ModuleIndicatorKind.StatusDot,
                presentationKind: ModulePresentationKind.Status),
            Descriptor.DefaultDisplayDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.High,
            BuildCoalescingKey(current));
    }

    private ModulePresentation CreatePresentation(PrivacyState state)
    {
        var hasUsage = state.MicrophoneInUse || state.CameraInUse;
        var title = hasUsage
            ? state.ActiveApplications.FirstOrDefault()?.DisplayName ?? Text("Privacy_Title", "Gizlilik")
            : Text("Privacy_Title", "Gizlilik");
        var secondary = hasUsage
            ? _viewModel.SummaryText
            : Text("Privacy_NoActiveDevices", "Aktif gizlilik kullanımı yok");
        return new ModulePresentation(
            ModuleId,
            title,
            secondary,
            _viewModel.StatusGlyph,
            hasUsage ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
            valueText: hasUsage ? state.ActiveApplications.Count.ToString() : null,
            presentationKind: ModulePresentationKind.Status,
            isPersistentOverride: hasUsage,
            persistentPriorityOverride: hasUsage ? 460 : null);
    }

    private string DescribeUsage(PrivacyApplication app)
    {
        if (app.UsesBoth)
        {
            return Text("Privacy_CameraAndMicrophoneInUse", "Kamera ve mikrofon kullanılıyor");
        }

        if (app.UsesCamera)
        {
            return Text("Privacy_CameraInUse", "Kamera kullanılıyor");
        }

        return Text("Privacy_MicrophoneInUse", "Mikrofon kullanılıyor");
    }

    private static string BuildCoalescingKey(PrivacyState state)
    {
        var ids = string.Join(
            '|',
            state.ActiveApplications.Select(app =>
                $"{app.Id}:{(app.UsesMicrophone ? "m" : string.Empty)}{(app.UsesCamera ? "c" : string.Empty)}"));
        return "privacy:" + ids;
    }

    private static bool StatesEquivalent(PrivacyState left, PrivacyState right)
    {
        if (left.MicrophoneInUse != right.MicrophoneInUse ||
            left.CameraInUse != right.CameraInUse ||
            left.ActiveApplications.Count != right.ActiveApplications.Count)
        {
            return false;
        }

        for (var index = 0; index < left.ActiveApplications.Count; index++)
        {
            var a = left.ActiveApplications[index];
            var b = right.ActiveApplications[index];
            if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
                a.UsesMicrophone != b.UsesMicrophone ||
                a.UsesCamera != b.UsesCamera)
            {
                return false;
            }
        }

        return true;
    }

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public void Dispose()
    {
        _service.StateChanged -= OnStateChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
