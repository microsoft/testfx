// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxPrototypeDiagnosticsTests
{
    private static readonly Lazy<TrxPrototypeDiagnosticRun> PaddedRun = new(TrxPrototypeDiagnostics.RunPadded);
    private static readonly Lazy<TrxPrototypeDiagnosticRun> JournalRun = new(TrxPrototypeDiagnostics.RunJournal);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TenThousandResults_PaddedPrototype_ReportsExactOperationsBytesFlushesSizesAndReflows()
    {
        TrxPrototypeDiagnosticRun run = PaddedRun.Value;
        WriteMetrics("padded", run);

        Assert.AreEqual(10_000, run.ResultCount);
        Assert.AreEqual(100, run.UniqueDefinitionCount);
        Assert.AreEqual(2_500, run.PassedCount);
        Assert.AreEqual(2_500, run.FailedCount);
        Assert.AreEqual(2_500, run.SkippedCount);
        Assert.AreEqual(2_500, run.TimeoutCount);
        Assert.AreEqual(run.Operations.RequestedWriteBytes, run.Operations.CommittedWriteBytes);
        Assert.AreEqual(0, run.Operations.RequestedReadBytes);
        Assert.AreEqual(run.PublishCount, run.Operations[TrxFileOperationKind.Replace]);
        Assert.AreEqual(run.PublishCount - 2, run.ReflowCount);
        Assert.AreEqual(1_024, run.InitialDefinitionPadBytes);
        Assert.AreEqual(1_024, run.InitialEntryPadBytes);
        Assert.IsGreaterThan(0, run.ReflowCount);
        Assert.IsGreaterThan(0, run.RemainingDefinitionPadBytes);
        Assert.IsGreaterThan(0, run.RemainingEntryPadBytes);
        Assert.IsGreaterThan(0, run.RemainingSummaryPadBytes);
        Assert.IsLessThan(run.PaddedSnapshotBytes, run.CompactSnapshotBytes);
        Assert.AreEqual(10_000, run.FixtureInputResultCount);
    }

    [TestMethod]
    public void TenThousandResults_JournalSnapshot_ReportsExactOperationsBytesFlushesSizesAndPublishes()
    {
        TrxPrototypeDiagnosticRun run = JournalRun.Value;
        WriteMetrics("journal", run);

        Assert.AreEqual(10_000, run.ResultCount);
        Assert.AreEqual(4, run.PublishCount);
        Assert.AreEqual(10_016, run.Operations[TrxFileOperationKind.Open]);
        Assert.AreEqual(150_012, run.Operations[TrxFileOperationKind.Read]);
        Assert.AreEqual(60_419, run.Operations[TrxFileOperationKind.Write]);
        Assert.AreEqual(10_004, run.Operations[TrxFileOperationKind.Flush]);
        Assert.AreEqual(4, run.Operations[TrxFileOperationKind.Replace]);
        Assert.AreEqual(0, run.Operations[TrxFileOperationKind.Seek]);
        Assert.AreEqual(0, run.Operations[TrxFileOperationKind.SetLength]);
        Assert.AreEqual(0, run.Operations[TrxFileOperationKind.Delete]);
        Assert.AreEqual(230_455, run.Operations.TotalOperations);
        Assert.AreEqual(
            run.JournalBytes.LongLength + run.Operations.ReplacementSizes.Sum(),
            run.Operations.RequestedWriteBytes);
        Assert.AreEqual(run.Operations.RequestedWriteBytes, run.Operations.CommittedWriteBytes);
        Assert.AreEqual(10_000, run.JournalDiagnostics!.PublishedRecordCount);
        Assert.AreEqual(100, run.JournalDiagnostics.PublishedDefinitionCount);
    }

    [TestMethod]
    public void TenThousandResults_MixedOutcomesRepeatedIdsMetadataAndRunningSlots_IsTruthfulAndOrdered()
    {
        TrxPrototypeDiagnosticRun run = JournalRun.Value;
        TrxDocumentExpectation expectation = CreateExpectation(run);
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(run.SnapshotBytes, expectation);

        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(run.SnapshotBytes);
        XNamespace ns = TrxDocumentClassifier.TeamTest2010Namespace;
        XElement root = document.Root!;
        Assert.AreEqual(10_000, root.Element(ns + "Results")!.Elements(ns + "UnitTestResult")
            .Count(element => element.Attribute("outcome")!.Value != "InProgress"));
        Assert.AreEqual(3, root.Element(ns + "Results")!.Elements(ns + "UnitTestResult")
            .Count(element => element.Attribute("outcome")!.Value == "InProgress"));
        Assert.HasCount(100, root.Element(ns + "TestDefinitions")!.Elements(ns + "UnitTest"));
        Assert.HasCount(10_000, root.Element(ns + "TestEntries")!.Elements(ns + "TestEntry"));
        Assert.AreSequenceEqual(
            run.ExecutionIds.Take(20).Select(value => value.ToString()).ToArray(),
            root.Element(ns + "Results")!.Elements(ns + "UnitTestResult")
                .Take(20)
                .Select(element => element.Attribute("executionId")!.Value)
                .ToArray());
        Assert.IsGreaterThan(500, root.Descendants(ns + "Description").Single().Value.Length);
        Assert.Contains(element => element.Value.Length > 1_000, root.Descendants(ns + "StdOut"));
        Assert.AreEqual(
            "diagnostic/collector-é漢😀<&>.bin",
            root.Descendants(ns + "A").Single().Attribute("href")!.Value);
    }

    [TestMethod]
    public void TenThousandResults_ReplayDiagnostics_ShowOneRecordAtATimeAndReleasedDefinitionState()
    {
        TrxPrototypeDiagnosticRun run = JournalRun.Value;
        TrxJournalSnapshotDiagnostics diagnostics = run.JournalDiagnostics!;

        Assert.AreEqual(0, diagnostics.CurrentReplayRecordCount);
        Assert.AreEqual(1, diagnostics.PeakReplayRecordCount);
        Assert.AreEqual(0, diagnostics.CurrentRecordBufferBytes);
        Assert.IsGreaterThan(diagnostics.MaxEncodedRecordBytes, diagnostics.PeakRecordBufferBytes);
        Assert.AreEqual(0, diagnostics.CurrentDefinitionIdCount);
        Assert.AreEqual(100, diagnostics.PeakDefinitionIdCount);
        Assert.IsFalse(diagnostics.RetainsResultCollection);
        Assert.IsFalse(diagnostics.RetainsXDocument);
        Assert.IsGreaterThan(0, diagnostics.MaxEncodedRecordBytes);
        Assert.IsGreaterThan(0, diagnostics.MaxRenderedFragmentBytes);
        Assert.IsLessThanOrEqualTo(
            diagnostics.MaxEncodedRecordBytes + diagnostics.MaxRenderedFragmentBytes,
            diagnostics.PeakLogicalBufferBytes);
        Assert.IsLessThan(run.JournalBytes.Length, diagnostics.PeakLogicalBufferBytes);
        Assert.IsLessThan(run.SnapshotBytes.Length, diagnostics.PeakLogicalBufferBytes);
    }

    [TestMethod]
    public void TenThousandResults_ManualMeasurement_EmitsReproducibleMetricsWithoutTimingAssertions()
    {
#if NETCOREAPP
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
#else
        long allocatedBefore = GC.GetTotalMemory(forceFullCollection: false);
#endif
        var stopwatch = Stopwatch.StartNew();
        TrxPrototypeDiagnosticRun run = TrxPrototypeDiagnostics.RunJournal();
        stopwatch.Stop();
#if NETCOREAPP
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
#else
        long allocatedAfter = GC.GetTotalMemory(forceFullCollection: false);
#endif
        using var process = Process.GetCurrentProcess();

        TestContext.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "manual-10k seed=phase5-fixed-v1; results={0}; uniqueDefinitions={1}; cadence={2}; elapsedMs={3}; gcAllocatedBytes={4}; workingSetBytes={5}; journalBytes={6}; snapshotBytes={7}; operations={8}; writes={9}; flushes={10}; replaces={11}",
                run.ResultCount,
                run.UniqueDefinitionCount,
                TrxPrototypeDiagnostics.PublicationCadence,
                stopwatch.ElapsedMilliseconds,
                allocatedAfter - allocatedBefore,
                process.WorkingSet64,
                run.JournalBytes.Length,
                run.SnapshotBytes.Length,
                run.Operations.TotalOperations,
                run.Operations[TrxFileOperationKind.Write],
                run.Operations[TrxFileOperationKind.Flush],
                run.Operations[TrxFileOperationKind.Replace]));

        Assert.AreEqual(10_000, run.ResultCount);
        Assert.AreEqual(100, run.UniqueDefinitionCount);
        Assert.AreEqual(4, run.PublishCount);
    }

    private void WriteMetrics(string name, TrxPrototypeDiagnosticRun run)
    {
        string operationCounts = string.Join(
            ",",
            run.Operations.Counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        string replacementSizes = string.Join(",", run.Operations.ReplacementSizes);
        TrxJournalSnapshotDiagnostics? journal = run.JournalDiagnostics;
        TestContext.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}-10k results={1}; uniqueDefinitions={2}; running={3}; outcomes=passed:{4},failed:{5},skipped:{6},timeout:{7}; operations={8}; totalOperations={9}; requestedReadBytes={10}; committedReadBytes={11}; requestedWriteBytes={12}; committedWriteBytes={13}; flushes={14}; replaces={15}; replacementSizes=[{16}]; journalBytes={17}; paddedBytes={18}; compactOrSnapshotBytes={19}; reflows={20}; pads=initialDefinition:{21},initialEntry:{22},remainingDefinition:{23},remainingEntry:{24},remainingSummary:{25}; maxWriteBytes={26}; maxFileBytes={27}; fixtureInputResults={28}; maxEncodedRecordBytes={29}; maxRenderedFragmentBytes={30}; peakLogicalBufferBytes={31}; currentReplayRecords={32}; peakReplayRecords={33}; currentRecordBufferBytes={34}; peakRecordBufferBytes={35}; currentDefinitionIds={36}; peakDefinitionIds={37}; retainsResultCollection={38}; retainsXDocument={39}",
                name,
                run.ResultCount,
                run.UniqueDefinitionCount,
                run.RunningCount,
                run.PassedCount,
                run.FailedCount,
                run.SkippedCount,
                run.TimeoutCount,
                operationCounts,
                run.Operations.TotalOperations,
                run.Operations.RequestedReadBytes,
                run.Operations.CommittedReadBytes,
                run.Operations.RequestedWriteBytes,
                run.Operations.CommittedWriteBytes,
                run.Operations[TrxFileOperationKind.Flush],
                run.Operations[TrxFileOperationKind.Replace],
                replacementSizes,
                run.JournalBytes.Length,
                run.PaddedSnapshotBytes,
                run.CompactSnapshotBytes,
                run.ReflowCount,
                run.InitialDefinitionPadBytes,
                run.InitialEntryPadBytes,
                run.RemainingDefinitionPadBytes,
                run.RemainingEntryPadBytes,
                run.RemainingSummaryPadBytes,
                run.Operations.MaxWriteBytes,
                run.Operations.MaxFileBytes,
                run.FixtureInputResultCount,
                journal?.MaxEncodedRecordBytes ?? 0,
                journal?.MaxRenderedFragmentBytes ?? 0,
                journal?.PeakLogicalBufferBytes ?? run.Operations.MaxWriteBytes,
                journal?.CurrentReplayRecordCount ?? 0,
                journal?.PeakReplayRecordCount ?? 0,
                journal?.CurrentRecordBufferBytes ?? 0,
                journal?.PeakRecordBufferBytes ?? 0,
                journal?.CurrentDefinitionIdCount ?? 0,
                journal?.PeakDefinitionIdCount ?? 0,
                journal?.RetainsResultCollection ?? true,
                journal?.RetainsXDocument ?? true));
    }

    private static TrxDocumentExpectation CreateExpectation(TrxPrototypeDiagnosticRun run)
    {
        var completed = new TrxExpectedResult[run.Results.Count];
        for (int i = 0; i < completed.Length; i++)
        {
            completed[i] = TrxExpectedResultFactory.Create(
                run.Results[i],
                run.ExecutionIds[i],
                TrxPhase3EvidenceMatrix.MachineName,
                TrxPhase3EvidenceMatrix.TestModule,
                TrxPhase3EvidenceMatrix.FrameworkUid,
                TrxPhase3EvidenceMatrix.FrameworkVersion,
                TrxPhase3EvidenceMatrix.FinishTime);
        }

        return new TrxDocumentExpectation
        {
            RunId = TrxPhase3EvidenceMatrix.RunId,
            RunName = TrxPhase3EvidenceMatrix.RunName,
            StartTime = TrxPhase3EvidenceMatrix.StartTime,
            FinishTime = TrxPhase3EvidenceMatrix.FinishTime,
            SummaryOutcome = "Failed",
            CompletedResults = completed,
            RunningTests =
            [
                .. run.RunningTests.Select(
                    running => new TrxExpectedRunningTest
                    {
                        TestId = Guid.Parse(running.Uid).ToString(),
                        ExecutionId = running.ExecutionId.ToString(),
                        TestName = running.DisplayName,
                        ComputerName = TrxPhase3EvidenceMatrix.MachineName,
                        StartTime = running.StartTime,
                    }),
            ],
        };
    }
}
