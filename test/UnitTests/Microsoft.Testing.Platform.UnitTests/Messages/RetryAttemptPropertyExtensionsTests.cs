// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages.UnitTests;

[TestClass]
public sealed class RetryAttemptPropertyExtensionsTests
{
    [TestMethod]
    public void IsSupersededRetryAttempt_WithoutRetryAttemptProperty_ReturnsFalse()
        => Assert.IsFalse(CreateTestNode().IsSupersededRetryAttempt());

    [TestMethod]
    public void IsSupersededRetryAttempt_WithSupersededRetryAttemptProperty_ReturnsTrue()
        => Assert.IsTrue(CreateTestNode(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true)).IsSupersededRetryAttempt());

    [TestMethod]
    public void IsSupersededRetryAttempt_WithFinalRetryAttemptProperty_ReturnsFalse()
        => Assert.IsFalse(CreateTestNode(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false)).IsSupersededRetryAttempt());

    [TestMethod]
    public void IsSupersededRetryAttempt_WithUnrelatedProperty_ReturnsFalse()
        => Assert.IsFalse(CreateTestNode(new TestMetadataProperty("key", "value")).IsSupersededRetryAttempt());

    private static TestNode CreateTestNode(params IProperty[] properties)
        => new()
        {
            Uid = new TestNodeUid("test"),
            DisplayName = "test",
            Properties = new PropertyBag(properties),
        };

    [TestMethod]
    public void RetryAttempt_EqualityUsesAttemptAndSupersession()
    {
        RetryAttemptProperty original = new(3, isSuperseded: true);
        RetryAttemptProperty equal = new(3, isSuperseded: true);
        Assert.IsTrue(original.Equals(equal));
        Assert.IsTrue(original.Equals((object)equal));
        Assert.IsFalse(original.Equals(new RetryAttemptProperty(4, isSuperseded: true)));
        Assert.IsFalse(original.Equals(new RetryAttemptProperty(3, isSuperseded: false)));
        Assert.IsFalse(original.Equals((object?)null));
        Assert.IsFalse(original.Equals("3"));
        Assert.AreEqual(3, original.AttemptNumber);
        Assert.IsTrue(original.IsSuperseded);
        Assert.AreEqual("RetryAttemptProperty { AttemptNumber = 3, IsSuperseded = True }", original.ToString());
        Dictionary<RetryAttemptProperty, string> attempts = new() { [original] = "superseded third attempt" };
        Assert.AreEqual("superseded third attempt", attempts[equal]);
        Assert.AreEqual("RetryAttemptProperty { AttemptNumber = 2147483647, IsSuperseded = False }", new RetryAttemptProperty(int.MaxValue, false).ToString());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(int.MinValue)]
    public void RetryAttempt_NonPositiveAttemptIsRejected(int attempt)
        => Assert.AreEqual(
            "attemptNumber",
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RetryAttemptProperty(attempt, false)).ParamName);
}
