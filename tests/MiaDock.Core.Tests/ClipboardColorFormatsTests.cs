using MiaDock.Core.Clipboard;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ClipboardColorFormatsTests
{
    [TestMethod]
    public void TryFromHex_ProducesRgbAndHslCopyValues()
    {
        Assert.IsTrue(ClipboardColorFormats.TryFromHex("#4A90E2", out var formats));

        Assert.AreEqual("#4A90E2", formats.Hex);
        Assert.AreEqual("rgb(74, 144, 226)", formats.Rgb);
        Assert.AreEqual("74, 144, 226", formats.RgbChannelsDisplay);
        Assert.AreEqual("hsl(212, 72%, 59%)", formats.Hsl);
        Assert.AreEqual("212°, 72%, 59%", formats.HslDisplay);
    }

    [TestMethod]
    [DataRow("#FF0000", "hsl(0, 100%, 50%)")]
    [DataRow("#00FF00", "hsl(120, 100%, 50%)")]
    [DataRow("#0000FF", "hsl(240, 100%, 50%)")]
    [DataRow("#000000", "hsl(0, 0%, 0%)")]
    [DataRow("#FFFFFF", "hsl(0, 0%, 100%)")]
    public void PrimaryColors_RoundTripThroughHsl(string hex, string expectedHsl)
    {
        Assert.IsTrue(ClipboardColorFormats.TryFromHex(hex, out var formats));
        Assert.AreEqual(expectedHsl, formats.Hsl);
        Assert.IsTrue(ClipboardColorFormats.TryConvertHslToHex(formats.Hsl, out var converted));
        Assert.AreEqual(hex, converted);
    }

    [TestMethod]
    public void TryFromHex_RejectsInvalidValues()
    {
        Assert.IsFalse(ClipboardColorFormats.TryFromHex("#4A90E", out _));
        Assert.IsFalse(ClipboardColorFormats.TryFromHex("4A90E2", out _));
        Assert.IsFalse(ClipboardColorFormats.TryFromHex(null, out _));
    }

    [TestMethod]
    [DataRow("#1234", "#11223344", "rgba(17, 34, 51, 0.267)")]
    [DataRow("rgba(74, 144, 226, 0.5)", "#4A90E280", "rgba(74, 144, 226, 0.502)")]
    [DataRow("hsla(0, 100%, 50%, 25%)", "#FF000040", "rgba(255, 0, 0, 0.251)")]
    public void CssColors_PreserveAlpha(string source, string expectedHex, string expectedRgb)
    {
        Assert.IsTrue(ClipboardColorFormats.TryParse(source, out var formats));
        Assert.AreEqual(expectedHex, formats.Hex);
        Assert.AreEqual(expectedRgb, formats.Rgb);
    }
}
