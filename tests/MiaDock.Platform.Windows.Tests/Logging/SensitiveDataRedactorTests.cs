using MiaDock.Platform.Windows.Logging;

namespace MiaDock.Platform.Windows.Tests.Logging;

[TestClass]
public sealed class SensitiveDataRedactorTests
{
    private readonly SensitiveDataRedactor _redactor = new();

    [TestMethod]
    public void Redact_RemovesWindowsUserAndFilePaths()
    {
        const string sensitive = @"Cannot read C:\Users\private-user\Music\secret-song.mp3";

        var result = _redactor.Redact(sensitive);
        var absolutePathResult = _redactor.Redact(@"Cannot read D:\Media\private-file.flac");

        Assert.DoesNotContain("private-user", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-song", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-file", absolutePathResult, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(result, "<user-path>");
        StringAssert.Contains(absolutePathResult, "<path>");
    }

    [TestMethod]
    public void SanitizeProperties_KeepsOnlyTechnicalAllowlist()
    {
        var result = _redactor.SanitizeProperties(new Dictionary<string, object?>
        {
            ["operation"] = "refresh",
            ["count"] = 3,
            ["isFullscreen"] = true,
            ["source"] = "Recovery",
            ["generation"] = 42,
            ["phase"] = "before-native-read",
            ["title"] = "Private song title",
            ["artist"] = "Private artist",
            ["filePath"] = @"C:\Users\someone\Music\private.mp3"
        });

        Assert.IsNotNull(result);
        Assert.AreEqual("refresh", result["operation"]);
        Assert.AreEqual("3", result["count"]);
        Assert.AreEqual("True", result["isFullscreen"]);
        Assert.AreEqual("Recovery", result["source"]);
        Assert.AreEqual("42", result["generation"]);
        Assert.AreEqual("before-native-read", result["phase"]);
        Assert.IsFalse(result.ContainsKey("title"));
        Assert.IsFalse(result.ContainsKey("artist"));
        Assert.IsFalse(result.ContainsKey("filePath"));
    }
}
