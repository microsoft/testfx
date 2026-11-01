// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
#if NETFRAMEWORK
using System.Security;
using System.Security.Permissions;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Telemetry;

public sealed class MSTestTelemetryDataCollectorContinuationTests : TestContainer
{
    private const string PlatformOptOut = "TESTINGPLATFORM_TELEMETRY_OPTOUT";
    private const string CliOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
    private const string DiscoveryEvent = "dotnet/testingplatform/mstest/discovery";
    private const string ExecutionEvent = "dotnet/testingplatform/mstest/sessionexit";
    private readonly string? _previousPlatformOptOut = Environment.GetEnvironmentVariable(PlatformOptOut);
    private readonly string? _previousCliOptOut = Environment.GetEnvironmentVariable(CliOptOut);
    private readonly MSTestTelemetryDataCollector? _previousCollector = MSTestTelemetryDataCollector.Current;
    private readonly Dictionary<string, long> _previousAssertionCounts = TelemetryCollector.DrainAssertionCallCounts();

    public MSTestTelemetryDataCollectorContinuationTests()
    {
        MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(null).GetAwaiter().GetResult();
        Environment.SetEnvironmentVariable(PlatformOptOut, null);
        Environment.SetEnvironmentVariable(CliOptOut, null);
        MSTestSettings.Reset();
    }

    public void MethodDiscoveryCountsEveryKnownAttributeAndIgnoresUnrelatedOnes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredMethod(
        [
            new TestMethodAttribute(),
            new TestClassAttribute(),
            new DataRowAttribute(42),
            new DynamicDataAttribute("Rows"),
            new TimeoutAttribute(100),
            new IgnoreAttribute("discovery input, not a skipped test"),
            new DoNotParallelizeAttribute(),
            new ResourceLockAttribute("database"),
            new DependsOnAttribute("Prepare"),
            new RetryAttribute(2),
            new OSConditionAttribute(OperatingSystems.Linux),
            new TestCategoryAttribute("unit"),
            new DeploymentItemAttribute("local-data.txt"),
            new ObsoleteAttribute("not an MSTest attribute"),
        ]);
        collector.TrackDiscoveredMethod([new TimeoutAttribute(200), new ObsoleteAttribute()]);

