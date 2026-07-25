using CommunityToolkit.Mvvm.ComponentModel;

namespace MiaDock.App.ViewModels;

public sealed class OnboardingModuleOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public OnboardingModuleOptionViewModel(
        string moduleId,
        string title,
        string description,
        string iconGlyph,
        bool isSelected,
        bool canSelectDuringOnboarding = true)
    {
        ModuleId = moduleId;
        Title = title;
        Description = description;
        IconGlyph = iconGlyph;
        _isSelected = isSelected;
        CanSelectDuringOnboarding = canSelectDuringOnboarding;
    }

    public string ModuleId { get; }
    public string Title { get; }
    public string Description { get; }
    public string IconGlyph { get; }
    public bool CanSelectDuringOnboarding { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
