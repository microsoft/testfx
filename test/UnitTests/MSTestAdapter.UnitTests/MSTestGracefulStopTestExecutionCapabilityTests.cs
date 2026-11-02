// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestGracefulStopTestExecutionCapabilityTests : TestContainer
{
    public async Task TryStopTestExecutionAsync_DistinguishesPendingActiveAndCompletedExecution()
    {
        var capability = MSTestGracefulStopTestExecutionCapability.Create();
        try
        {
            capability.NotifyTestExecutionPending();

            bool pendingStopAccepted = await capability.TryStopTestExecutionAsync(CancellationToken.None);

            pendingStopAccepted.Should().BeTrue();
            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            capability.NotifyTestExecutionCompleted();
            capability = MSTestGracefulStopTestExecutionCapability.Create();
            capability.NotifyTestExecutionPending();
            capability.NotifyTestExecutionStarting();

            bool activeStopAccepted = await capability.TryStopTestExecutionAsync(CancellationToken.None);

            activeStopAccepted.Should().BeTrue();

            capability.NotifyTestExecutionCompleted();

            bool completedStopAccepted = await capability.TryStopTestExecutionAsync(CancellationToken.None);

            completedStopAccepted.Should().BeFalse();
        }
        finally
        {
            capability.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }

    public async Task DiscoveryCapabilityCannotClearAnActiveRunsStopRequest()
    {
        var runCapability = MSTestGracefulStopTestExecutionCapability.Create();
        var discoveryCapability = MSTestGracefulStopTestExecutionCapability.Create();

        try
        {
            runCapability.NotifyTestExecutionPending();
            runCapability.NotifyTestExecutionStarting();
            (await runCapability.TryStopTestExecutionAsync(CancellationToken.None)).Should().BeTrue();

            discoveryCapability.NotifyTestExecutionPending();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();
            discoveryCapability.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();
        }
        finally
        {
            runCapability.NotifyTestExecutionCompleted();
            discoveryCapability.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }

    public async Task LegacyStopTestExecutionAsync_DoesNotReassertStopAfterExecutionCompleted()
    {
        var capability = MSTestGracefulStopTestExecutionCapability.Create();
        try
        {
            capability.NotifyTestExecutionPending();
            capability.NotifyTestExecutionStarting();
            capability.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;

            await capability.StopTestExecutionAsync(CancellationToken.None);

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeFalse();
        }
        finally
        {
            capability.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }

    public async Task OverlappingRunCannotClearAnActiveRunsStopRequest()
    {
        var firstRun = MSTestGracefulStopTestExecutionCapability.Create();
        var overlappingRun = MSTestGracefulStopTestExecutionCapability.Create();
        var nextRun = MSTestGracefulStopTestExecutionCapability.Create();

        try
        {
            firstRun.NotifyTestExecutionPending();
            firstRun.NotifyTestExecutionStarting();
            (await firstRun.TryStopTestExecutionAsync(CancellationToken.None)).Should().BeTrue();

            overlappingRun.NotifyTestExecutionPending();
            overlappingRun.NotifyTestExecutionStarting();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            firstRun.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            overlappingRun.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionPending();
            nextRun.NotifyTestExecutionStarting();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeFalse();
        }
        finally
        {
            firstRun.NotifyTestExecutionCompleted();
            overlappingRun.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }

    public async Task OverlappingRunCannotClearAPendingRunsStopRequest()
    {
        var pendingRun = MSTestGracefulStopTestExecutionCapability.Create();
        var overlappingRun = MSTestGracefulStopTestExecutionCapability.Create();
        var nextRun = MSTestGracefulStopTestExecutionCapability.Create();

        try
        {
            pendingRun.NotifyTestExecutionPending();
            (await pendingRun.TryStopTestExecutionAsync(CancellationToken.None)).Should().BeTrue();

            overlappingRun.NotifyTestExecutionPending();
            overlappingRun.NotifyTestExecutionStarting();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            pendingRun.NotifyTestExecutionStarting();
            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            pendingRun.NotifyTestExecutionCompleted();
            overlappingRun.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionPending();
            nextRun.NotifyTestExecutionStarting();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeFalse();
        }
        finally
        {
            pendingRun.NotifyTestExecutionCompleted();
            overlappingRun.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }

    public async Task CompletedDiscoveryReleasesPendingStopOwnershipBeforeNextRun()
    {
        var discovery = MSTestGracefulStopTestExecutionCapability.Create();
        var nextRun = MSTestGracefulStopTestExecutionCapability.Create();

        try
        {
            discovery.NotifyTestExecutionPending();
            (await discovery.TryStopTestExecutionAsync(CancellationToken.None)).Should().BeTrue();

            discovery.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionPending();
            nextRun.NotifyTestExecutionStarting();

            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeFalse();
        }
        finally
        {
            discovery.NotifyTestExecutionCompleted();
            nextRun.NotifyTestExecutionCompleted();
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
        }
    }
}
