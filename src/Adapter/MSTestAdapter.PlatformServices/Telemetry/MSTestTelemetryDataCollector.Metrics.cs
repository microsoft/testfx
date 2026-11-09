// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestTelemetryDataCollector
{
    /// <summary>
    /// Builds the discovery telemetry metrics dictionary (settings + discovery-time data).
    /// Sent at the end of MTP discover-only sessions (e.g. dotnet test --list-tests).
    /// </summary>
    /// <returns>A dictionary of telemetry key-value pairs for the discovery event.</returns>
    internal Dictionary<string, object> BuildDiscoveryMetrics()
    {
        Dictionary<string, object> metrics = [];
        AddDiscoveryMetrics(metrics);
        return metrics;
    }

    /// <summary>
    /// Builds the execution telemetry metrics dictionary. Sent at the end of an MSTest run
    /// (MTP run mode or VSTest run mode). Always carries assertion usage. Also carries the
    /// settings/attribute/config payload UNLESS a discovery event has already been emitted
    /// during this process — that avoids duplicating the discovery payload across two events
    /// when a host (such as a future MTP host) chooses to call both discover and run within
    /// the same session.
    /// </summary>
    /// <param name="assertionCounts">Drained assertion call counts captured during execution.</param>
    /// <param name="includeDiscoveryPayload">When true, also include the discovery metrics
    /// (settings, config_source, attribute_usage, custom_test_*_types). False when the discovery
    /// event already shipped these in this process.</param>
    /// <returns>A dictionary of telemetry key-value pairs for the sessionexit event.</returns>
    internal Dictionary<string, object> BuildExecutionMetrics(Dictionary<string, long> assertionCounts, bool includeDiscoveryPayload)
    {
        Dictionary<string, object> metrics = [];

        if (includeDiscoveryPayload)
        {
            AddDiscoveryMetrics(metrics);
        }

        if (assertionCounts.Count > 0)
        {
            metrics["mstest.assertion_usage"] = SerializeDictionary(assertionCounts);
        }

        return metrics;
    }

    private void AddDiscoveryMetrics(Dictionary<string, object> metrics)
    {
        // Settings
        AddSettingsMetrics(metrics);

        // Configuration source (runsettings, testconfig.json, or none)
        if (ConfigurationSource is { } configSource)
        {
            metrics["mstest.config_source"] = configSource;
        }

        // Attribute usage (aggregated counts as JSON; serializer enforces ordinal sort)
        if (!_attributeCounts.IsEmpty)
        {
            metrics["mstest.attribute_usage"] = SerializeDictionary(_attributeCounts);
        }

        // Custom/inherited types (anonymized names; serializer enforces ordinal sort)
        // Take a snapshot under the lock that protects the HashSet to avoid concurrent
        // modification while serializing.
        string[]? customMethodTypesSnapshot = SnapshotCustomTypes(_customTestMethodTypes);
        if (customMethodTypesSnapshot is { Length: > 0 })
        {
            metrics["mstest.custom_test_method_types"] = SerializeCollection(customMethodTypesSnapshot);
        }

        string[]? customClassTypesSnapshot = SnapshotCustomTypes(_customTestClassTypes);
        if (customClassTypesSnapshot is { Length: > 0 })
        {
            metrics["mstest.custom_test_class_types"] = SerializeCollection(customClassTypesSnapshot);
        }
    }

    private string[]? SnapshotCustomTypes(HashSet<string> set)
    {
        lock (_customTypesGate)
        {
            return set.Count == 0 ? null : [.. set];
        }
    }

    private static string SerializeCollection(IEnumerable<string> values)
    {
        StringBuilder builder = new("[");
        bool isFirst = true;

        foreach (string value in values.OrderBy(static x => x, StringComparer.Ordinal))
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            AppendJsonString(builder, value);
            isFirst = false;
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string SerializeDictionary(IEnumerable<KeyValuePair<string, long>> values)
    {
        StringBuilder builder = new("{");
        bool isFirst = true;

        foreach (KeyValuePair<string, long> value in values.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            AppendJsonString(builder, value.Key);
            builder.Append(':');
            builder.Append(value.Value.ToString(CultureInfo.InvariantCulture));
            isFirst = false;
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void AddSettingsMetrics(Dictionary<string, object> metrics)
    {
        MSTestSettings settings = MSTestSettings.CurrentSettings;

        // Parallelization
        metrics["mstest.setting.parallelization_enabled"] = AsTelemetryBool(!settings.DisableParallelization);
        if (settings.ParallelizationScope is not null)
        {
            metrics["mstest.setting.parallelization_scope"] = settings.ParallelizationScope.Value.ToString();
        }

        if (settings.ParallelizationWorkers is not null)
        {
            // Cast to double so AppInsightsProvider routes this through the metric channel
            // instead of stringifying it as a property — see AppInsightsProvider.SendLoopAsync.
            metrics["mstest.setting.parallelization_workers"] = (double)settings.ParallelizationWorkers.Value;
        }

        // Timeouts (cast to double for the same reason as parallelization_workers above).
        metrics["mstest.setting.test_timeout"] = (double)settings.TestTimeout;
        metrics["mstest.setting.assembly_initialize_timeout"] = (double)settings.AssemblyInitializeTimeout;
        metrics["mstest.setting.assembly_cleanup_timeout"] = (double)settings.AssemblyCleanupTimeout;
        metrics["mstest.setting.class_initialize_timeout"] = (double)settings.ClassInitializeTimeout;
        metrics["mstest.setting.class_cleanup_timeout"] = (double)settings.ClassCleanupTimeout;
        metrics["mstest.setting.test_initialize_timeout"] = (double)settings.TestInitializeTimeout;
        metrics["mstest.setting.test_cleanup_timeout"] = (double)settings.TestCleanupTimeout;
        metrics["mstest.setting.global_test_initialize_timeout"] = (double)settings.GlobalTestInitializeTimeout;
        metrics["mstest.setting.global_test_cleanup_timeout"] = (double)settings.GlobalTestCleanupTimeout;
        metrics["mstest.setting.cooperative_cancellation"] = AsTelemetryBool(settings.CooperativeCancellationTimeout);

        // Behavior
        metrics["mstest.setting.map_inconclusive_to_failed"] = AsTelemetryBool(settings.MapInconclusiveToFailed);
        metrics["mstest.setting.map_not_runnable_to_failed"] = AsTelemetryBool(settings.MapNotRunnableToFailed);
        metrics["mstest.setting.treat_discovery_warnings_as_errors"] = AsTelemetryBool(settings.TreatDiscoveryWarningsAsErrors);
        metrics["mstest.setting.consider_empty_data_source_as_inconclusive"] = AsTelemetryBool(settings.ConsiderEmptyDataSourceAsInconclusive);
        metrics["mstest.setting.order_tests_by_name"] = AsTelemetryBool(settings.OrderTestsByNameInClass);
        metrics["mstest.setting.randomize_test_order"] = AsTelemetryBool(settings.RandomizeTestOrder);
        metrics["mstest.setting.random_test_order_seed_provided"] = AsTelemetryBool(settings.RandomTestOrderSeed.HasValue);
        metrics["mstest.setting.capture_debug_traces"] = AsTelemetryBool(settings.CaptureDebugTraces);
        metrics["mstest.setting.output_capture_mode"] = settings.OutputCaptureMode.ToString();
    }

    // MTP's telemetry providers (e.g. AppInsightsProvider) reject raw boolean values and assert
    // that they should be sent as their lowercase string form. This mirrors
    // Microsoft.Testing.Platform.Telemetry.TelemetryExtensions.AsTelemetryBool, which we can't
    // reference from here because that type lives in a different assembly.
    private static string AsTelemetryBool(bool value) => value ? "true" : "false";
}
#endif
