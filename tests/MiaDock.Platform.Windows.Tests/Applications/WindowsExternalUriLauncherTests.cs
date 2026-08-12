using MiaDock.Platform.Windows.Applications;

namespace MiaDock.Platform.Windows.Tests.Applications;

[TestClass]
public sealed class WindowsExternalUriLauncherTests
{
    [TestMethod]
    public async Task LaunchAsync_HttpsUri_DelegatesToWindowsClient()
    {
        var client = new FakeWindowsUriLauncherClient { Result = true };
        var launcher = new WindowsExternalUriLauncher(client);
        var uri = new Uri("https://mymiamo.net");

        var result = await launcher.LaunchAsync(uri);

        Assert.IsTrue(result);
        Assert.AreEqual(uri, client.LastUri);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    public async Task LaunchAsync_ClientReturnsFalse_ReportsFailureWithoutThrowing()
    {
        var client = new FakeWindowsUriLauncherClient { Result = false };
        var launcher = new WindowsExternalUriLauncher(client);

        var result = await launcher.LaunchAsync(new Uri("https://github.com/mymiamo/MiaDock"));

        Assert.IsFalse(result);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    public async Task LaunchAsync_ClientThrows_ReportsFailureWithoutThrowing()
    {
        var client = new FakeWindowsUriLauncherClient
        {
            Exception = new InvalidOperationException("No default browser")
        };
        var launcher = new WindowsExternalUriLauncher(client);

        var result = await launcher.LaunchAsync(new Uri("https://www.instagram.com/mymiamonet/"));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task LaunchAsync_NonHttpsUri_IsRejectedBeforeWindowsCall()
    {
        var client = new FakeWindowsUriLauncherClient { Result = true };
        var launcher = new WindowsExternalUriLauncher(client);

        var result = await launcher.LaunchAsync(new Uri("http://mymiamo.net"));

        Assert.IsFalse(result);
        Assert.AreEqual(0, client.CallCount);
    }

    private sealed class FakeWindowsUriLauncherClient : IWindowsUriLauncherClient
    {
        public bool Result { get; init; }
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }
        public Uri? LastUri { get; private set; }

        public Task<bool> LaunchAsync(
            Uri uri,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUri = uri;
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<bool>(Exception);
        }
    }
}
