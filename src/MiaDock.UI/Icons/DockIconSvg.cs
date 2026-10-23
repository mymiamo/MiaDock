using System.Globalization;
using System.Xml.Linq;

namespace MiaDock.UI.Icons;

public static class DockIconSvg
{
    public static string ReadAsset(string assetName)
    {
        using var stream = typeof(DockIconSvg).Assembly.GetManifestResourceStream($"MiaDock.UI.Ionicons.{assetName}")
            ?? throw new InvalidOperationException($"Missing bundled dock icon: {assetName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ImageIcon does not apply Foreground to its source. Color the local SVG
    // explicitly, preserving transparent interiors and all outline strokes.
    public static string Tint(string svg, uint argb)
    {
        var root = XElement.Parse(svg);
        var color = $"#{argb & 0xFFFFFF:X6}";
        if (root.Attribute("fill") is null) root.SetAttributeValue("fill", color);
        root.SetAttributeValue("color", color);
        root.SetAttributeValue("opacity", ((argb >> 24) / 255d).ToString("R", CultureInfo.InvariantCulture));
        foreach (var attribute in root.DescendantsAndSelf().Attributes()
                     .Where(attribute => attribute.Name.LocalName is "fill" or "stroke").ToArray())
        {
            if (attribute.Value != "none") attribute.Value = color;
        }
        return root.ToString(SaveOptions.DisableFormatting);
    }
}
