// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Telemetry;

public sealed class MSTestTelemetryDataCollectorTests : TestContainer
{
    public void TrackDiscoveredMethod_CountsSchedulingAttributes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredMethod([
            new TestMethodAttribute(),
            new DependsOnAttribute("CreateCart"),
            new ResourceLockAttribute("database"),
        ]);

        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics["mstest.attribute_usage"].Should()
            .Be("""{"DependsOnAttribute":1,"ResourceLockAttribute":1,"TestMethodAttribute":1}""");
    }

    public void TrackDiscoveredMethod_AggregatesRepeatedDependsOnDeclarations()
    {
        var collector = new MSTestTelemetryDataCollector();

        // '[DependsOn]' allows multiple applications: fan-in is declared by repeating it.
        collector.TrackDiscoveredMethod([
            new TestMethodAttribute(),
            new DependsOnAttribute("AddItem"),
            new DependsOnAttribute("ApplyCoupon"),
        ]);

        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics["mstest.attribute_usage"].Should()
            .Be("""{"DependsOnAttribute":2,"TestMethodAttribute":1}""");
    }

    public void TrackDiscoveredClass_CountsSchedulingAttributes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredClass([
            new TestClassAttribute(),
            new DependsOnAttribute(typeof(MSTestTelemetryDataCollectorTests)),
            new ResourceLockAttribute("database"),
        ]);

        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics["mstest.attribute_usage"].Should()
            .Be("""{"DependsOnAttribute":1,"ResourceLockAttribute":1,"TestClassAttribute":1}""");
    }
}
#endif
