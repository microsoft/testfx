// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ReliableTestSuite;

/// <summary>
/// STEP 2 - COORDINATE WHAT YOU CANNOT ELIMINATE.
///
/// These tests mutate a process-wide environment variable, so unlike the file tests they DO
/// share state with every other test in the process. Left unguarded under method-level
/// parallelization, one test's write would race another's read - the classic flaky failure.
///
/// With MSTest 4.4, [ResourceLock] is the precise tool for this shared state. It names the exact
/// resource that is shared, so the scheduler serializes only tests that declare the SAME key and
/// lets everything else run concurrently. [DoNotParallelize] would also prevent the race, but it
/// would serialize this class against the ENTIRE suite and defer it to the end of the run, even
/// against tests that never touch the environment.
///
/// A reader-only test could take the same key in shared mode with
/// [ResourceLock(WellKnownResources.EnvironmentVariables, Mode = ResourceAccessMode.Read)],
/// allowing concurrent readers while still excluding writers.
///
/// LIMITATIONS to be honest about:
///   - The lock is COOPERATIVE: it only coordinates tests that opt in with the SAME key. A test
///     that mutates the environment without declaring the lock is not held back by it.
///   - Scope is a single TEST SOURCE (assembly). The adapter creates a separate lock manager per
///     source, so matching keys serialize only the parallel tests WITHIN this assembly's run; they
///     do NOT coordinate tests in a different assembly, even when both run in the same test-host
///     process. It is not a cross-assembly, cross-process, or distributed/cross-agent mutex - don't
///     rely on it for global state shared across assemblies.
///   - WellKnownResources.EnvironmentVariables is a shared well-known key for process-wide
///     environment state; any string can be used as a custom key for your own shared resource.
///
/// The difference is determinism with throughput: [DoNotParallelize] trades all parallelism
/// for safety; [ResourceLock] trades only the parallelism that is genuinely unsafe.
/// </summary>
[TestClass]
[ResourceLock(WellKnownResources.EnvironmentVariables)]
public sealed class EnvironmentPricingTests
{
    [TestInitialize]
    public void ClearRate()
        => Environment.SetEnvironmentVariable(PricingCalculator.TaxRateVariable, null);

    [TestCleanup]
    public void ResetRate()
        => Environment.SetEnvironmentVariable(PricingCalculator.TaxRateVariable, null);

    [TestMethod]
    public void NoRateConfigured_ReturnsAmountUnchanged()
    {
        Assert.AreEqual(100m, PricingCalculator.ApplyTax(100m));
    }

    [TestMethod]
    public void RateConfigured_AppliesTax()
    {
        Environment.SetEnvironmentVariable(
            PricingCalculator.TaxRateVariable,
            0.2m.ToString(CultureInfo.InvariantCulture));

        Assert.AreEqual(120m, PricingCalculator.ApplyTax(100m));
    }
}