        collector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["ConditionBaseAttribute"] = 1,
            ["DataRowAttribute"] = 1,
            ["DependsOnAttribute"] = 1,
            ["DeploymentItemAttribute"] = 1,
            ["DoNotParallelizeAttribute"] = 1,
            ["DynamicDataAttribute"] = 1,
            ["IgnoreAttribute"] = 1,
            ["ResourceLockAttribute"] = 1,
            ["RetryBaseAttribute"] = 1,
            ["TestCategoryAttribute"] = 1,
            ["TestClassAttribute"] = 1,
            ["TestMethodAttribute"] = 1,
            ["TimeoutAttribute"] = 2,
        });
        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();
        metrics.Should().NotContainKey("mstest.custom_test_method_types").And.NotContainKey("mstest.custom_test_class_types");
    }

    public void ClassDiscoveryCountsOnlyClassSchedulingAttributes()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredClass(
        [
            new TestClassAttribute(),
            new ParallelizeAttribute { Workers = 4 },
            new DoNotParallelizeAttribute(),
            new ResourceLockAttribute("database"),
            new DependsOnAttribute(typeof(MSTestTelemetryDataCollectorContinuationTests)),
            new DataRowAttribute(42),
            new TestMethodAttribute(),
            new ObsoleteAttribute(),
        ]);
        collector.TrackDiscoveredClass([new ParallelizeAttribute(), new ObsoleteAttribute()]);

        collector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["DependsOnAttribute"] = 1,
            ["DoNotParallelizeAttribute"] = 1,
            ["ParallelizeAttribute"] = 2,
            ["ResourceLockAttribute"] = 1,
            ["TestClassAttribute"] = 1,
        });
    }

    public void DerivedAttributesAreCountedByBaseTypeAndOnlyDistinctHashesAreEmitted()
    {
        var collector = new MSTestTelemetryDataCollector();

        collector.TrackDiscoveredMethod(
        [
            new OtherInheritedMethodAttribute(),
            new InheritedMethodAttribute(),
            new InheritedMethodAttribute(),
            new InheritedClassAttribute(),
        ]);
        collector.TrackDiscoveredClass([new InheritedClassAttribute(), new InheritedClassAttribute(), new TestClassAttribute()]);

        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics["mstest.attribute_usage"].Should().Be("""{"TestClassAttribute":4,"TestMethodAttribute":3}""");
        metrics["mstest.custom_test_method_types"].Should().Be(
            """["7DC084D0218CF60BA17A61E41ABF36DC8D6D661A12A402A7E725E46FEBE36F4B","9602953A5543C1C89A8F6C421CC35F59F83277B4943F68F4044E2E460377269A"]""");
        metrics["mstest.custom_test_class_types"].Should().Be(
            """["C35A15AFD5BA25F65E2F2A1C9548ADCE38F3293F71A5E55673000F99B9D1C1A9"]""");
        string payload = string.Join("|", metrics.Values);
        payload.Should().NotContain(nameof(InheritedMethodAttribute)).And.NotContain(nameof(OtherInheritedMethodAttribute))
            .And.NotContain(nameof(InheritedClassAttribute)).And.NotContain(typeof(MSTestTelemetryDataCollectorContinuationTests).FullName!);
    }

    public void MergedAttributeCountsAreAdditiveAndSnapshotsAreIndependent()
    {
        var collector = new MSTestTelemetryDataCollector();
        collector.AddDiscoveryAttributeCounts(new Dictionary<string, long> { ["z"] = 2, ["A"] = 3 });
        collector.AddDiscoveryAttributeCounts(new Dictionary<string, long> { ["z"] = 5, ["other"] = 7 });

        Dictionary<string, long> snapshot = collector.GetDiscoveryAttributeCounts();

        snapshot.Should().BeEquivalentTo(new Dictionary<string, long> { ["z"] = 7, ["A"] = 3, ["other"] = 7 });
        snapshot["z"] = 99;
        snapshot.Remove("A");
        snapshot["outside"] = 11;
        collector.AddDiscoveryAttributeCounts(new Dictionary<string, long> { ["other"] = 1 });
        collector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(new Dictionary<string, long> { ["z"] = 7, ["A"] = 3, ["other"] = 8 });
        snapshot["other"].Should().Be(7);
    }

    public void MetricsSerializeOrdinalKeysEscapesAndInvariantSignedCounts()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
            culture.NumberFormat.NegativeSign = "minus";
            CultureInfo.CurrentCulture = culture;
            var counts = new Dictionary<string, long>
            {
                ["z"] = 3,
                ["a-é"] = 2,
                ["A\"\\\b\f\n\r\t\0\u0001\u0085"] = -17,
            };
            const string Expected = """{"A\"\\\b\f\n\r\t\u0000\u0001\u0085":-17,"a-é":2,"z":3}""";
            var collector = new MSTestTelemetryDataCollector();
            collector.AddDiscoveryAttributeCounts(counts);

            Dictionary<string, object> discovery = collector.BuildDiscoveryMetrics();
            Dictionary<string, object> execution = collector.BuildExecutionMetrics(counts, includeDiscoveryPayload: false);

            discovery["mstest.attribute_usage"].Should().Be(Expected);
            execution.Should().ContainSingle();
            execution["mstest.assertion_usage"].Should().Be(Expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    public void DiscoveryMetricsProjectEffectiveSettingsWithoutRawSeed()
    {
        MSTestSettings.PopulateSettings(
            """
            <RunSettings>
              <RunConfiguration><DisableParallelization>true</DisableParallelization></RunConfiguration>
              <MSTest>
                <Parallelize><Workers>5</Workers><Scope>MethodLevel</Scope></Parallelize>
                <TestTimeout>11</TestTimeout>
                <AssemblyInitializeTimeout>13</AssemblyInitializeTimeout>
                <AssemblyCleanupTimeout>17</AssemblyCleanupTimeout>
                <ClassInitializeTimeout>19</ClassInitializeTimeout>
                <ClassCleanupTimeout>23</ClassCleanupTimeout>
                <TestInitializeTimeout>29</TestInitializeTimeout>
                <TestCleanupTimeout>31</TestCleanupTimeout>
                <GlobalTestInitializeTimeout>37</GlobalTestInitializeTimeout>
                <GlobalTestCleanupTimeout>41</GlobalTestCleanupTimeout>
                <CooperativeCancellationTimeout>true</CooperativeCancellationTimeout>
                <MapInconclusiveToFailed>true</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>false</MapNotRunnableToFailed>
                <TreatDiscoveryWarningsAsErrors>false</TreatDiscoveryWarningsAsErrors>
                <ConsiderEmptyDataSourceAsInconclusive>true</ConsiderEmptyDataSourceAsInconclusive>
                <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
                <RandomizeTestOrder>true</RandomizeTestOrder>
                <RandomTestOrderSeed>-2147483648</RandomTestOrderSeed>
                <CaptureTraceOutput>None</CaptureTraceOutput>
              </MSTest>
            </RunSettings>
            """, null, null);
        var collector = new MSTestTelemetryDataCollector();

        Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();

        metrics.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["mstest.setting.parallelization_enabled"] = "false",
            ["mstest.setting.parallelization_scope"] = "MethodLevel",
            ["mstest.setting.parallelization_workers"] = 5d,
            ["mstest.setting.test_timeout"] = 11d,
            ["mstest.setting.assembly_initialize_timeout"] = 13d,
            ["mstest.setting.assembly_cleanup_timeout"] = 17d,
            ["mstest.setting.class_initialize_timeout"] = 19d,
            ["mstest.setting.class_cleanup_timeout"] = 23d,
            ["mstest.setting.test_initialize_timeout"] = 29d,
            ["mstest.setting.test_cleanup_timeout"] = 31d,
            ["mstest.setting.global_test_initialize_timeout"] = 37d,
            ["mstest.setting.global_test_cleanup_timeout"] = 41d,
            ["mstest.setting.cooperative_cancellation"] = "true",
            ["mstest.setting.map_inconclusive_to_failed"] = "true",
            ["mstest.setting.map_not_runnable_to_failed"] = "false",
            ["mstest.setting.treat_discovery_warnings_as_errors"] = "false",
            ["mstest.setting.consider_empty_data_source_as_inconclusive"] = "true",
            ["mstest.setting.order_tests_by_name"] = "true",
            ["mstest.setting.randomize_test_order"] = "true",
            ["mstest.setting.random_test_order_seed_provided"] = "true",
            ["mstest.setting.capture_debug_traces"] = "false",
            ["mstest.setting.output_capture_mode"] = "None",
        });
        metrics.Values.Should().OnlyContain(value => value is string || value is double);
    }

    public void ExecutionMetricsIncludeDiscoveryOnlyWhenRequested()
    {
        var collector = new MSTestTelemetryDataCollector { ConfigurationSource = "runsettings" };
        collector.TrackDiscoveredMethod([new TestMethodAttribute()]);

        collector.BuildExecutionMetrics([], includeDiscoveryPayload: false).Should().BeEmpty();
        Dictionary<string, object> executionOnly = collector.BuildExecutionMetrics(new() { ["Assert.Equal"] = 2 }, includeDiscoveryPayload: false);
        Dictionary<string, object> combined = collector.BuildExecutionMetrics(new() { ["Assert.Equal"] = 2 }, includeDiscoveryPayload: true);
        Dictionary<string, object> discoveryOnly = collector.BuildExecutionMetrics([], includeDiscoveryPayload: true);

        executionOnly.Should().BeEquivalentTo(new Dictionary<string, object> { ["mstest.assertion_usage"] = """{"Assert.Equal":2}""" });
        combined["mstest.config_source"].Should().Be("runsettings");
        combined["mstest.attribute_usage"].Should().Be("""{"TestMethodAttribute":1}""");
        combined["mstest.assertion_usage"].Should().Be("""{"Assert.Equal":2}""");
        discoveryOnly["mstest.config_source"].Should().Be("runsettings");
        discoveryOnly["mstest.attribute_usage"].Should().Be("""{"TestMethodAttribute":1}""");
        discoveryOnly.Should().NotContainKey("mstest.assertion_usage");
    }

    public async Task EnsureInitializedPublishesOneCollectorForAllCallers()
    {
        using var start = new ManualResetEventSlim();
        Task<MSTestTelemetryDataCollector>[] calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return MSTestTelemetryDataCollector.EnsureInitialized();
        })).ToArray();

        start.Set();
        MSTestTelemetryDataCollector[] collectors = await Task.WhenAll(calls);

        MSTestTelemetryDataCollector published = MSTestTelemetryDataCollector.Current!;
        collectors.Should().OnlyContain(collector => ReferenceEquals(collector, published));
        MSTestTelemetryDataCollector.EnsureInitialized().Should().BeSameAs(published);
        published.ConfigurationSource = "shared-session";
        collectors.Should().OnlyContain(collector => collector.ConfigurationSource == "shared-session");
    }

    public void OptOutRecognizesOnlyExactTruthySpellingsInEitherVariable()
    {
        foreach (string variable in new[] { PlatformOptOut, CliOptOut })
        {
            foreach ((string? value, bool expected) in new (string?, bool)[]
            {
                ("1", true),
                ("true", true),
                ("True", false),
                ("TRUE", false),
                (" true ", false),
                ("0", false),
                (string.Empty, false),
                (null, false),
            })
            {
                Environment.SetEnvironmentVariable(PlatformOptOut, null);
                Environment.SetEnvironmentVariable(CliOptOut, null);
                Environment.SetEnvironmentVariable(variable, value);

                MSTestTelemetryDataCollector.IsTelemetryOptedOut().Should().Be(expected);
            }
        }

        Environment.SetEnvironmentVariable(PlatformOptOut, "0");
        Environment.SetEnvironmentVariable(CliOptOut, "true");
        MSTestTelemetryDataCollector.IsTelemetryOptedOut().Should().BeTrue();
        Environment.SetEnvironmentVariable(PlatformOptOut, "true");
        Environment.SetEnvironmentVariable(CliOptOut, "0");
        MSTestTelemetryDataCollector.IsTelemetryOptedOut().Should().BeTrue();
    }

    public async Task DiscoverySendAwaitsCompletionAndSuppressesDiscoveryUntilExecutionReset()
    {
        var collector = MSTestTelemetryDataCollector.EnsureInitialized();
        collector.ConfigurationSource = "runsettings";
        collector.TrackDiscoveredMethod([new TestMethodAttribute()]);
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<(string Name, Dictionary<string, object> Metrics)>();
        Task sending = MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync((name, metrics) =>
        {
            events.Add((name, [with(metrics)]));
            return completion.Task;
        });

        sending.IsCompleted.Should().BeFalse();
        MSTestTelemetryDataCollector.Current.Should().BeSameAs(collector);
        events.Should().ContainSingle();
        events[0].Name.Should().Be(DiscoveryEvent);
        events[0].Metrics["mstest.config_source"].Should().Be("runsettings");
        events[0].Metrics["mstest.attribute_usage"].Should().Be("""{"TestMethodAttribute":1}""");
        completion.SetResult(0);
        await sending;
        MSTestTelemetryDataCollector.Current.Should().BeNull();

        collector = MSTestTelemetryDataCollector.EnsureInitialized();
        collector.ConfigurationSource = "must-not-resend";
        collector.TrackDiscoveredClass([new TestClassAttribute()]);
        TelemetryCollector.TrackAssertionCall("Adapter.Context.Equal", isEnabled: true);
        TelemetryCollector.TrackAssertionCall("Adapter.Context.Equal", isEnabled: true);
        await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
        {
            events.Add((name, [with(metrics)]));
            return Task.CompletedTask;
        });

        events.Should().HaveCount(2);
        events[1].Name.Should().Be(ExecutionEvent);
        events[1].Metrics.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["mstest.assertion_usage"] = """{"Adapter.Context.Equal":2}""",
        });
        MSTestTelemetryDataCollector.Current.Should().BeNull();
        TelemetryCollector.DrainAssertionCallCounts().Should().BeEmpty();

        MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "fresh-after-execution";
        await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
        {
            events.Add((name, [with(metrics)]));
            return Task.CompletedTask;
        });
        events.Should().HaveCount(3);
        events[2].Name.Should().Be(ExecutionEvent);
        events[2].Metrics["mstest.config_source"].Should().Be("fresh-after-execution");
        events[2].Metrics.Should().NotContainKey("mstest.assertion_usage");
    }

    public async Task MissingSenderOrCollectorStillClearsStateAndDrainsAssertions()
    {
        foreach ((bool hasCollector, bool hasSender) in new[] { (true, false), (false, true), (false, false) })
        {
            MSTestTelemetryDataCollector.Current = hasCollector ? new() : null;
            int calls = 0;
            Func<string, IDictionary<string, object>, Task>? sender = null;
            if (hasSender)
            {
                sender = (_, _) =>
                {
                    calls++;
                    return Task.CompletedTask;
                };
            }

            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(sender);
            MSTestTelemetryDataCollector.Current.Should().BeNull();
            calls.Should().Be(0);

            MSTestTelemetryDataCollector.EnsureInitialized();
            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync((_, _) => Task.CompletedTask);
            MSTestTelemetryDataCollector.Current = hasCollector ? new() : null;
            TelemetryCollector.TrackAssertionCall("Adapter.Context.NoSender", isEnabled: true);

            await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(sender);

            calls.Should().Be(0);
            MSTestTelemetryDataCollector.Current.Should().BeNull();
            TelemetryCollector.DrainAssertionCallCounts().Should().BeEmpty();
            MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "after-early-return";
            Dictionary<string, object>? payload = null;
            await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
            {
                name.Should().Be(ExecutionEvent);
                payload = [with(metrics)];
                return Task.CompletedTask;
            });
            payload.Should().NotBeNull();
            payload!["mstest.config_source"].Should().Be("after-early-return");
            payload.Should().NotContainKey("mstest.assertion_usage");
        }
    }

    public async Task OptingOutAfterInitializationPreventsBothEventKinds()
    {
        foreach (string variable in new[] { PlatformOptOut, CliOptOut })
        {
            foreach (string value in new[] { "1", "true" })
            {
                Environment.SetEnvironmentVariable(PlatformOptOut, null);
                Environment.SetEnvironmentVariable(CliOptOut, null);
                MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "not-for-transmission";
                Environment.SetEnvironmentVariable(variable, value);
                int calls = 0;
                Func<string, IDictionary<string, object>, Task> sender = (_, _) =>
                {
                    calls++;
                    return Task.CompletedTask;
                };

                await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(sender);
                MSTestTelemetryDataCollector.Current.Should().BeNull();
                MSTestTelemetryDataCollector.EnsureInitialized();
                TelemetryCollector.TrackAssertionCall("Adapter.Context.OptedOut", isEnabled: true);
                await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(sender);

                calls.Should().Be(0);
                MSTestTelemetryDataCollector.Current.Should().BeNull();
                TelemetryCollector.DrainAssertionCallCounts().Should().BeEmpty();
            }
        }
    }

    public async Task DiscoverySenderFailurePreservesTheNextExecutionPayload()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "failed-discovery";
            var failure = new InvalidOperationException("local discovery sender rejected the event");
            int calls = 0;
            string? attemptedName = null;
            Dictionary<string, object>? attemptedMetrics = null;

            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync((name, metrics) =>
            {
                calls++;
                attemptedName = name;
                attemptedMetrics = [with(metrics)];
                return asynchronous ? Task.FromException(failure) : throw failure;
            });

            calls.Should().Be(1);
            attemptedName.Should().Be(DiscoveryEvent);
            attemptedMetrics.Should().NotBeNull();
            attemptedMetrics!["mstest.config_source"].Should().Be("failed-discovery");
            MSTestTelemetryDataCollector.Current.Should().BeNull();
            MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "after-failed-discovery";
            Dictionary<string, object>? payload = null;
            await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
            {
                name.Should().Be(ExecutionEvent);
                payload = [with(metrics)];
                return Task.CompletedTask;
            });
            payload.Should().NotBeNull();
            payload!["mstest.config_source"].Should().Be("after-failed-discovery");
        }
    }

    public async Task ExecutionSenderFailureDrainsCountersAndResetsDiscoverySuppression()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            MSTestTelemetryDataCollector.EnsureInitialized();
            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync((_, _) => Task.CompletedTask);
            MSTestTelemetryDataCollector.EnsureInitialized();
            TelemetryCollector.TrackAssertionCall("Adapter.Context.FailedSender", isEnabled: true);
            var failure = new InvalidOperationException("local execution sender rejected the event");
            int calls = 0;
            string? attemptedName = null;
            Dictionary<string, object>? attemptedMetrics = null;

            await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
            {
                calls++;
                attemptedName = name;
                attemptedMetrics = [with(metrics)];
                return asynchronous ? Task.FromException(failure) : throw failure;
            });

            calls.Should().Be(1);
            attemptedName.Should().Be(ExecutionEvent);
            attemptedMetrics.Should().BeEquivalentTo(new Dictionary<string, object>
            {
                ["mstest.assertion_usage"] = """{"Adapter.Context.FailedSender":1}""",
            });
            MSTestTelemetryDataCollector.Current.Should().BeNull();
            TelemetryCollector.DrainAssertionCallCounts().Should().BeEmpty();
            MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "after-failed-execution";
            Dictionary<string, object>? payload = null;
            await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
            {
                name.Should().Be(ExecutionEvent);
                payload = [with(metrics)];
                return Task.CompletedTask;
            });
            payload.Should().NotBeNull();
            payload!["mstest.config_source"].Should().Be("after-failed-execution");
            payload.Should().NotContainKey("mstest.assertion_usage");
        }
    }

