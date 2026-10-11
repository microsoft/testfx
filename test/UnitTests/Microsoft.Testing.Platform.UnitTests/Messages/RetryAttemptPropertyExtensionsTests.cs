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
}
