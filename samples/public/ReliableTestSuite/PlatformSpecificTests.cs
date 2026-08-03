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
    public void UsesWindowsOnlyPath()
    {
        Assert.IsTrue(OperatingSystem.IsWindows());
    }

    // Skips a timing-sensitive check when running under CI (ConditionMode.Exclude), where
    // shared/throttled agents make it flaky - without pretending it passed locally.
    [TestMethod]
    [CICondition(ConditionMode.Exclude)]
    public void TimingSensitiveCheck_NotOnCI()
    {
        // A real check that would be timing-flaky on shared CI agents.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        stopwatch.Stop();
        Assert.IsFalse(stopwatch.IsRunning);
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
