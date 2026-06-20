// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class JUnitReportTestResultCaptureTests
{
    [TestMethod]
    public void TryCapture_DoesNotWalkTerminalProperties_ForNonTerminalStates()
    {
        var bag = new PropertyBag(
            DiscoveredTestNodeStateProperty.CachedInstance,
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)),
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)));
        TestNode node = new() { Uid = "a", DisplayName = "x", Properties = bag };

        Assert.IsNull(TestResultCapture.TryCapture(new TestNodeUpdateMessage(new SessionUid("1"), node)));
    }

    [TestMethod]
    public void TryCapture_DuplicateSingletonProperty_Throws()
    {
        var bag = new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)),
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)));
        TestNode node = new() { Uid = "a", DisplayName = "x", Properties = bag };

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            TestResultCapture.TryCapture(new TestNodeUpdateMessage(new SessionUid("1"), node)));

        Assert.Contains(nameof(TimingProperty), ex.Message);
    }
}