#if NETFRAMEWORK
    public async Task DeniedOptOutEnvironmentCannotEscapeSendOrLeakSessionState()
    {
        MSTestTelemetryDataCollector.EnsureInitialized();
        TelemetryCollector.TrackAssertionCall("Adapter.Context.DeniedEnvironment", isEnabled: true);
        var permissions = new PermissionSet(PermissionState.None);
        permissions.AddPermission(new SecurityPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new FileIOPermission(PermissionState.Unrestricted));
        Exception? directFailure = null;
        Task discovery;
        Task execution;
        int calls = 0;
        Func<string, IDictionary<string, object>, Task> sender = (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        };
        try
        {
            permissions.PermitOnly();
            try
            {
                MSTestTelemetryDataCollector.IsTelemetryOptedOut();
            }
            catch (Exception exception)
            {
                directFailure = exception;
            }

            discovery = MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(sender);
            MSTestTelemetryDataCollector.Current = new MSTestTelemetryDataCollector();
            execution = MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(sender);
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        directFailure.Should().BeOfType<SecurityException>();
        await discovery;
        await execution;
        calls.Should().Be(0);
        MSTestTelemetryDataCollector.Current.Should().BeNull();
        TelemetryCollector.DrainAssertionCallCounts().Should().BeEmpty();
        MSTestTelemetryDataCollector.EnsureInitialized().ConfigurationSource = "after-denied-environment";
        Dictionary<string, object>? payload = null;
        await MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync((name, metrics) =>
        {
            name.Should().Be(ExecutionEvent);
            payload = [with(metrics)];
            return Task.CompletedTask;
        });
        payload.Should().NotBeNull();
        payload!["mstest.config_source"].Should().Be("after-denied-environment");
        payload.Should().NotContainKey("mstest.assertion_usage");
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

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class InheritedMethodAttribute : TestMethodAttribute;

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class OtherInheritedMethodAttribute : TestMethodAttribute;

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class InheritedClassAttribute : TestClassAttribute;
}
#endif
