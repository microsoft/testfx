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
        try
        {
            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionPending();

            bool pendingStopAccepted = await MSTestGracefulStopTestExecutionCapability.Instance.TryStopTestExecutionAsync(CancellationToken.None);

            pendingStopAccepted.Should().BeTrue();
            PlatformServiceProvider.Instance.IsGracefulStopRequested.Should().BeTrue();

            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionPending();
            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionStarting();

            bool activeStopAccepted = await MSTestGracefulStopTestExecutionCapability.Instance.TryStopTestExecutionAsync(CancellationToken.None);

            activeStopAccepted.Should().BeTrue();

            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionCompleted();

            bool completedStopAccepted = await MSTestGracefulStopTestExecutionCapability.Instance.TryStopTestExecutionAsync(CancellationToken.None);

            completedStopAccepted.Should().BeFalse();
        }
        finally
        {
            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionPending();
            MSTestGracefulStopTestExecutionCapability.NotifyTestExecutionStarting();
        }
    }
}
