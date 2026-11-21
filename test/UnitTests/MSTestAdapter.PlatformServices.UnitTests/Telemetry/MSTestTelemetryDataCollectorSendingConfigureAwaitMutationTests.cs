// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Telemetry;

public sealed class MSTestTelemetryDataCollectorSendingConfigureAwaitMutationTests : TestContainer
{
    private const string PlatformOptOut = "TESTINGPLATFORM_TELEMETRY_OPTOUT";
    private const string CliOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly string? _previousPlatformOptOut = Environment.GetEnvironmentVariable(PlatformOptOut);
    private readonly string? _previousCliOptOut = Environment.GetEnvironmentVariable(CliOptOut);
    private readonly MSTestTelemetryDataCollector? _previousCollector = MSTestTelemetryDataCollector.Current;
    private readonly Dictionary<string, long> _previousAssertionCounts = TelemetryCollector.DrainAssertionCallCounts();

    public MSTestTelemetryDataCollectorSendingConfigureAwaitMutationTests()
    {
        MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(null).GetAwaiter().GetResult();
        Environment.SetEnvironmentVariable(PlatformOptOut, null);
        Environment.SetEnvironmentVariable(CliOptOut, null);
        MSTestSettings.Reset();
    }

    public async Task SendDiscoveryTelemetryAndResetAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var collector = MSTestTelemetryDataCollector.EnsureInitialized();
        collector.ConfigurationSource = "runsettings";
        collector.TrackDiscoveredMethod([new TestMethodAttribute()]);
        string? sentEvent = null;

        // The sender stays pending, so the await on it suspends while the probe's context is current.
        Task sending = probe.Run(() => MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync((name, _) =>
        {
            sentEvent = name;
            return gate.Task;
        }));
        sending.IsCompleted.Should().BeFalse();
        gate.SetResult(true);
        await WaitFor(sending);

        sentEvent.Should().Be("dotnet/testingplatform/mstest/discovery");
        MSTestTelemetryDataCollector.Current.Should().BeNull();
        probe.PostCount.Should().Be(0, "sending the discovery telemetry must not capture the caller's synchronization context");
    }

    public async Task SendExecutionTelemetryAndResetAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "runsettings";
        string? sentEvent = null;

        // The sender stays pending, so the await on it suspends while the probe's context is current.
        Task sending = probe.Run(() => MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, _) =>
        {
            sentEvent = name;
            return gate.Task;
        }));
        sending.IsCompleted.Should().BeFalse();
        gate.SetResult(true);
        await WaitFor(sending);

        sentEvent.Should().Be("dotnet/testingplatform/mstest/sessionexit");
        MSTestTelemetryDataCollector.Current.Should().BeNull();
        probe.PostCount.Should().Be(0, "sending the execution telemetry must not capture the caller's synchronization context");
    }

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

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }
}
#endif
