// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Collects aggregated telemetry data about MSTest API usage within a test session.
/// This data is used to understand which APIs are heavily used or unused to guide future investment.
/// </summary>
internal static class TelemetryCollector
{
    private static ConcurrentDictionary<string, long> s_assertionCallCounts = new();

    /// <summary>
    /// Records that an assertion method was called.
    /// </summary>
    /// <param name="assertionName">The full name of the assertion (e.g. "Assert.AreEqual", "CollectionAssert.Contains").</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackAssertionCall(string assertionName)
        => s_assertionCallCounts.AddOrUpdate(assertionName, 1, static (_, count) => count + 1);

    /// <summary>
    /// Gets a snapshot of all assertion call counts and resets the counters.
    /// This is thread-safe: it atomically swaps the dictionary and drains the old one.
    /// </summary>
    /// <returns>A dictionary mapping assertion names to call counts.</returns>
    internal static Dictionary<string, long> DrainAssertionCallCounts()
    {
        ConcurrentDictionary<string, long> old = Interlocked.Exchange(ref s_assertionCallCounts, new ConcurrentDictionary<string, long>());
#pragma warning disable IDE0028 // Simplify collection initialization - ConcurrentDictionary snapshot copy
        return new Dictionary<string, long>(old);
#pragma warning restore IDE0028 // Simplify collection initialization
    }
}
