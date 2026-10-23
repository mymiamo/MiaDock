using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MiaDock.UI.Icons;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class DockIconCatalogTests
{
    [TestMethod]
    public void AllModuleDescriptorGlyphs_HaveIosEquivalents()
    {
        string[] descriptors = ["\uE121", "\uE995", "\uE823", "\uE8C8", "\uE88E", "\uE717",
            "\uE895", "\uE72E", "\uE850", "\uE765", "\uE896", "\uE702", "\uE8D6", "\uE7F4", "\uE701", "\uEA8F"];
        foreach (var glyph in descriptors)
            Assert.AreNotEqual(DockIconCatalog.FallbackKey, DockIconCatalog.Resolve(glyph), glyph);
    }

    [TestMethod]
    public void EveryCatalogEntry_HasBothBundledVariants_WithVerifiedUpstreamBytes()
    {
        using var stream = typeof(DockIconCatalog).Assembly.GetManifestResourceStream("MiaDock.UI.Ionicons.manifest.json")!;
        using var manifest = JsonDocument.Parse(stream);
        Assert.AreEqual(DockIconCatalog.Version, manifest.RootElement.GetProperty("version").GetString());
        foreach (var key in DockIconCatalog.Keys)
        foreach (var filled in new[] { false, true })
        {
            var asset = DockIconCatalog.AssetName(key, filled);
            var svg = DockIconSvg.ReadAsset(asset);
            var expected = manifest.RootElement.GetProperty("assets").GetProperty(asset).GetString();
            Assert.AreEqual(expected, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg))), asset);
            var root = XElement.Parse(svg);
            Assert.AreEqual("svg", root.Name.LocalName, asset);
            Assert.AreEqual("0 0 512 512", root.Attribute("viewBox")?.Value, asset);
            Assert.IsTrue(root.Descendants().Any(), asset);
            Assert.IsFalse(root.Descendants().Any(element => element.Name.LocalName is "script" or "image" or "use" or "text" or "style"), asset);
        }
        Assert.AreNotEqual(DockIconSvg.ReadAsset("home-outline.svg"), DockIconSvg.ReadAsset("home.svg"));
    }

    [TestMethod]
    public void UnknownGlyphOrKey_UsesLocalIosFallback()
    {
        Assert.AreEqual("help-circle", DockIconCatalog.Resolve(null));
        Assert.AreEqual("help-circle", DockIconCatalog.Resolve("not-a-glyph", "not-a-key"));
        Assert.AreEqual("home", DockIconCatalog.Resolve("\uE80F"));
        Assert.AreEqual("moon", DockIconCatalog.Resolve("\uE80F", "moon"));
        Assert.AreEqual("help-circle-outline.svg", DockIconCatalog.AssetName("unknown", false));
    }

    [TestMethod]
    public void DynamicStates_KeepDistinctTransportAudioPrivacyAndDeviceSemantics()
    {
        Assert.AreEqual("play", DockIconCatalog.Resolve("\uE768"));
        Assert.AreEqual("pause", DockIconCatalog.Resolve("\uE769"));
        Assert.AreEqual("volume-mute", DockIconCatalog.Resolve("\uE74F"));
        Assert.AreEqual("volume-high", DockIconCatalog.Resolve("\uE767"));
        Assert.AreEqual("volume-low", DockIconCatalog.Resolve("\uE993"));
        Assert.AreEqual("volume-medium", DockIconCatalog.Resolve("\uE995"));
        Assert.AreEqual("wifi", DockIconCatalog.Resolve("\uE701"));
        Assert.AreEqual("git-network", DockIconCatalog.Resolve("\uE839"));
        Assert.AreEqual("cellular", DockIconCatalog.Resolve("\uEC05"));
        Assert.AreEqual("bluetooth", DockIconCatalog.Resolve("\uE702"));
        Assert.AreEqual("lock-closed", DockIconCatalog.Resolve("\uE72E"));
        Assert.AreEqual("mic", DockIconCatalog.Resolve("\uE720"));
        Assert.AreEqual("videocam", DockIconCatalog.Resolve("\uE714"));
        Assert.AreEqual("headset", DockIconCatalog.Resolve("\uE7F6"));
        Assert.AreEqual("hardware-chip", DockIconCatalog.Resolve("\uE88E"));
    }

    [TestMethod]
    public void Tint_ColorsAllInk_WithoutFillingTransparentOutlineInteriors()
    {
        foreach (var key in DockIconCatalog.Keys)
        foreach (var filled in new[] { false, true })
        {
            var original = XElement.Parse(DockIconSvg.ReadAsset(DockIconCatalog.AssetName(key, filled)));
            var tinted = XElement.Parse(DockIconSvg.Tint(original.ToString(), 0x8073A9FF));
            Assert.AreEqual(original.DescendantsAndSelf().Attributes("fill").Count(attribute => attribute.Value == "none"),
                tinted.DescendantsAndSelf().Attributes("fill").Count(attribute => attribute.Value == "none"), key);
            foreach (var attribute in tinted.DescendantsAndSelf().Attributes()
                .Where(attribute => attribute.Name.LocalName is "fill" or "stroke" && attribute.Value != "none"))
                Assert.AreEqual("#73A9FF", attribute.Value, key);
            Assert.AreEqual("0 0 512 512", tinted.Attribute("viewBox")?.Value);
            Assert.IsTrue(double.Parse(tinted.Attribute("opacity")!.Value, System.Globalization.CultureInfo.InvariantCulture) < 1);
        }
    }

    [TestMethod]
    public void DockXamlStaticGlyphs_AllResolve_WithoutChangingSettingsOrBattery()
    {
        var excluded = new HashSet<string> { "ModuleAvailabilityBadge.xaml", "ModuleSettingsCard.xaml" };
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Controls"), "*.xaml"))
        {
            if (excluded.Contains(Path.GetFileName(file))) continue;
            var document = XDocument.Load(file);
            Assert.IsFalse(document.Descendants().Any(element => element.Name.LocalName == "FontIcon"), file);
            foreach (var icon in document.Descendants().Where(element => element.Name.LocalName == "DockIcon"))
            {
                var glyph = icon.Attribute("Glyph")?.Value;
                if (string.IsNullOrWhiteSpace(glyph) || glyph.StartsWith('{')) continue;
                Assert.IsTrue(DockIconCatalog.Glyphs.ContainsKey(glyph), $"{file}: {glyph}");
            }
        }
        var battery = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Controls", "BatteryStatusIcon.xaml"));
        Assert.IsTrue(battery.Descendants().Any(element => element.Name.LocalName == "Polygon"));
        var settings = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Settings", "AppearanceSettingsPage.xaml"));
        Assert.IsTrue(settings.Descendants().Any(element => element.Name.LocalName == "FontIcon"));
    }
}
