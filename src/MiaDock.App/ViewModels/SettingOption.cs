namespace MiaDock.App.ViewModels;

public sealed record SettingOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
