using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MiaDock.App.Controls;

public sealed partial class ModuleAvailabilityBadge : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ModuleAvailabilityBadge), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(ModuleAvailabilityBadge), new PropertyMetadata("\uE946"));

    public ModuleAvailabilityBadge()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
