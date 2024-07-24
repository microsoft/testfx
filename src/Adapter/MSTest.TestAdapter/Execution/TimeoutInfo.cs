// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal readonly struct TimeoutInfo
{
    public TimeoutInfo(TimeoutAttribute timeoutAttribute)
    {
        Timeout = timeoutAttribute.Timeout;
        CooperativeCancellation = timeoutAttribute.IsCooperativeCancellationSet
            ? timeoutAttribute.CooperativeCancellation
            : MSTestSettings.CurrentSettings.CooperativeCancellationTimeout;
    }

    public TimeoutInfo(FixtureKind fixtureKind)
    {
        Timeout = fixtureKind switch
        {
            FixtureKind.AssemblyInitialize => MSTestSettings.CurrentSettings.AssemblyInitializeTimeout,
            FixtureKind.AssemblyCleanup => MSTestSettings.CurrentSettings.AssemblyCleanupTimeout,
            FixtureKind.ClassInitialize => MSTestSettings.CurrentSettings.ClassInitializeTimeout,
            FixtureKind.ClassCleanup => MSTestSettings.CurrentSettings.ClassCleanupTimeout,
            FixtureKind.TestInitialize => MSTestSettings.CurrentSettings.TestInitializeTimeout,
            FixtureKind.TestCleanup => MSTestSettings.CurrentSettings.TestCleanupTimeout,
            _ => throw new NotSupportedException("Unsupported fixture kind " + fixtureKind),
        };
        CooperativeCancellation = MSTestSettings.CurrentSettings.CooperativeCancellationTimeout;
    }

    public int Timeout { get; }

    public bool CooperativeCancellation { get; }
}
