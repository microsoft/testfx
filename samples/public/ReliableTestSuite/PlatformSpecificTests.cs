// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// STEP 3 - GATE ENVIRONMENT-SPECIFIC TESTS DECLARATIVELY.
///
/// A common source of "works on my machine" flakiness is a test that silently early-returns
/// (or worse, does nothing) when it is on the wrong OS, off CI, or when a required tool is
/// missing. That reports a false PASS. Condition attributes instead mark the test as NOT RUN
/// for the right reason, so the run summary tells the truth.
///
/// Conditions of the SAME group are OR'd; conditions in DIFFERENT groups are AND'd.
/// </summary>
[TestClass]
public sealed class PlatformSpecificTests
{
    // Runs only on Windows. On Linux/macOS this is reported as not-run, not as a hollow pass.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void UsesWindowsPathSemantics()
    {
        // Stands in for genuinely Windows-specific behavior (rather than re-asserting the OS):
        // the path separator is '\' and path comparison is case-insensitive.
        Assert.AreEqual('\\', Path.DirectorySeparatorChar);
        Assert.IsTrue(string.Equals(@"C:\Temp", @"c:\temp", StringComparison.OrdinalIgnoreCase));
    }

    // Excluded on CI (ConditionMode.Exclude) for a REAL environmental reason: this check needs an
    // interactive desktop session that headless CI agents do not have. That is honest gating.
    // Excluding a test merely because it is FLAKY on CI would be hiding the flake - the earlier
    // rungs (isolate, coordinate, bound) are how you fix that instead of muting it.
    [TestMethod]
    [CICondition(ConditionMode.Exclude)]
    public void InteractiveOnlyCheck_NotOnHeadlessCI()
    {
        Assert.IsTrue(Environment.UserInteractive);
    }

    // Runs only when 'dotnet' is resolvable on PATH. Gating on tool availability beats a
    // try/catch that swallows a missing-tool error and reports success.
    [TestMethod]
    [ExecutableCondition("dotnet")]
    public void RequiresDotnetCli()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        Assert.IsNotNull(path);
    }
}
