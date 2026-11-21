// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AppModelCapabilitiesMutationTests : TestContainer
{
    public void EveryCapabilityFollowsTheApplicationModelTheBuildTargets()
    {
#if WIN_UI
        AppModelCapabilities.IsUIAppModel.Should().BeTrue();
        AppModelCapabilities.SupportsDeployment.Should().BeFalse();
        AppModelCapabilities.SupportsTelemetry.Should().BeFalse();
        AppModelCapabilities.SupportsAssertionFailureDiagnostics.Should().BeFalse();
        AppModelCapabilities.SupportsDiscoveryCache.Should().BeFalse();
#else
        AppModelCapabilities.IsUIAppModel.Should().BeFalse();
        AppModelCapabilities.SupportsDeployment.Should().BeTrue();
        AppModelCapabilities.SupportsTelemetry.Should().BeTrue();
        AppModelCapabilities.SupportsAssertionFailureDiagnostics.Should().BeTrue();
        AppModelCapabilities.SupportsDiscoveryCache.Should().BeTrue();
#endif
    }
}
