// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Runtime.ExceptionServices;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Telemetry;

public sealed class MSTestTelemetryDataCollectorMutationTests : TestContainer
{
    private const string PlatformOptOut = "TESTINGPLATFORM_TELEMETRY_OPTOUT";
    private const string CliOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
    private const string DiscoveryEvent = "dotnet/testingplatform/mstest/discovery";
    private const string ExecutionEvent = "dotnet/testingplatform/mstest/sessionexit";

    private readonly string? _previousPlatformOptOut = Environment.GetEnvironmentVariable(PlatformOptOut);
    private readonly string? _previousCliOptOut = Environment.GetEnvironmentVariable(CliOptOut);
    private readonly MSTestTelemetryDataCollector? _previousCollector = MSTestTelemetryDataCollector.Current;
    private readonly Dictionary<string, long> _previousAssertionCounts = TelemetryCollector.DrainAssertionCallCounts();

    public MSTestTelemetryDataCollectorMutationTests()
    {
        MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(null).GetAwaiter().GetResult();
        Environment.SetEnvironmentVariable(PlatformOptOut, null);
        Environment.SetEnvironmentVariable(CliOptOut, null);
        MSTestSettings.Reset();
    }

    public void MissingCollectorOrSenderReturnsBeforeBuildingMetricsOrInvokingTheSender()
    {
        // Without the early return the missing collector or sender is dereferenced inside the try block, where the
        // resulting NullReferenceException is swallowed. The state left behind is identical, so observe the first-chance
        // exceptions raised on this thread instead.
        int testThreadId = Environment.CurrentManagedThreadId;
        var nullReferences = new List<string>();
        string scenario = "control";
        EventHandler<FirstChanceExceptionEventArgs> handler = (_, e) =>
        {
            if (Environment.CurrentManagedThreadId == testThreadId && e.Exception is NullReferenceException)
            {
                nullReferences.Add($"{scenario}: {e.Exception.Message}");
            }
        };

        int sends = 0;
        Func<string, IDictionary<string, object>, Task> sender = (_, _) =>
        {
            sends++;
            return Task.CompletedTask;
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            Func<int>? missing = null;
            try
            {
                _ = missing!();
            }
            catch (NullReferenceException)
            {
            }

            nullReferences.Should().ContainSingle("the probe must observe a swallowed NullReferenceException");
            nullReferences.Clear();

            foreach ((bool hasCollector, bool hasSender) in new[] { (true, false), (false, true), (false, false) })
            {
                scenario = $"discovery (collector: {hasCollector}, sender: {hasSender})";
                MSTestTelemetryDataCollector.Current = hasCollector ? new() : null;
                MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(hasSender ? sender : null).GetAwaiter().GetResult();

                scenario = $"execution (collector: {hasCollector}, sender: {hasSender})";
                MSTestTelemetryDataCollector.Current = hasCollector ? new() : null;
                MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(hasSender ? sender : null).GetAwaiter().GetResult();
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        nullReferences.Should().BeEmpty();
        sends.Should().Be(0);
        MSTestTelemetryDataCollector.Current.Should().BeNull();
    }

    public void ExecutionTelemetryIsNotSentWhenThereIsNothingToReport()
    {
        var sent = new List<(string EventName, Dictionary<string, object> Metrics)>();
        Func<string, IDictionary<string, object>, Task> sender = (eventName, metrics) =>
        {
            sent.Add((eventName, new Dictionary<string, object>(metrics)));
            return Task.CompletedTask;
        };

        // The discovery event carries the settings and the custom types, so the session exit event has nothing left to add.
        _ = MSTestTelemetryDataCollector.EnsureInitialized();
        MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(sender).GetAwaiter().GetResult();
        _ = MSTestTelemetryDataCollector.EnsureInitialized();
        MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(sender).GetAwaiter().GetResult();

        MSTestTelemetryDataCollector.Current.Should().BeNull();
        sent.Should().ContainSingle(entry => entry.EventName == DiscoveryEvent);
        sent.Should().NotContain(entry => entry.EventName == ExecutionEvent && entry.Metrics.Count == 0, "an empty session exit event must not be sent");

        // An assertion call is something to report, even though the discovery event was already sent.
        _ = MSTestTelemetryDataCollector.EnsureInitialized();
        MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(sender).GetAwaiter().GetResult();
        _ = MSTestTelemetryDataCollector.EnsureInitialized();
        TelemetryCollector.TrackAssertionCall("Adapter.X", isEnabled: true);
        MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(sender).GetAwaiter().GetResult();

        Dictionary<string, object> executionMetrics = sent.Last(entry => entry.EventName == ExecutionEvent).Metrics;
        executionMetrics.Should().ContainKey("mstest.assertion_usage");
        executionMetrics.Keys.Should().NotContain(key => key.StartsWith("mstest.setting.", StringComparison.Ordinal));
    }

    public void TrackDiscoveredMethodReportsCustomTestClassAttributeTypes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredMethod([new CustomClassAttribute()]);

        AssertOnlyCustomTestClassTypeIsReported(collector);
    }

    public void TrackDiscoveredClassReportsCustomTestClassAttributeTypes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredClass([new CustomClassAttribute()]);

        AssertOnlyCustomTestClassTypeIsReported(collector);
    }

#if NET
    public void EnsureInitializedReturnsThePublishedCollectorWithoutAllocatingAnother()
    {
        var published = MSTestTelemetryDataCollector.EnsureInitialized();
        _ = MSTestTelemetryDataCollector.EnsureInitialized();
        _ = MSTestTelemetryDataCollector.EnsureInitialized();

        long controlBefore = GC.GetAllocatedBytesForCurrentThread();
        byte[] control = new byte[10_000];
        long controlAllocated = GC.GetAllocatedBytesForCurrentThread() - controlBefore;
        GC.KeepAlive(control);
        controlAllocated.Should().BeGreaterThanOrEqualTo(10_000, "the allocation counter must observe allocations on this thread");

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            _ = MSTestTelemetryDataCollector.EnsureInitialized();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // A throwaway collector per call would allocate well over 100 KB here.
        allocated.Should().BeLessThan(16 * 1024);
        MSTestTelemetryDataCollector.EnsureInitialized().Should().BeSameAs(published);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(null).GetAwaiter().GetResult();
        }
        finally
        {
            try
            {
                Environment.SetEnvironmentVariable(PlatformOptOut, _previousPlatformOptOut);
                Environment.SetEnvironmentVariable(CliOptOut, _previousCliOptOut);
                MSTestTelemetryDataCollector.Current = _previousCollector;
                foreach (KeyValuePair<string, long> previous in _previousAssertionCounts)
                {
                    for (long index = 0; index < previous.Value; index++)
                    {
                        TelemetryCollector.TrackAssertionCall(previous.Key, isEnabled: true);
                    }
                }

                MSTestSettings.Reset();
                MSTestSettingsProvider.Reset();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }

    private static void AssertOnlyCustomTestClassTypeIsReported(MSTestTelemetryDataCollector collector)
    {
        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics.Should().ContainKey("mstest.custom_test_class_types");
        metrics["mstest.custom_test_class_types"].Should().BeOfType<string>().Which.Should().MatchRegex("""^\["[0-9A-F]{64}"\]$""");
        metrics.Should().NotContainKey("mstest.custom_test_method_types");
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class CustomClassAttribute : TestClassAttribute;
}
#endif
