using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MiaDock.App.Controls;

public sealed partial class AlbumArtwork : UserControl
{
    public static readonly DependencyProperty ArtworkCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(ArtworkCornerRadius),
            typeof(CornerRadius),
            typeof(AlbumArtwork),
            new PropertyMetadata(new CornerRadius(14)));

    public AlbumArtwork() => InitializeComponent();

    public CornerRadius ArtworkCornerRadius
    {
        get => (CornerRadius)GetValue(ArtworkCornerRadiusProperty);
        set => SetValue(ArtworkCornerRadiusProperty, value);
    }
}
