namespace MiaDock.Core.Modules;

public sealed record ModulePresentation
{
    public ModulePresentation(
        string moduleId,
        string primaryText,
        string secondaryText,
        string leadingGlyph,
        ModuleIndicatorKind indicator,
        string? indicatorText = null,
        string? valueText = null,
        double? progress = null,
        bool isSensitive = false,
        ModulePresentationKind presentationKind = ModulePresentationKind.Standard,
        IReadOnlyList<ModuleCommandState>? commands = null,
        bool? isPersistentOverride = null,
        int? persistentPriorityOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(primaryText);
        ArgumentNullException.ThrowIfNull(secondaryText);
        ArgumentNullException.ThrowIfNull(leadingGlyph);
        ModuleId = moduleId;
        PrimaryText = primaryText;
        SecondaryText = secondaryText;
        LeadingGlyph = leadingGlyph;
        Indicator = indicator;
        IndicatorText = indicatorText;
        ValueText = valueText;
        Progress = progress is { } value ? Math.Clamp(value, 0, 1) : null;
        IsSensitive = isSensitive;
        PresentationKind = presentationKind;
        Commands = commands ?? Array.Empty<ModuleCommandState>();
        IsPersistentOverride = isPersistentOverride;
        PersistentPriorityOverride = persistentPriorityOverride;
    }

    public string ModuleId { get; }
    public string PrimaryText { get; }
    public string SecondaryText { get; }
    public string LeadingGlyph { get; }
    public ModuleIndicatorKind Indicator { get; }
    public string? IndicatorText { get; }
    public string? ValueText { get; }
    public double? Progress { get; }
    public bool IsSensitive { get; }
    public ModulePresentationKind PresentationKind { get; }
    public IReadOnlyList<ModuleCommandState> Commands { get; }
    public bool? IsPersistentOverride { get; }
    public int? PersistentPriorityOverride { get; }
}
