// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class SlowTestThresholdStateTests
{
    [TestMethod]
    public void IsDue_UsesExponentialBackoff()
    {
        var state = new SlowTestThresholdState(TimeSpan.FromSeconds(60));

        Assert.IsFalse(state.IsDue(TimeSpan.FromSeconds(59)));
        Assert.IsTrue(state.IsDue(TimeSpan.FromSeconds(60)));
        Assert.IsFalse(state.IsDue(TimeSpan.FromSeconds(119)));
        Assert.IsTrue(state.IsDue(TimeSpan.FromSeconds(120)));
    }

    [TestMethod]
    public void IsDue_WhenPollIsDelayed_SkipsObsoleteThresholds()
    {
        var state = new SlowTestThresholdState(TimeSpan.FromSeconds(60));

        Assert.IsTrue(state.IsDue(TimeSpan.FromSeconds(300)));
        Assert.IsFalse(state.IsDue(TimeSpan.FromSeconds(300)));
        Assert.IsFalse(state.IsDue(TimeSpan.FromSeconds(479)));
        Assert.IsTrue(state.IsDue(TimeSpan.FromSeconds(480)));
    }

    [TestMethod]
    public void IsDue_WhenThresholdIsNotPositive_IsDisabled()
    {
        Assert.IsFalse(new SlowTestThresholdState(TimeSpan.Zero).IsDue(TimeSpan.MaxValue));
        Assert.IsFalse(new SlowTestThresholdState(TimeSpan.FromSeconds(-1)).IsDue(TimeSpan.MaxValue));
    }

    [TestMethod]
    public void IsDue_WhenNextThresholdWouldOverflow_SaturatesAtMaximum()
    {
        var threshold = TimeSpan.FromTicks((TimeSpan.MaxValue.Ticks / 2) + 1);
        var state = new SlowTestThresholdState(threshold);

        Assert.IsTrue(state.IsDue(threshold));
        Assert.IsFalse(state.IsDue(TimeSpan.FromTicks(TimeSpan.MaxValue.Ticks - 1)));
    }
}
