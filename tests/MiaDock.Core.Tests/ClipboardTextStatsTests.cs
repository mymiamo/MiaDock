using MiaDock.Core.Clipboard;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ClipboardTextStatsTests
{
    [TestMethod]
    public void FromText_CountsUnicodeWhitespaceSeparatedWords()
    {
        var stats = ClipboardTextStats.FromText("Merhaba  güzel\tdünya");

        Assert.AreEqual(3, stats.WordCount);
        Assert.AreEqual("Merhaba  güzel\tdünya".Length, stats.CharacterCount);
    }

    [TestMethod]
    public void FromText_EmptyString_HasZeroCounts()
    {
        var stats = ClipboardTextStats.FromText(string.Empty);

        Assert.AreEqual(0, stats.CharacterCount);
        Assert.AreEqual(0, stats.WordCount);
    }

    [TestMethod]
    public void TryCreate_PlainText_ReturnsStats()
    {
        var item = ClipboardPeekClassifier.ClassifyText("plain copied text", DateTimeOffset.UtcNow);
        var stats = ClipboardTextStats.TryCreate(item);

        Assert.IsNotNull(stats);
        Assert.AreEqual(3, stats.WordCount);
        Assert.AreEqual(17, stats.CharacterCount);
    }

    [TestMethod]
    public void TryCreate_SensitiveAndEmpty_HaveNoStats()
    {
        var sensitive = ClipboardPeekClassifier.ClassifyText("123456", DateTimeOffset.UtcNow);
        var empty = ClipboardPeekClassifier.ClassifyText("   ", DateTimeOffset.UtcNow);
        var color = ClipboardPeekClassifier.ClassifyText("#4A90E2", DateTimeOffset.UtcNow);

        Assert.IsNull(ClipboardTextStats.TryCreate(sensitive));
        Assert.IsNull(ClipboardTextStats.TryCreate(empty));
        Assert.IsNull(ClipboardTextStats.TryCreate(color));
        Assert.IsNull(ClipboardTextStats.TryCreate(null));
    }
}
