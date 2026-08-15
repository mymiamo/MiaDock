using MiaDock.Core.Clipboard;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ClipboardPeekClassifierTests
{
    [TestMethod]
    public void SensitiveText_IsMaskedAndCannotEnterHistory()
    {
        var item = ClipboardPeekClassifier.ClassifyText("sk-1234567890abcdefghijklmnop", DateTimeOffset.UtcNow);

        Assert.AreEqual(ClipboardPeekContentType.Sensitive, item.Type);
        Assert.IsTrue(item.IsSensitive);
        Assert.AreEqual("••••••••", item.DisplayText);
        Assert.IsTrue(item.IsRevealable);
        Assert.IsNull(item.RawText);
    }

    [TestMethod]
    [DataRow("https://mymiamo.net/bug", ClipboardPeekContentType.Url)]
    [DataRow("support@mymiamo.net", ClipboardPeekContentType.Email)]
    [DataRow("#4A90E2", ClipboardPeekContentType.Color)]
    [DataRow("plain copied text", ClipboardPeekContentType.PlainText)]
    public void Text_IsClassifiedDeterministically(string text, ClipboardPeekContentType expected)
    {
        var item = ClipboardPeekClassifier.ClassifyText(text, DateTimeOffset.UtcNow);

        Assert.AreEqual(expected, item.Type);
        Assert.IsFalse(item.IsSensitive);
    }

    [TestMethod]
    public void Options_DefaultToPrivateInMemoryHistory()
    {
        var options = ClipboardPeekOptions.Default;

        Assert.AreEqual(5, options.HistoryLimit);
        Assert.AreEqual(ClipboardPeekEventMode.SmartOnly, options.EventMode);
        Assert.IsTrue(options.ShowImageEvents);
    }

    [TestMethod]
    [DataRow("Bearer abcdefghijklmnopqrstuvwxyz012345")]
    [DataRow("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abcdefghijklmnopqrstuvwxyz")]
    [DataRow("123456")]
    [DataRow("4111 1111 1111 1111")]
    [DataRow("-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----")]
    [DataRow("ghp_abcdefghijklmnopqrstuvwxyz123456")]
    public void SupportedSecretFormats_AreSensitive(string text)
    {
        var item = ClipboardPeekClassifier.ClassifyText(text, DateTimeOffset.UtcNow);

        Assert.AreEqual(ClipboardPeekContentType.Sensitive, item.Type);
        Assert.IsNull(item.RawText);
    }

    [TestMethod]
    [DataRow("4111 1111 1111 1112")]
    [DataRow("12345")]
    [DataRow("abcdefghijklmnopqrstuvwxyzabcdef")]
    [DataRow("This-is-a-normal-sentence-with-words-123")]
    public void NonSecrets_AvoidHighEntropyFalsePositives(string text)
    {
        Assert.IsFalse(ClipboardPeekClassifier.IsSensitive(text));
    }

    [TestMethod]
    [DataRow("#abc", "#AABBCC")]
    [DataRow("#4a90e2", "#4A90E2")]
    [DataRow("rgb(74, 144, 226)", "#4A90E2")]
    [DataRow("hsl(0, 100%, 50%)", "#FF0000")]
    [DataRow("HSL(120, 100%, 50%)", "#00FF00")]
    [DataRow("hsl(240,100%,50%)", "#0000FF")]
    public void SupportedColors_AreNormalized(string text, string expected)
    {
        var item = ClipboardPeekClassifier.ClassifyText(text, DateTimeOffset.UtcNow);

        Assert.AreEqual(ClipboardPeekContentType.Color, item.Type);
        Assert.AreEqual(expected, item.ColorValue);
    }

    [TestMethod]
    public void ExistingFullPaths_AreClassified_AndMissingPathIsText()
    {
        var directory = Directory.CreateTempSubdirectory("miadock-clipboard-");
        try
        {
            var file = Path.Combine(directory.FullName, "sample.txt");
            File.WriteAllText(file, "test");

            Assert.AreEqual(ClipboardPeekContentType.Folder,
                ClipboardPeekClassifier.ClassifyText(directory.FullName, DateTimeOffset.UtcNow).Type);
            Assert.AreEqual(ClipboardPeekContentType.File,
                ClipboardPeekClassifier.ClassifyText(file, DateTimeOffset.UtcNow).Type);
            Assert.AreEqual(ClipboardPeekContentType.PlainText,
                ClipboardPeekClassifier.ClassifyText(Path.Combine(directory.FullName, "missing.txt"), DateTimeOffset.UtcNow).Type);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}
