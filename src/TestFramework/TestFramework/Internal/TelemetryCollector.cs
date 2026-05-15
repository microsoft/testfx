// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Collects aggregated telemetry data about MSTest API usage within a test session.
/// This data is used to understand which APIs are heavily used or unused to guide future investment.
/// </summary>
internal static class TelemetryCollector
{
    // Lazily evaluated opt-out flag. Mirrors the env-var checks performed by the adapter
    // (and by Microsoft.Testing.Platform's TelemetryManager) so the assertion hot path can
    // short-circuit when the user has opted out. Lazy<T> guarantees the env-var lookup
    // happens at most once per process.
    private static readonly Lazy<bool> IsEnabled = new(IsTelemetryEnabledFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);

    private static ConcurrentDictionary<string, long> s_assertionCallCounts = new();

    /// <summary>
    /// Records that an assertion method was called. This is on the hot path of every assertion,
    /// so it is aggressively inlined and short-circuits when the user has opted out of telemetry
    /// via <c>TESTINGPLATFORM_TELEMETRY_OPTOUT</c> or <c>DOTNET_CLI_TELEMETRY_OPTOUT</c>.
    /// Any unexpected exception (e.g. <see cref="OutOfMemoryException"/>) is swallowed so
    /// telemetry never alters user-visible test behavior.
    /// </summary>
    /// <param name="assertionName">The full name of the assertion (e.g. "Assert.AreEqual", "CollectionAssert.Contains").</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackAssertionCall(string assertionName)
    {
        if (!IsEnabled.Value)
        {
            return;
        }

        try
        {
            s_assertionCallCounts.AddOrUpdate(assertionName, 1, static (_, count) => count + 1);
        }
        catch
        {
            // Telemetry must never affect test outcomes.
        }
    }

    /// <summary>
    /// Gets a snapshot of all assertion call counts and resets the counters.
    /// This is thread-safe but best-effort: it atomically swaps the dictionary and copies the old one.
    /// In-flight calls to <see cref="TrackAssertionCall"/> that race with the swap may be lost.
    /// This is acceptable for telemetry where approximate counts are sufficient.
    /// </summary>
    /// <returns>A dictionary mapping assertion names to their (best-effort) call counts.</returns>
    internal static Dictionary<string, long> DrainAssertionCallCounts()
    {
        ConcurrentDictionary<string, long> old = Interlocked.Exchange(ref s_assertionCallCounts, new ConcurrentDictionary<string, long>());

        // Use the explicit Dictionary(IEnumerable<KeyValuePair>) ctor so we get a stable
        // snapshot of the swapped-out instance. A collection-expression spread would be
        // semantically equivalent but the explicit ctor reads more clearly here.
#pragma warning disable IDE0028 // Simplify collection initialization
        return new Dictionary<string, long>(old);
#pragma warning restore IDE0028
    }

    private static bool IsTelemetryEnabledFromEnvironment()
    {
        try
        {
            string? telemetryOptOut = Environment.GetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT");
            if (string.Equals(telemetryOptOut, "1", StringComparison.Ordinal) ||
                string.Equals(telemetryOptOut, "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? cliTelemetryOptOut = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");
            return !string.Equals(cliTelemetryOptOut, "1", StringComparison.Ordinal)
                && !string.Equals(cliTelemetryOptOut, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we cannot read environment variables (e.g. partial-trust scenarios), treat
            // that as opted out — telemetry must never affect test behavior.
            return false;
        }
    }
}
