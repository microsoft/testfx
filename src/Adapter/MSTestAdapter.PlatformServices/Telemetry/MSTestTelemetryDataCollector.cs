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
/// <remarks>
/// This collector relies on static state (<see cref="s_current"/>) that lives in the
/// AppDomain in which the adapter executes. On .NET Framework runs that opt into the
/// adapter's child-AppDomain isolation, code that runs inside the child AppDomain (for
/// example, attribute discovery via the adapter's enumerators) sees its own
/// <see cref="Current"/> snapshot, which is initially null. In that case telemetry from
/// the isolated AppDomain is silently dropped (the <c>Current?.Track*</c> call sites are
/// null-safe). This is an intentional, graceful degradation: the .NET Framework AppDomain
/// scenario is rare and the effort to marshal counters across AppDomain boundaries via
/// <see cref="MarshalByRefObject"/> is not justified for best-effort usage telemetry.
/// </remarks>
internal sealed class MSTestTelemetryDataCollector
{
    private readonly ConcurrentDictionary<string, long> _attributeCounts = new();
#if NET9_0_OR_GREATER
    private readonly Lock _customTypesGate = new();
#else
    private readonly object _customTypesGate = new();
#endif
    private readonly HashSet<string> _customTestMethodTypes = [];
    private readonly HashSet<string> _customTestClassTypes = [];

#pragma warning disable IDE0032 // Use auto property - Volatile.Read/Write requires a ref to a field
    private static MSTestTelemetryDataCollector? s_current;
    private static int s_discoveryEventEmitted;

    // Volatile because ConfigurationSource is written from the discovery thread (e.g. settings
    // load) and read from whichever thread runs SendDiscoveryTelemetryAndResetAsync — without
    // a memory barrier the reader could in principle observe a stale null.
    private volatile string? _configurationSource;
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
    internal string? ConfigurationSource
    {
        get => _configurationSource;
        set => _configurationSource = value;
    }

