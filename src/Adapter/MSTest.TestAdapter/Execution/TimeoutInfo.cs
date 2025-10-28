// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal readonly struct TimeoutInfo
{
    private TimeoutInfo(int timeout, bool cooperativeCancellation)
    {
        Timeout = timeout;
        CooperativeCancellation = cooperativeCancellation;
    }

    public int Timeout { get; }

    public bool CooperativeCancellation { get; }

    public static TimeoutInfo FromTimeoutAttribute(TimeoutAttribute timeoutAttribute)
        => new(
            timeoutAttribute.Timeout,
            timeoutAttribute.IsCooperativeCancellationSet
                ? timeoutAttribute.CooperativeCancellation
                : MSTestSettings.CurrentSettings.CooperativeCancellationTimeout);

    public static TimeoutInfo FromFixtureSettings(FixtureKind fixtureKind)
        => new(
            fixtureKind switch
            {
                FixtureKind.AssemblyInitialize => MSTestSettings.CurrentSettings.AssemblyInitializeTimeout,
                FixtureKind.AssemblyCleanup => MSTestSettings.CurrentSettings.AssemblyCleanupTimeout,
                FixtureKind.ClassInitialize => MSTestSettings.CurrentSettings.ClassInitializeTimeout,
                FixtureKind.ClassCleanup => MSTestSettings.CurrentSettings.ClassCleanupTimeout,
                FixtureKind.TestInitialize => MSTestSettings.CurrentSettings.TestInitializeTimeout,
                FixtureKind.TestCleanup => MSTestSettings.CurrentSettings.TestCleanupTimeout,
                _ => throw new NotSupportedException("Unsupported fixture kind " + fixtureKind),
            },
            MSTestSettings.CurrentSettings.CooperativeCancellationTimeout);

    public static TimeoutInfo FromTestTimeoutSettings()
        => new(
            MSTestSettings.CurrentSettings.TestTimeout > 0 ? MSTestSettings.CurrentSettings.TestTimeout : TestMethodInfo.TimeoutWhenNotSet,
            MSTestSettings.CurrentSettings.CooperativeCancellationTimeout);

    internal /* for testing purpose */ static TimeoutInfo FromTimeout(int timeout)
        => new(timeout, false);
}
