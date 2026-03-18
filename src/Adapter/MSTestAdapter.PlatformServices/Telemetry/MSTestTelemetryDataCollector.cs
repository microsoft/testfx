// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Collects and aggregates telemetry data about MSTest usage within a test session.
/// Captures settings, attribute usage, custom/inherited types, and assertion API usage.
/// </summary>
internal sealed class MSTestTelemetryDataCollector
{
    private readonly Dictionary<string, long> _attributeCounts = [];
    private readonly HashSet<string> _customTestMethodTypes = [];
    private readonly HashSet<string> _customTestClassTypes = [];

#pragma warning disable IDE0032 // Use auto property - Volatile.Read/Write requires a ref to a field
    private static MSTestTelemetryDataCollector? s_current;
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Gets or sets the current telemetry data collector for the session.
    /// Set at session start, cleared at session close.
    /// </summary>
    internal static MSTestTelemetryDataCollector? Current
    {
        get => Volatile.Read(ref s_current);
        set => Volatile.Write(ref s_current, value);
    }

    internal static MSTestTelemetryDataCollector EnsureInitialized()
    {
        MSTestTelemetryDataCollector? collector = Current;
        if (collector is not null)
        {
            return collector;
        }

        collector = new MSTestTelemetryDataCollector();
        MSTestTelemetryDataCollector? existingCollector = Interlocked.CompareExchange(ref s_current, collector, null);

        return existingCollector ?? collector;
    }

    /// <summary>
    /// Gets a value indicating whether any data has been collected.
    /// </summary>
    internal bool HasData { get; private set; }

    /// <summary>
    /// Checks whether telemetry collection is opted out via environment variables.
    /// Mirrors the same checks as Microsoft.Testing.Platform's TelemetryManager.
    /// </summary>
    /// <returns><c>true</c> if telemetry is opted out; <c>false</c> otherwise.</returns>
    internal static bool IsTelemetryOptedOut()
    {
        string? telemetryOptOut = Environment.GetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT");
        if (telemetryOptOut is "1" or "true")
        {
            return true;
        }

        string? cliTelemetryOptOut = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");

        return cliTelemetryOptOut is "1" or "true";
    }

    /// <summary>
    /// Gets or sets the configuration source used for this session.
    /// </summary>
    internal string? ConfigurationSource { get; set; }

    /// <summary>
    /// Records the attributes found on a test method during discovery.
    /// </summary>
    /// <param name="attributes">The cached attributes from the method.</param>
    internal void TrackDiscoveredMethod(Attribute[] attributes)
    {
        HasData = true;

        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();
            string attributeName = attributeType.Name;

            // Track custom/inherited TestMethodAttribute types (store anonymized hash)
            if (attribute is TestMethodAttribute && attributeType != typeof(TestMethodAttribute))
            {
                _customTestMethodTypes.Add(AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                _customTestClassTypes.Add(AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track attribute usage counts by base type name
            string trackingName = attribute switch
            {
                TestMethodAttribute => nameof(TestMethodAttribute),
                TestClassAttribute => nameof(TestClassAttribute),
                DataRowAttribute => nameof(DataRowAttribute),
                DynamicDataAttribute => nameof(DynamicDataAttribute),
                TimeoutAttribute => nameof(TimeoutAttribute),
                IgnoreAttribute => nameof(IgnoreAttribute),
                DoNotParallelizeAttribute => nameof(DoNotParallelizeAttribute),
                RetryBaseAttribute => nameof(RetryBaseAttribute),
                ConditionBaseAttribute => nameof(ConditionBaseAttribute),
                TestCategoryAttribute => nameof(TestCategoryAttribute),
#if !WIN_UI
                DeploymentItemAttribute => nameof(DeploymentItemAttribute),
#endif
                _ => attributeName,
            };

            _attributeCounts[trackingName] = _attributeCounts.TryGetValue(trackingName, out long count)
                ? count + 1
                : 1;
        }
    }

    /// <summary>
    /// Records the attributes found on a test class during discovery.
    /// </summary>
    /// <param name="attributes">The cached attributes from the class.</param>
    internal void TrackDiscoveredClass(Attribute[] attributes)
    {
        HasData = true;

        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                _customTestClassTypes.Add(AnonymizeString(attributeType.FullName ?? attributeType.Name));
            }

            string? trackingName = attribute switch
            {
                TestClassAttribute => nameof(TestClassAttribute),
                ParallelizeAttribute => nameof(ParallelizeAttribute),
                DoNotParallelizeAttribute => nameof(DoNotParallelizeAttribute),
                _ => null,
            };

            if (trackingName is not null)
            {
                _attributeCounts[trackingName] = _attributeCounts.TryGetValue(trackingName, out long count)
                    ? count + 1
                    : 1;
            }
        }
    }