    /// <summary>
    /// Records the attributes found on a test method during discovery. Safe to call concurrently
    /// from multiple discovery threads — counters use a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// and the custom-type sets are protected by an internal lock.
    /// </summary>
    /// <param name="attributes">The cached attributes from the method.</param>
    internal void TrackDiscoveredMethod(Attribute[] attributes)
    {
        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();
            string attributeName = attributeType.Name;

            // Track custom/inherited TestMethodAttribute types (store anonymized hash)
            if (attribute is TestMethodAttribute && attributeType != typeof(TestMethodAttribute))
            {
                AddCustomType(_customTestMethodTypes, AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                AddCustomType(_customTestClassTypes, AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track attribute usage counts by base type name (only known MSTest attributes)
            string? trackingName = attribute switch
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
                _ => null,
            };

            if (trackingName is not null)
            {
                _attributeCounts.AddOrUpdate(trackingName, 1, static (_, count) => count + 1);
            }
        }
    }

    /// <summary>
    /// Records the attributes found on a test class during discovery. Safe to call concurrently
    /// from multiple discovery threads — counters use a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// and the custom-type sets are protected by an internal lock.
    /// </summary>
    /// <param name="attributes">The cached attributes from the class.</param>
    internal void TrackDiscoveredClass(Attribute[] attributes)
    {
        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                AddCustomType(_customTestClassTypes, AnonymizeString(attributeType.FullName ?? attributeType.Name));
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
                _attributeCounts.AddOrUpdate(trackingName, 1, static (_, count) => count + 1);
            }
        }
    }

    private void AddCustomType(HashSet<string> set, string value)
    {
        lock (_customTypesGate)
        {
            set.Add(value);
        }
    }

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
        metrics["mstest.setting.cooperative_cancellation"] = AsTelemetryBool(settings.CooperativeCancellationTimeout);

        // Behavior
        metrics["mstest.setting.map_inconclusive_to_failed"] = AsTelemetryBool(settings.MapInconclusiveToFailed);
        metrics["mstest.setting.map_not_runnable_to_failed"] = AsTelemetryBool(settings.MapNotRunnableToFailed);
        metrics["mstest.setting.treat_discovery_warnings_as_errors"] = AsTelemetryBool(settings.TreatDiscoveryWarningsAsErrors);
        metrics["mstest.setting.consider_empty_data_source_as_inconclusive"] = AsTelemetryBool(settings.ConsiderEmptyDataSourceAsInconclusive);
        metrics["mstest.setting.order_tests_by_name"] = AsTelemetryBool(settings.OrderTestsByNameInClass);
        metrics["mstest.setting.capture_debug_traces"] = AsTelemetryBool(settings.CaptureDebugTraces);
    }

    // MTP's telemetry providers (e.g. AppInsightsProvider) reject raw boolean values and assert
    // that they should be sent as their lowercase string form. This mirrors
    // Microsoft.Testing.Platform.Telemetry.TelemetryExtensions.AsTelemetryBool, which we can't
    // reference from here because that type lives in a different assembly.
    private static string AsTelemetryBool(bool value) => value ? "true" : "false";

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
    /// Sends the accumulated discovery telemetry via the provided sender delegate and clears the
    /// discovery state by resetting the current collector. Safe to call when no sender is available
    /// (the collector is still cleared so state does not leak across sessions).
    /// </summary>
    /// <param name="telemetrySender">Optional delegate to send telemetry. If null, telemetry is silently discarded.</param>
    internal static async Task SendDiscoveryTelemetryAndResetAsync(Func<string, IDictionary<string, object>, Task>? telemetrySender)
    {
        try
        {
            MSTestTelemetryDataCollector? collector = Current;
            if (collector is null || telemetrySender is null)
            {
                return;
            }

            // Defense in depth: re-check opt-out at send time in case the env var was set after
            // EnsureInitialized but before this point.
            if (IsTelemetryOptedOut())
            {
                return;
            }

            Dictionary<string, object> metrics = collector.BuildDiscoveryMetrics();
            if (metrics.Count > 0)
            {
                await telemetrySender("dotnet/testingplatform/mstest/discovery", metrics).ConfigureAwait(false);

                // Mark that the discovery payload (settings + attribute_usage + custom_test_*_types
                // + config_source) has shipped in this process so a subsequent execution event in
                // the same session does not duplicate it.
                Interlocked.Exchange(ref s_discoveryEventEmitted, 1);
            }
        }
        catch (Exception)
        {
            // Telemetry should never cause test failures
        }
        finally
        {
            // Clear the current collector so a subsequent execution accumulates settings/config
            // anew (settings are static-per-process so re-population is cheap and keeps each
            // event self-contained).
            Current = null;
        }
    }

    /// <summary>
    /// Sends the accumulated execution telemetry via the provided sender delegate, drains the
    /// static assertion counters, and clears the current collector. Safe to call when no sender
    /// is available (the counters and collector are still drained/cleared so state does not leak
    /// across sessions).
    /// </summary>
    /// <param name="telemetrySender">Optional delegate to send telemetry. If null, telemetry is silently discarded.</param>
    internal static async Task SendExecutionTelemetryAndResetAsync(Func<string, IDictionary<string, object>, Task>? telemetrySender)
    {
        try
        {
            // Always drain the static assertion counters so they don't leak across sessions,
            // even when no sender is wired (VSTest mode) or no collector was initialized
            // (e.g. telemetry opted out before EnsureInitialized was called).
            Dictionary<string, long> assertionCounts = TelemetryCollector.DrainAssertionCallCounts();

            MSTestTelemetryDataCollector? collector = Current;
            if (collector is null || telemetrySender is null)
            {
                return;
            }

            // Defense in depth: re-check opt-out at send time in case the env var was set after
            // EnsureInitialized but before this point.
            if (IsTelemetryOptedOut())
            {
                return;
            }

            // If the discovery event already shipped the settings/attribute payload during this
            // process, do not duplicate it in the sessionexit event. The flag is reset below in
            // the finally block so each process can still ship a fresh payload after a full
            // session reset.
            bool includeDiscoveryPayload = Interlocked.CompareExchange(ref s_discoveryEventEmitted, 0, 0) == 0;

            Dictionary<string, object> metrics = collector.BuildExecutionMetrics(assertionCounts, includeDiscoveryPayload);
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
            Interlocked.Exchange(ref s_discoveryEventEmitted, 0);
        }
    }
}
#endif
