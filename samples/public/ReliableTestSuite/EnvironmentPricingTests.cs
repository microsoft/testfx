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
/// TODAY (shipped MSTest): the blunt-but-correct tool is [DoNotParallelize]. It guarantees the
/// class runs with nothing else, but it is all-or-nothing: the class is serialized against the
/// ENTIRE suite and deferred to the end of the run, even against tests that never touch the
/// environment.
///
/// COMING IN MSTest 4.4 - the precise tool is [ResourceLock]. It names the exact resource that
/// is shared, so the scheduler serializes only tests that declare the SAME key and lets
/// everything else run concurrently:
///
///     // [assembly-visible once MSTest 4.4 ships]
///     [TestClass]
///     [ResourceLock(WellKnownResources.EnvironmentVariables)]   // exclusive by default
///     public sealed class EnvironmentPricingTests { ... }
///
/// A reader-only test could take the same key in shared mode with
/// [ResourceLock(WellKnownResources.EnvironmentVariables, Mode = ResourceAccessMode.Read)],
/// allowing concurrent readers while still excluding writers.
///
/// The difference is determinism with throughput: [DoNotParallelize] trades all parallelism
/// for safety; [ResourceLock] trades only the parallelism that is genuinely unsafe.
/// </summary>
[TestClass]
[DoNotParallelize]
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
