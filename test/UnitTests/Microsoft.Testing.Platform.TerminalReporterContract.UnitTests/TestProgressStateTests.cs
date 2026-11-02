// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.TerminalReporterContract.UnitTests;

[TestClass]
public sealed class TestProgressStateTests
{
    [TestMethod]
    public void NotifyHandshake_LegacyAndExplicitInstances_PreserveAttemptAssignments()
    {
        TestProgressState state = CreateState();

        state.NotifyHandshake("legacy");
        state.NotifyHandshake("legacy");
        state.NotifyHandshake("shard", attemptNumber: 1);
        state.NotifyHandshake("later", attemptNumber: 4);
        state.NotifyHandshake("earlier", attemptNumber: 2);
        state.NotifyHandshake("later");
        state.NotifyHandshake("next");

        Assert.AreEqual(1, state.GetAttemptNumber("legacy"));
        Assert.AreEqual(1, state.GetAttemptNumber("shard"));
        Assert.AreEqual(4, state.GetAttemptNumber("later"));
        Assert.AreEqual(2, state.GetAttemptNumber("earlier"));
        Assert.AreEqual(5, state.GetAttemptNumber("next"));
        Assert.AreEqual(5, state.TryCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void NotifyHandshake_InvalidAttempt_DoesNotRegisterInstance(int attemptNumber)
    {
        TestProgressState state = CreateState();

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => state.NotifyHandshake("instance", attemptNumber));

        Assert.AreEqual("attemptNumber", exception.ParamName);
        Assert.AreEqual(0, state.TryCount);
        state.NotifyHandshake("instance");
        Assert.AreEqual(1, state.GetAttemptNumber("instance"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void NotifyHandshake_RegisteredInstanceCannotChangeAttempt(int attemptNumber)
    {
        TestProgressState state = CreateState();
        state.NotifyHandshake("instance", attemptNumber: 1);

        Assert.ThrowsExactly<UnreachableException>(() => state.NotifyHandshake("instance", attemptNumber));

        Assert.AreEqual(1, state.GetAttemptNumber("instance"));
        Assert.AreEqual(1, state.TryCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ReportResult_InvalidRetryAttempt_PrecedesInstanceLookup(int retryAttemptNumber)
    {
        TestProgressState state = CreateState();

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => state.ReportPassingTest("uid", "Test", "unknown", retryAttemptNumber));

        Assert.AreEqual("retryAttemptNumber", exception.ParamName);
        Assert.AreEqual(0, state.TotalTests);
        Assert.AreEqual(0, state.TryCount);
    }

    [TestMethod]
    public void ReportResult_UnknownInstance_DoesNotChangeCounts()
    {
        TestProgressState state = CreateState();

        Assert.ThrowsExactly<UnreachableException>(() => state.ReportFailedTest("uid", "Test", "unknown"));

        Assert.AreEqual(0, state.FailedTests);
        Assert.AreEqual(0, state.TotalTests);
        Assert.AreEqual(0, state.TryCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReportResult_StaleAttempt_DoesNotChangeLatestResults(bool olderHost)
    {
        TestProgressState state = CreateState();
        state.NotifyHandshake("first", attemptNumber: 1);
        state.NotifyHandshake("second", attemptNumber: 2);
        state.ReportFailedTest("uid", "Test", "second", retryAttemptNumber: 2);

        Assert.ThrowsExactly<UnreachableException>(() => state.ReportPassingTest(
            "uid", "Test", olderHost ? "first" : "second", retryAttemptNumber: olderHost ? 5 : 1));

        Assert.AreEqual(1, state.FailedTests);
        Assert.AreEqual(0, state.PassedTests);
        Assert.AreEqual(1, state.TotalTests);
        Assert.AreEqual(0, state.RetriedTests);
        Assert.AreEqual(0, state.RetriedExecutions);
        Assert.IsEmpty(state.GetFlakyTests());
    }

    [TestMethod]
    public void ReportResult_FoldedRowsAndComposedRetries_ReplaceEarlierTallies()
    {
        TestProgressState state = CreateState();
        state.NotifyHandshake("first-shard", attemptNumber: 1);
        state.NotifyHandshake("second-shard", attemptNumber: 1);
        state.NotifyHandshake("retry-host", attemptNumber: 2);
        state.ReportFailedTest("folded", "Original", "first-shard");
        state.ReportFailedTest("folded", "Original", "second-shard");
        state.ReportPassingTest("stable", "Stable", "first-shard");
        Assert.AreEqual(2, state.FailedTests);
        Assert.AreEqual(3, state.TotalTests);

        state.ReportPassingTest("folded", "Retry", "first-shard", retryAttemptNumber: 2);
        state.ReportSkippedTest("folded", "Retry", "second-shard", retryAttemptNumber: 2);
        Assert.AreEqual(2, state.PassedTests);
        Assert.AreEqual(1, state.SkippedTests);
        Assert.AreEqual(0, state.FailedTests);
        Assert.AreEqual(2, state.RetriedFailedTests);
        Assert.AreEqual(2, state.RetriedExecutions);
        Assert.AreEqual(0, state.FlakyTests);
        Assert.IsEmpty(state.GetFlakyTests());

        state.ReportFailedTest("folded", "Host retry", "retry-host");
        state.ReportPassingTest("folded", "Recovered", "retry-host", retryAttemptNumber: 2);
        state.ReportPassingTest("folded", "Another row", "retry-host", retryAttemptNumber: 2);

        Assert.AreEqual(3, state.PassedTests);
        Assert.AreEqual(0, state.SkippedTests);
        Assert.AreEqual(0, state.FailedTests);
        Assert.AreEqual(3, state.TotalTests);
        Assert.AreEqual(3, state.RetriedFailedTests);
        Assert.AreEqual(1, state.RetriedTests);
        Assert.AreEqual(5, state.RetriedExecutions);
        Assert.AreEqual(1, state.FlakyTests);
        Assert.AreEqual(("Recovered", 4), Assert.ContainsSingle(state.GetFlakyTests()));
    }

    [TestMethod]
    public void GetSlowestTests_DuplicateUidsAndTies_UseLatestValuesAndOrdinalOrder()
    {
        TestProgressState state = CreateState();
        Assert.IsEmpty(state.GetSlowestTests(5));

        state.RecordTestDuration("first", "Old", TimeSpan.FromSeconds(9));
        state.RecordTestDuration("second", "a", TimeSpan.FromSeconds(3));
        state.RecordTestDuration("third", "A", TimeSpan.FromSeconds(3));
        state.RecordTestDuration("first", "z", TimeSpan.FromSeconds(3));

        Assert.IsEmpty(state.GetSlowestTests(0));
        Assert.IsEmpty(state.GetSlowestTests(-1));
        Assert.AreSequenceEqual(
            [("A", TimeSpan.FromSeconds(3)), ("a", TimeSpan.FromSeconds(3)), ("z", TimeSpan.FromSeconds(3))],
            state.GetSlowestTests(5));
        Assert.AreSequenceEqual(
            [("A", TimeSpan.FromSeconds(3)), ("a", TimeSpan.FromSeconds(3))],
            state.GetSlowestTests(2));

        state.RecordTestDuration("second", "Removed", duration: null);
        state.RecordTestDuration("missing", "Missing", duration: null);

        Assert.AreSequenceEqual(
            [("A", TimeSpan.FromSeconds(3)), ("z", TimeSpan.FromSeconds(3))],
            state.GetSlowestTests(5));
    }

    [TestMethod]
    public void ReportDiscoveredTest_NullAndDuplicateNames_PreserveCountsAndSnapshotIsolation()
    {
        TestProgressState state = CreateState(isDiscovery: true);
        state.ReportDiscoveredTest(null);
        state.ReportDiscoveredTest("Same");
        state.ReportDiscoveredTest("Same");
        state.ReportDiscoveredTest(string.Empty);
        List<string> snapshot = state.DiscoveredTestDisplayNames;
        snapshot.Clear();

        Assert.AreEqual(4, state.DiscoveredTests);
        Assert.AreEqual(4, state.TotalTests);
        Assert.AreSequenceEqual(["Same", "Same", string.Empty], state.DiscoveredTestDisplayNames);

        List<string> replacement = ["Replacement"];
        state.DiscoveredTestDisplayNames = replacement;
        replacement.Clear();
        Assert.AreSequenceEqual(["Replacement"], state.DiscoveredTestDisplayNames);
        Assert.AreEqual(4, state.DiscoveredTests);
    }

    private static TestProgressState CreateState(bool isDiscovery = false)
        => new(1, "assembly.dll", "net8.0", "x64", new SystemStopwatch(), isDiscovery);
}
