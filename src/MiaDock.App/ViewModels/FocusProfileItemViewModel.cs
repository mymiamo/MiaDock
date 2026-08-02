using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Focus;

namespace MiaDock.App.ViewModels;

public sealed partial class FocusProfileItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public FocusProfileItemViewModel(
        FocusProfile profile,
        string displayName,
        bool isActive)
    {
        Profile = profile;
        DisplayName = displayName;
        IsActive = isActive;
    }

    public FocusProfile Profile { get; }

    public string Id => Profile.Id;

    public string IconGlyph => FocusIconGlyphs.For(Profile.IconKey);

    public string Color => Profile.Color;

    public double ActiveOpacity => IsActive ? 1 : 0.68;

    public double ActiveIndicatorOpacity => IsActive ? 1 : 0;

    public bool CanActivate => !IsActive;

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveOpacity));
        OnPropertyChanged(nameof(ActiveIndicatorOpacity));
        OnPropertyChanged(nameof(CanActivate));
    }
}

public static class FocusIconGlyphs
{
    public static string For(string iconKey) => iconKey switch
    {
        "briefcase" => "\uE821",
        "game-controller" => "\uE7FC",
        "moon" => "\uE708",
        "do-not-disturb" => "\uE711",
        "book" => "\uE82D",
        "fitness" => "\uE95E",
        "leaf" => "\uE8BE",
        _ => "\uE734"
    };
}
