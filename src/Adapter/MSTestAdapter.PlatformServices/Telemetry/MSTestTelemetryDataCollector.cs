// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
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
internal sealed partial class MSTestTelemetryDataCollector
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
}
#endif
