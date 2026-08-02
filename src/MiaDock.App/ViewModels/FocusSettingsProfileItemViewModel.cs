using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Focus;

namespace MiaDock.App.ViewModels;

public sealed class FocusSettingsProfileItemViewModel : ObservableObject
{
    private FocusProfile _profile;
    private string _displayName;
    private string _summary;
    private bool _isActive;

    public FocusSettingsProfileItemViewModel(
        FocusProfile profile,
        string displayName,
        string summary,
        bool isActive)
    {
        _profile = profile;
        _displayName = displayName;
        _summary = summary;
        _isActive = isActive;
    }

    public FocusProfile Profile => _profile;

    public string Id => _profile.Id;

    public string DisplayName => _displayName;

    public string Summary => _summary;

    public bool IsActive => _isActive;

    public bool IsBuiltIn => FocusProfileDefaults.BuiltInIds.Contains(Id);

    public bool IsCustom => !IsBuiltIn;

    public string IconGlyph => FocusIconGlyphs.For(_profile.IconKey);

    public string Color => _profile.Color;

    public void Refresh(
        FocusProfile profile,
        string displayName,
        string summary,
        bool isActive)
    {
        _profile = profile;
        _displayName = displayName;
        _summary = summary;
        _isActive = isActive;
        OnPropertyChanged(string.Empty);
    }
}