    /// <summary>
    /// Builds the telemetry metrics dictionary for sending via the telemetry collector.
    /// </summary>
    /// <returns>A dictionary of telemetry key-value pairs.</returns>
    internal Dictionary<string, object> BuildMetrics()
    {
        Dictionary<string, object> metrics = [];

        // Settings
        AddSettingsMetrics(metrics);

        // Configuration source (runsettings, testconfig.json, or none)
        if (ConfigurationSource is not null)
        {
            metrics["mstest.config_source"] = ConfigurationSource;
        }

        // Attribute usage (aggregated counts as JSON)
        if (_attributeCounts.Count > 0)
        {
            metrics["mstest.attribute_usage"] = SerializeDictionary(_attributeCounts);
        }

        // Custom/inherited types (anonymized names)
        if (_customTestMethodTypes.Count > 0)
        {
            metrics["mstest.custom_test_method_types"] = SerializeCollection(_customTestMethodTypes);
        }

        if (_customTestClassTypes.Count > 0)
        {
            metrics["mstest.custom_test_class_types"] = SerializeCollection(_customTestClassTypes);
        }

        // Assertion usage (drain the static counters)
        Dictionary<string, long> assertionCounts = TelemetryCollector.DrainAssertionCallCounts();
        if (assertionCounts.Count > 0)
        {
            metrics["mstest.assertion_usage"] = SerializeDictionary(assertionCounts);
        }

        return metrics;
    }

    private static string SerializeCollection(IEnumerable<string> values)
    {
        System.Text.StringBuilder builder = new("[");
        bool isFirst = true;

        foreach (string value in values)
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

    private static string SerializeDictionary(Dictionary<string, long> values)
    {
        System.Text.StringBuilder builder = new("{");
        bool isFirst = true;

        foreach (KeyValuePair<string, long> value in values)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            AppendJsonString(builder, value.Key);
            builder.Append(':');
            builder.Append(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            isFirst = false;
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendJsonString(System.Text.StringBuilder builder, string value)
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
                        builder.Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
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
        metrics["mstest.setting.parallelization_enabled"] = !settings.DisableParallelization;
        if (settings.ParallelizationScope is not null)
        {
            metrics["mstest.setting.parallelization_scope"] = settings.ParallelizationScope.Value.ToString();
        }

        if (settings.ParallelizationWorkers is not null)
        {
            metrics["mstest.setting.parallelization_workers"] = settings.ParallelizationWorkers.Value;
        }

        // Timeouts
        metrics["mstest.setting.test_timeout"] = settings.TestTimeout;
        metrics["mstest.setting.assembly_initialize_timeout"] = settings.AssemblyInitializeTimeout;
        metrics["mstest.setting.assembly_cleanup_timeout"] = settings.AssemblyCleanupTimeout;
        metrics["mstest.setting.class_initialize_timeout"] = settings.ClassInitializeTimeout;
        metrics["mstest.setting.class_cleanup_timeout"] = settings.ClassCleanupTimeout;
        metrics["mstest.setting.test_initialize_timeout"] = settings.TestInitializeTimeout;
        metrics["mstest.setting.test_cleanup_timeout"] = settings.TestCleanupTimeout;
        metrics["mstest.setting.cooperative_cancellation"] = settings.CooperativeCancellationTimeout;

        // Behavior
        metrics["mstest.setting.map_inconclusive_to_failed"] = settings.MapInconclusiveToFailed;
        metrics["mstest.setting.map_not_runnable_to_failed"] = settings.MapNotRunnableToFailed;
        metrics["mstest.setting.treat_discovery_warnings_as_errors"] = settings.TreatDiscoveryWarningsAsErrors;
        metrics["mstest.setting.consider_empty_data_source_as_inconclusive"] = settings.ConsiderEmptyDataSourceAsInconclusive;
        metrics["mstest.setting.order_tests_by_name"] = settings.OrderTestsByNameInClass;
        metrics["mstest.setting.capture_debug_traces"] = settings.CaptureDebugTraces;
        metrics["mstest.setting.has_test_settings_file"] = settings.TestSettingsFile is not null;
    }

    private static string AnonymizeString(string value)
    {
#if NET
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
#endif
    }

    /// <summary>
    /// Sends collected telemetry via the provided sender delegate and resets the current collector.
    /// Safe to call even when no sender is available (no-op).
    /// </summary>
    /// <param name="telemetrySender">Optional delegate to send telemetry. If null, telemetry is silently discarded.</param>
    internal static async Task SendTelemetryAndResetAsync(Func<string, IDictionary<string, object>, Task>? telemetrySender)
    {
        try
        {
            MSTestTelemetryDataCollector? collector = Current;
            if (collector is not { HasData: true } || telemetrySender is null)
            {
                TelemetryCollector.DrainAssertionCallCounts();
                return;
            }

            Dictionary<string, object> metrics = collector.BuildMetrics();
            if (metrics.Count > 0)
            {
                await telemetrySender("dotnet/testingplatform/mstest/sessionexit", metrics).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Telemetry should never cause test failures
        }
        finally
        {
            Current = null;
        }
    }
}
#endif
