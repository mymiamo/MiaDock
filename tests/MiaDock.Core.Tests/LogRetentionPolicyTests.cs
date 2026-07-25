using MiaDock.Core.Logging;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class LogRetentionPolicyTests
{
    [TestMethod]
    public void DefaultPolicy_IsValidAndBounded()
    {
        LogRetentionPolicy.Default.Validate();

        Assert.AreEqual(2 * 1024 * 1024, LogRetentionPolicy.Default.MaximumFileBytes);
        Assert.AreEqual(10, LogRetentionPolicy.Default.MaximumFiles);
        Assert.AreEqual(TimeSpan.FromDays(14), LogRetentionPolicy.Default.MaximumAge);
    }

    [TestMethod]
    [DataRow(63 * 1024, 10, 14)]
    [DataRow(2 * 1024 * 1024, 0, 14)]
    [DataRow(2 * 1024 * 1024, 101, 14)]
    [DataRow(2 * 1024 * 1024, 10, 0)]
    public void InvalidPolicy_Throws(long maximumBytes, int maximumFiles, int maximumDays)
    {
        var policy = new LogRetentionPolicy(maximumBytes, maximumFiles, TimeSpan.FromDays(maximumDays));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(policy.Validate);
    }
}
