// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxIncrementalWriterInterruptionTests
{
    private static readonly Lazy<TrxPhase3EvidenceReport> Evidence =
        new(TrxPhase3EvidenceMatrix.Create);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Initialize_EveryCreateWriteFlushAndByteCut_ClassifiesStartupStates()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "startup-absent");
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "startup-existing");
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "startup-recoverable");

        TrxPhase3EvidenceObservation truncated = report.Observations.Single(
            observation => observation.Scenario == "startup-existing"
                && observation.Dimension == TrxPhase3EvidenceDimension.Operation
                && observation.OperationIndex == 0);
        Assert.AreEqual(TrxDocumentClassification.Malformed, truncated.Classification, truncated.Diagnostic);
        Assert.AreEqual(0, truncated.TargetLength);

        Assert.Contains(
            observation => observation.Scenario == "startup-absent"
                && observation.Dimension == TrxPhase3EvidenceDimension.Byte
                && observation.Cut == 0
                && observation.Classification == TrxDocumentClassification.Malformed,
            report.Observations);
        Assert.Contains(
            observation => observation.Scenario == "startup-absent"
                && observation.Dimension == TrxPhase3EvidenceDimension.Byte
                && observation.Cut == observation.RequestedBytes
                && observation.Classification == TrxDocumentClassification.Truthful,
            report.Observations);
        Assert.Contains(
            observation => observation.Scenario == "startup-recoverable"
                && observation.Classification == TrxDocumentClassification.Repairable,
            report.Observations);
    }

    [TestMethod]
    public void ResultTailAndClosers_EverySeekWriteFlushAndByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        foreach (string scenario in new[] { "tail-zero-prior", "tail-one-prior", "tail-many-prior" })
        {
            TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, scenario);
            TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, scenario, TrxPhase3WriteRole.ResultTail);
        }

        Assert.Contains(
            observation => observation.WriteRole == TrxPhase3WriteRole.ResultTail
                && observation.Classification == TrxDocumentClassification.Malformed,
            report.Observations);
        Assert.Contains(
            observation => observation.WriteRole == TrxPhase3WriteRole.ResultTail
                && observation.Cut == observation.RequestedBytes
                && observation.Classification == TrxDocumentClassification.ParseableInconsistent,
            report.Observations);
    }

    [TestMethod]
    public void DefinitionPad_EveryStructuralByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "definition-small");
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "definition-large-metadata");
        TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(
            report,
            "definition-small",
            TrxPhase3WriteRole.Definition);

        TrxPhase3EvidenceObservation largeDefinition = report.Observations.First(
            observation => observation.Scenario == "definition-large-metadata"
                && observation.Dimension == TrxPhase3EvidenceDimension.Byte
                && observation.WriteRole == TrxPhase3WriteRole.Definition);
        Assert.IsGreaterThan(500, largeDefinition.RequestedBytes);
        TrxPhase3EvidenceMatrix.AssertSelectedLargeCuts(report, "definition-large-metadata", largeDefinition.OperationIndex);
    }

    [TestMethod]
    public void EntryPad_EveryStructuralByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        foreach (string scenario in new[] { "entry-unique", "entry-repeated", "entry-exact-boundary" })
        {
            TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, scenario);
            TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, scenario, TrxPhase3WriteRole.Entry);
        }

        TrxPhase3EvidenceMatrix.AssertEntryOverflowIsRejectedBeforeMutation();
    }

    [TestMethod]
    public void FixedCounters_EveryCellTransitionAndByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        foreach (string scenario in new[] { "counter-0-to-1", "counter-9-to-10", "counter-99-to-100" })
        {
            TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, scenario);
        }

        TrxPhase3EvidenceMatrix.AssertEveryCounterCellWasCut(report, "counter-0-to-1");
        Assert.Contains(
            observation => observation.WriteRole == TrxPhase3WriteRole.Counter
                && observation.Classification == TrxDocumentClassification.ParseableInconsistent,
            report.Observations);
    }

    [TestMethod]
    public void FixedOutcomeAndTimestamp_EveryByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, "finish-timestamp", TrxPhase3WriteRole.Timestamp);
        TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, "clean-outcome", TrxPhase3WriteRole.Outcome);
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "summary-crash-details");

        Assert.Contains(
            observation => observation.Scenario == "finish-timestamp"
                && observation.Classification == TrxDocumentClassification.ParseableInconsistent,
            report.Observations);
        Assert.Contains(
            observation => observation.Scenario == "clean-outcome"
                && observation.Classification == TrxDocumentClassification.Malformed,
            report.Observations);
    }

    [TestMethod]
    public void RunningSlots_ClaimReleaseCompleteAndOverflow_EveryByteCut_ProducesStableEvidence()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        foreach (string scenario in new[] { "running-claim", "running-second-claim", "running-release" })
        {
            TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, scenario);
        }

        TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, "running-claim", TrxPhase3WriteRole.RunningSlot);
        TrxPhase3EvidenceMatrix.AssertEveryByteWasCut(report, "running-release", TrxPhase3WriteRole.RunningSlotClear);
        TrxPhase3EvidenceMatrix.AssertRunningValidationFailuresDoNotMutate();
    }

    [TestMethod]
    public void ForcedPartialWrites_AllStructuralWrites_AreFrozenWithoutCleanupRepair()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        TrxPhase3WriteRole[] requiredRoles =
        [
            TrxPhase3WriteRole.StartupDocument,
            TrxPhase3WriteRole.Definition,
            TrxPhase3WriteRole.Entry,
            TrxPhase3WriteRole.ResultTail,
            TrxPhase3WriteRole.Counter,
            TrxPhase3WriteRole.Timestamp,
            TrxPhase3WriteRole.Outcome,
            TrxPhase3WriteRole.RunningSlot,
            TrxPhase3WriteRole.RunningSlotClear,
        ];

        foreach (TrxPhase3WriteRole role in requiredRoles)
        {
            Assert.Contains(
                observation => observation.Dimension == TrxPhase3EvidenceDimension.Byte
                        && observation.WriteRole == role
                        && observation.Cut == 0,
                report.Observations,
                $"No zero-byte frozen prefix was recorded for {role}.");
            Assert.Contains(
                observation => observation.Dimension == TrxPhase3EvidenceDimension.Byte
                        && observation.WriteRole == role
                        && observation.Cut == observation.RequestedBytes,
                report.Observations,
                $"No complete frozen prefix was recorded for {role}.");
        }

        Assert.IsTrue(report.Observations.All(observation => observation.CleanupWasFrozen));
    }

    [TestMethod]
    public void LargePayload_SelectedUtf8EntityTagAndSectorAdjacentCuts_AreClassified()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;
        TrxPhase3EvidenceMatrix.AssertOperationCoverage(report, "large-payload");
        foreach (TrxPhase3EvidenceObservation write in report.Observations
                     .Where(observation => observation.Scenario == "large-payload"
                         && observation.Dimension == TrxPhase3EvidenceDimension.Byte)
                     .GroupBy(observation => observation.OperationIndex)
                     .Select(group => group.First()))
        {
            Assert.IsGreaterThan(500, write.RequestedBytes);
            TrxPhase3EvidenceMatrix.AssertSelectedLargeCuts(report, "large-payload", write.OperationIndex);
        }
    }

    [TestMethod]
    public void PaddedWriter_EvidenceDigest_IsStableAndContainsExpectedCounterexamples()
    {
        TrxPhase3EvidenceReport report = Evidence.Value;

        Assert.AreEqual(574, report.OperationCaseCount);
        Assert.AreEqual(5_082, report.ByteCaseCount);
        Assert.HasCount(5_698, report.Observations);
        Assert.AreEqual(report.Observations.Count, report.ClassificationCounts.Values.Sum());
        Assert.AreEqual(4_546, report.ClassificationCounts[TrxDocumentClassification.Malformed]);
        Assert.AreEqual(719, report.ClassificationCounts[TrxDocumentClassification.ParseableInconsistent]);
        Assert.AreEqual(22, report.ClassificationCounts[TrxDocumentClassification.Repairable]);
        Assert.AreEqual(411, report.ClassificationCounts[TrxDocumentClassification.Truthful]);
        Assert.AreEqual(
            "A2F70EA25564B1EDA70F4804D7C452E7D286D955F21B262D67ABF0759FCDD62E",
            report.Digest);
        Assert.IsTrue(report.Observations.All(observation => observation.WriteRole != TrxPhase3WriteRole.Unknown));
        Assert.IsTrue(report.Observations.All(observation => observation.Diagnostic.Contains("window=", StringComparison.Ordinal)));
        Assert.IsTrue(report.Observations.All(observation => observation.Diagnostic.Contains("flushes=", StringComparison.Ordinal)));

        TestContext.WriteLine(report.Summary);
    }
}

internal enum TrxPhase3EvidenceDimension
{
    BeforeFirst,
    Operation,
    Byte,
    Successful,
}

internal enum TrxPhase3WriteRole
{
    None,
    StartupDocument,
    Definition,
    Entry,
    ResultTail,
    Counter,
    Timestamp,
    Outcome,
    RunningSlot,
    RunningSlotClear,
    Summary,
    Unknown,
}

internal sealed class TrxPhase3EvidenceObservation
{
    public required string Scenario { get; init; }

    public required TrxPhase3EvidenceDimension Dimension { get; init; }

    public required int OperationIndex { get; init; }

    public required TrxFileOperationKind? OperationKind { get; init; }

    public required TrxPhase3WriteRole WriteRole { get; init; }

    public required long Offset { get; init; }

    public required int RequestedBytes { get; init; }

    public required int? Cut { get; init; }

    public required int TargetLength { get; init; }

    public required int FlushCount { get; init; }

    public required bool TargetPresent { get; init; }

    public required bool CleanupWasFrozen { get; init; }

    public required TrxDocumentClassification Classification { get; init; }

    public required string Diagnostic { get; init; }
}

internal sealed class TrxPhase3EvidenceReport
{
    public required IReadOnlyList<TrxPhase3EvidenceObservation> Observations { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<TrxFileOperationRecord>> BaselineOperations { get; init; }

    public required IReadOnlyDictionary<TrxDocumentClassification, int> ClassificationCounts { get; init; }

    public required int OperationCaseCount { get; init; }

    public required int ByteCaseCount { get; init; }

    public required string Digest { get; init; }

    public required string Summary { get; init; }
}

internal static class TrxPhase3EvidenceMatrix
{
    internal const string TargetPath = "results.trx";
    internal const string RecoveryPath = "results.trx.recovery";
    internal const string MachineName = "phase3-machine";
    internal const string TestModule = "Phase3.Tests.dll";
    internal const string FrameworkUid = "phase3-framework";
    internal const string FrameworkVersion = "1.0.0";
    internal const string RunName = "phase3-user@phase3-machine 2026-07-20 10:00:00.0000000";
    internal const int CounterWidth = 3;

    internal static readonly Guid RunId = new("10000000-0000-0000-0000-000000000031");
    internal static readonly DateTimeOffset StartTime = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset FinishTime = new(2026, 7, 20, 10, 5, 0, TimeSpan.Zero);
    internal static readonly string[] PrototypeRootOrder =
        ["Times", "TestSettings", "TestDefinitions", "TestEntries", "TestLists", "ResultSummary", "Results"];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static TrxPhase3EvidenceReport Create()
    {
        List<TrxPhase3Scenario> scenarios = CreateScenarios();
        List<TrxPhase3EvidenceObservation> observations = [];
        var baselineOperations = new Dictionary<string, IReadOnlyList<TrxFileOperationRecord>>(StringComparer.Ordinal);

        foreach (TrxPhase3Scenario scenario in scenarios)
        {
            TrxPhase3Run baseline = Run(scenario, terminationPlan: null, captureSnapshots: true);
            Assert.IsTrue(baseline.Completed);
            Assert.AreEqual(
                TrxDocumentClassification.Truthful,
                ClassifyBest(scenario, baseline.TargetBytes, operation: null, cut: null).Classification,
                scenario.Name);
            baselineOperations.Add(scenario.Name, baseline.Operations);

            observations.Add(CreateBeforeFirstObservation(scenario, baseline.PreActSnapshot));
            for (int operationIndex = 0; operationIndex < baseline.Operations.Count; operationIndex++)
            {
                TrxFileOperationRecord operation = baseline.Operations[operationIndex];
                observations.Add(RunFaultCase(
                    scenario,
                    baseline,
                    operation,
                    TrxPhase3EvidenceDimension.Operation,
                    cut: null));

                if (operation.Kind != TrxFileOperationKind.Write)
                {
                    continue;
                }

                TrxPhase3WriteRole role = InferWriteRole(scenario, baseline, operation);
                IReadOnlyList<int> cuts = scenario.ExhaustiveRoles.Contains(role)
                    ? Enumerable.Range(0, operation.RequestedByteCount + 1).ToArray()
                    : scenario.SelectedRoles.Contains(role)
                        ? CreateSelectedCuts(GetWrittenBytes(baseline, operation))
                        : [];
                foreach (int cut in cuts)
                {
                    observations.Add(RunFaultCase(
                        scenario,
                        baseline,
                        operation,
                        TrxPhase3EvidenceDimension.Byte,
                        cut));
                }
            }

            TrxDocumentObservation successful = ClassifyBest(
                scenario,
                baseline.TargetBytes,
                operation: null,
                cut: null);
            observations.Add(new TrxPhase3EvidenceObservation
            {
                Scenario = scenario.Name,
                Dimension = TrxPhase3EvidenceDimension.Successful,
                OperationIndex = baseline.Operations.Count,
                OperationKind = null,
                WriteRole = TrxPhase3WriteRole.None,
                Offset = 0,
                RequestedBytes = 0,
                Cut = null,
                TargetLength = baseline.TargetBytes.Length,
                FlushCount = baseline.Operations.Count(operation => operation.Kind == TrxFileOperationKind.Flush),
                TargetPresent = baseline.TargetPresent,
                CleanupWasFrozen = true,
                Classification = successful.Classification,
                Diagnostic = CreateStableDiagnostic(
                    scenario.Name,
                    TrxPhase3EvidenceDimension.Successful,
                    baseline.Operations.Count,
                    null,
                    TrxPhase3WriteRole.None,
                    0,
                    0,
                    null,
                    baseline.TargetPresent,
                    baseline.TargetBytes,
                    baseline.Operations.Count(operation => operation.Kind == TrxFileOperationKind.Flush),
                    successful.Classification),
            });
        }

        IReadOnlyDictionary<TrxDocumentClassification, int> counts = Enum
            .GetValues(typeof(TrxDocumentClassification))
            .Cast<TrxDocumentClassification>()
            .ToDictionary(
                classification => classification,
                classification => observations.Count(observation => observation.Classification == classification));
        int operationCases = observations.Count(observation => observation.Dimension == TrxPhase3EvidenceDimension.Operation);
        int byteCases = observations.Count(observation => observation.Dimension == TrxPhase3EvidenceDimension.Byte);
        string digest = ComputeDigest(observations.Select(observation => observation.Diagnostic));
        string countText = string.Join(
            ",",
            counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "phase3 scenarios={0}; operations={1}; byteCuts={2}; observations={3}; classifications=[{4}]; digest={5}",
            scenarios.Count,
            operationCases,
            byteCases,
            observations.Count,
            countText,
            digest);

        return new TrxPhase3EvidenceReport
        {
            Observations = observations,
            BaselineOperations = baselineOperations,
            ClassificationCounts = counts,
            OperationCaseCount = operationCases,
            ByteCaseCount = byteCases,
            Digest = digest,
            Summary = summary,
        };
    }

    public static void AssertOperationCoverage(TrxPhase3EvidenceReport report, string scenario)
    {
        IReadOnlyList<TrxFileOperationRecord> baseline = report.BaselineOperations[scenario];
        TrxPhase3EvidenceObservation[] operationCases =
        [
            .. report.Observations.Where(
                observation => observation.Scenario == scenario
                    && observation.Dimension == TrxPhase3EvidenceDimension.Operation),
        ];
        Assert.HasCount(baseline.Count, operationCases);
        Assert.AreSequenceEqual(
            baseline.Select(operation => operation.OperationIndex).ToArray(),
            operationCases.Select(observation => observation.OperationIndex).ToArray());

        foreach (TrxFileOperationKind required in new[]
                 {
                     TrxFileOperationKind.Seek,
                     TrxFileOperationKind.Write,
                     TrxFileOperationKind.Flush,
                 })
        {
            if (baseline.Any(operation => operation.Kind == required))
            {
                Assert.AreEqual(
                    baseline.Count(operation => operation.Kind == required),
                    operationCases.Count(observation => observation.OperationKind == required),
                    $"{scenario}: {required}");
            }
        }
    }

    public static void AssertEveryByteWasCut(
        TrxPhase3EvidenceReport report,
        string scenario,
        TrxPhase3WriteRole role)
    {
        IGrouping<int, TrxPhase3EvidenceObservation>[] writes =
        [
            .. report.Observations
                .Where(observation => observation.Scenario == scenario
                    && observation.Dimension == TrxPhase3EvidenceDimension.Byte
                    && observation.WriteRole == role)
                .GroupBy(observation => observation.OperationIndex),
        ];
        Assert.IsNotEmpty(writes, $"{scenario}: {role}");
        foreach (IGrouping<int, TrxPhase3EvidenceObservation> write in writes)
        {
            int requested = write.First().RequestedBytes;
            Assert.AreSequenceEqual(
                Enumerable.Range(0, requested + 1).ToArray(),
                write.Select(observation => observation.Cut!.Value).OrderBy(value => value).ToArray(),
                $"{scenario}: operation {write.Key}, role {role}");
        }
    }

    public static void AssertSelectedLargeCuts(
        TrxPhase3EvidenceReport report,
        string scenario,
        int operationIndex)
    {
        int[] cuts =
        [
            .. report.Observations
                .Where(observation => observation.Scenario == scenario
                    && observation.OperationIndex == operationIndex
                    && observation.Dimension == TrxPhase3EvidenceDimension.Byte)
                .Select(observation => observation.Cut!.Value)
                .OrderBy(value => value),
        ];
        Assert.IsNotEmpty(cuts);
        int requested = report.Observations.First(
            observation => observation.Scenario == scenario
                && observation.OperationIndex == operationIndex
                && observation.Dimension == TrxPhase3EvidenceDimension.Byte).RequestedBytes;
        Assert.Contains(0, cuts);
        Assert.Contains(1, cuts);
        Assert.Contains(requested - 1, cuts);
        Assert.Contains(requested, cuts);
        if (requested > 513)
        {
            Assert.Contains(511, cuts);
            Assert.Contains(512, cuts);
            Assert.Contains(513, cuts);
        }

        if (requested > 4_097)
        {
            Assert.Contains(4_095, cuts);
            Assert.Contains(4_096, cuts);
            Assert.Contains(4_097, cuts);
        }
    }

    public static void AssertEveryCounterCellWasCut(TrxPhase3EvidenceReport report, string scenario)
    {
        IGrouping<int, TrxPhase3EvidenceObservation>[] cells =
        [
            .. report.Observations
                .Where(observation => observation.Scenario == scenario
                    && observation.Dimension == TrxPhase3EvidenceDimension.Byte
                    && observation.WriteRole == TrxPhase3WriteRole.Counter)
                .GroupBy(observation => observation.OperationIndex),
        ];
        Assert.HasCount(7, cells);
        foreach (IGrouping<int, TrxPhase3EvidenceObservation> cell in cells)
        {
            Assert.AreSequenceEqual(
                Enumerable.Range(0, CounterWidth + 1).ToArray(),
                cell.Select(observation => observation.Cut!.Value).OrderBy(value => value).ToArray());
        }
    }

    public static void AssertEntryOverflowIsRejectedBeforeMutation()
    {
        TrxTestResult result = CreateResult(41, "entry-overflow", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(41);
        int entryBytes = TrxPrototypeXmlRenderer.RenderEntry(result.Uid, executionId).Length;
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false, recordOperations: false);
        TrxIncrementalWriterPrototype writer = CreateWriter(
            operations,
            definitionPadBytes: 2_048,
            entryPadBytes: entryBytes - 1);
        writer.Initialize();
        operations.BeginFaultWindow(terminationPlan: null);
        byte[] before = operations.GetFileBytes(TargetPath);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => writer.AppendCompleted(result, executionId));

        Assert.IsEmpty(operations.Operations);
        Assert.AreSequenceEqual(before, operations.GetFileBytes(TargetPath));
    }

    public static void AssertRunningValidationFailuresDoNotMutate()
    {
        var operations = new TrxFaultInjectingFileOperations(captureSnapshots: false, recordOperations: false);
        TrxIncrementalWriterPrototype writer = CreateWriter(operations, runningSlotCount: 1, runningSlotByteCapacity: 320);
        writer.Initialize();
        Guid runningExecution = ExecutionId(51);
        int slot = writer.ClaimRunning(TestId(51), "running-one", runningExecution, StartTime);
        operations.BeginFaultWindow(terminationPlan: null);
        byte[] before = operations.GetFileBytes(TargetPath);

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => writer.ClaimRunning(TestId(52), "overflow", ExecutionId(52), StartTime));
        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => writer.ClaimRunning(TestId(51), "duplicate", runningExecution, StartTime));
        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => writer.AppendCompleted(CreateResult(52, "not-claimed", TrxTestOutcome.Passed), ExecutionId(52), slot));

        Assert.IsEmpty(operations.Operations);
        Assert.AreSequenceEqual(before, operations.GetFileBytes(TargetPath));
    }

    internal static TrxIncrementalWriterPrototype CreateWriter(
        ITrxPrototypeFileOperations operations,
        int definitionPadBytes = 4_096,
        int entryPadBytes = 2_048,
        int summaryPadBytes = 2_048,
        int counterWidth = CounterWidth,
        int runningSlotCount = 2,
        int runningSlotByteCapacity = 320)
        => new(
            operations,
            TargetPath,
            RunId,
            RunName,
            MachineName,
            TestModule,
            FrameworkUid,
            FrameworkVersion,
            StartTime,
            definitionPadBytes,
            entryPadBytes,
            summaryPadBytes,
            counterWidth,
            runningSlotCount,
            runningSlotByteCapacity);

    internal static TrxDocumentExpectation CreateExpectation(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        IReadOnlyList<TrxExpectedRunningTest>? running = null,
        DateTimeOffset? finishTime = null,
        string summaryOutcome = "Failed")
    {
        var expected = new TrxExpectedResult[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            expected[i] = TrxExpectedResultFactory.Create(
                results[i],
                executionIds[i],
                MachineName,
                TestModule,
                FrameworkUid,
                FrameworkVersion,
                finishTime ?? StartTime);
        }

        return new TrxDocumentExpectation
        {
            RunId = RunId,
            RunName = RunName,
            StartTime = StartTime,
            FinishTime = finishTime ?? StartTime,
            SummaryOutcome = summaryOutcome,
            CompletedResults = expected,
            RunningTests = running ?? [],
            RootChildOrder = PrototypeRootOrder,
        };
    }

    internal static TrxTestResult CreateResult(
        int number,
        string displayName,
        TrxTestOutcome outcome,
        string? definitionName = null,
        IReadOnlyList<TrxTestMetadata>? metadata = null,
        IReadOnlyList<string>? categories = null,
        IReadOnlyList<TrxStreamMessage>? messages = null)
        => new()
        {
            Uid = TestId(number),
            DisplayName = displayName,
            Outcome = outcome,
            StartTime = StartTime.AddSeconds(number),
            EndTime = StartTime.AddSeconds(number + 1),
            Duration = TimeSpan.FromSeconds(1),
            TrxTestDefinitionName = definitionName ?? displayName,
            TrxFullyQualifiedTypeName = "Phase3.ContractTests",
            TestMethodIdentifier = new TrxTestMethodIdentifier
            {
                Namespace = "Phase3",
                TypeName = "ContractTests",
                MethodName = definitionName ?? displayName,
            },
            Metadata = metadata,
            Categories = categories,
            Messages = messages,
        };

    internal static string TestId(int value) => $"20000000-0000-0000-0000-{value:D12}";

    internal static Guid ExecutionId(int value) => new($"30000000-0000-0000-0000-{value:D12}");

    private static List<TrxPhase3Scenario> CreateScenarios()
    {
        List<TrxPhase3Scenario> scenarios = [];
        TrxDocumentExpectation empty = CreateExpectation([], []);
        byte[] initialSmall = TrxPrototypeXmlRenderer.RenderInitial(
            RunId,
            RunName,
            StartTime,
            definitionPadBytes: 32,
            entryPadBytes: 32,
            summaryPadBytes: 32,
            counterWidth: CounterWidth,
            runningSlotCount: 0,
            runningSlotByteCapacity: 1);
        scenarios.Add(new TrxPhase3Scenario(
            "startup-absent",
            empty,
            empty,
            _ => { },
            writer => writer.Initialize(),
            definitionPadBytes: 32,
            entryPadBytes: 32,
            summaryPadBytes: 32,
            runningSlotCount: 0,
            runningSlotByteCapacity: 1,
            exhaustiveRoles: [TrxPhase3WriteRole.StartupDocument]));
        scenarios.Add(new TrxPhase3Scenario(
            "startup-existing",
            empty,
            empty,
            _ => { },
            writer => writer.Initialize(),
            definitionPadBytes: 32,
            entryPadBytes: 32,
            summaryPadBytes: 32,
            runningSlotCount: 0,
            runningSlotByteCapacity: 1,
            seedTarget: initialSmall,
            selectedRoles: [TrxPhase3WriteRole.StartupDocument]));
        scenarios.Add(new TrxPhase3Scenario(
            "startup-recoverable",
            empty,
            empty,
            _ => { },
            writer => writer.Initialize(),
            definitionPadBytes: 32,
            entryPadBytes: 32,
            summaryPadBytes: 32,
            runningSlotCount: 0,
            runningSlotByteCapacity: 1,
            seedTarget: initialSmall,
            recoveryBytes: initialSmall,
            selectedRoles: [TrxPhase3WriteRole.StartupDocument]));

        AddTailScenario(scenarios, "tail-zero-prior", priorCount: 0, resultNumber: 1);
        AddTailScenario(scenarios, "tail-one-prior", priorCount: 1, resultNumber: 2);
        AddTailScenario(scenarios, "tail-many-prior", priorCount: 3, resultNumber: 4);

        TrxTestResult smallDefinition = CreateResult(
            11,
            "definition <&> é漢😀",
            TrxTestOutcome.Passed,
            metadata: [new TrxTestMetadata { Key = "Owner", Value = "Zoë <owner>" }],
            categories: ["fast", "unicode-漢"]);
        AddAppendScenario(
            scenarios,
            "definition-small",
            [],
            [],
            smallDefinition,
            ExecutionId(11),
            exhaustiveRoles: [TrxPhase3WriteRole.Definition]);

        string largeMetadataValue = string.Concat("é漢😀<&>", new string('界', 260));
        TrxTestResult largeDefinition = CreateResult(
            12,
            "large-definition",
            TrxTestOutcome.Passed,
            metadata:
            [
                new TrxTestMetadata { Key = "Owner", Value = "Zoë" },
                new TrxTestMetadata { Key = "Description", Value = largeMetadataValue },
                new TrxTestMetadata { Key = "Custom<&>", Value = largeMetadataValue },
            ],
            categories: ["large", "unicode-漢"]);
        AddAppendScenario(
            scenarios,
            "definition-large-metadata",
            [],
            [],
            largeDefinition,
            ExecutionId(12),
            definitionPadBytes: 4_096,
            selectedRoles: [TrxPhase3WriteRole.Definition]);

        AddAppendScenario(
            scenarios,
            "entry-unique",
            [],
            [],
            CreateResult(13, "entry-unique", TrxTestOutcome.Passed),
            ExecutionId(13),
            exhaustiveRoles: [TrxPhase3WriteRole.Entry]);

        TrxTestResult repeatedFirst = CreateResult(14, "repeat[1]", TrxTestOutcome.Passed, "Repeated.Definition");
        TrxTestResult repeatedSecond = CreateResult(14, "repeat[2]", TrxTestOutcome.Passed, "Repeated.Definition");
        AddAppendScenario(
            scenarios,
            "entry-repeated",
            [repeatedFirst],
            [ExecutionId(14)],
            repeatedSecond,
            ExecutionId(15),
            exhaustiveRoles: [TrxPhase3WriteRole.Entry]);

        TrxTestResult exactEntry = CreateResult(16, "entry-boundary", TrxTestOutcome.Passed);
        int exactEntryBytes = TrxPrototypeXmlRenderer.RenderEntry(exactEntry.Uid, ExecutionId(16)).Length;
        AddAppendScenario(
            scenarios,
            "entry-exact-boundary",
            [],
            [],
            exactEntry,
            ExecutionId(16),
            entryPadBytes: exactEntryBytes,
            exhaustiveRoles: [TrxPhase3WriteRole.Entry]);

        AddCounterScenario(scenarios, "counter-0-to-1", priorCount: 0, exhaustive: true);
        AddCounterScenario(scenarios, "counter-9-to-10", priorCount: 9, exhaustive: false);
        AddCounterScenario(scenarios, "counter-99-to-100", priorCount: 99, exhaustive: false);

        scenarios.Add(CreateTimestampScenario());
        scenarios.Add(CreateOutcomeScenario());
        scenarios.Add(CreateSummaryScenario());
        scenarios.Add(CreateRunningClaimScenario("running-claim", hasPriorClaim: false));
        scenarios.Add(CreateRunningClaimScenario("running-second-claim", hasPriorClaim: true));
        scenarios.Add(CreateRunningReleaseScenario());
        scenarios.Add(CreateLargePayloadScenario());
        return scenarios;
    }

    private static void AddTailScenario(
        List<TrxPhase3Scenario> scenarios,
        string name,
        int priorCount,
        int resultNumber)
    {
        List<TrxTestResult> prior = [];
        List<Guid> priorExecutions = [];
        for (int i = 1; i <= priorCount; i++)
        {
            prior.Add(CreateResult(i, $"prior-{i}", TrxTestOutcome.Passed, $"Prior.{i}"));
            priorExecutions.Add(ExecutionId(i));
        }

        TrxTestResult result = CreateResult(resultNumber, $"tail-{resultNumber}", TrxTestOutcome.Passed, $"Tail.{resultNumber}");
        AddAppendScenario(
            scenarios,
            name,
            prior,
            priorExecutions,
            result,
            ExecutionId(100 + resultNumber),
            exhaustiveRoles: [TrxPhase3WriteRole.ResultTail]);
    }

    private static void AddAppendScenario(
        List<TrxPhase3Scenario> scenarios,
        string name,
        IReadOnlyList<TrxTestResult> prior,
        IReadOnlyList<Guid> priorExecutions,
        TrxTestResult result,
        Guid executionId,
        int definitionPadBytes = 4_096,
        int entryPadBytes = 2_048,
        IReadOnlyList<TrxPhase3WriteRole>? exhaustiveRoles = null,
        IReadOnlyList<TrxPhase3WriteRole>? selectedRoles = null)
    {
        TrxTestResult[] afterResults = [.. prior, result];
        Guid[] afterExecutions = [.. priorExecutions, executionId];
        scenarios.Add(new TrxPhase3Scenario(
            name,
            CreateExpectation(prior, priorExecutions),
            CreateExpectation(afterResults, afterExecutions),
            writer =>
            {
                writer.Initialize();
                for (int i = 0; i < prior.Count; i++)
                {
                    writer.AppendCompleted(prior[i], priorExecutions[i]);
                }
            },
            writer => writer.AppendCompleted(result, executionId),
            definitionPadBytes: definitionPadBytes,
            entryPadBytes: entryPadBytes,
            exhaustiveRoles: exhaustiveRoles,
            selectedRoles: selectedRoles));
    }

    private static void AddCounterScenario(
        List<TrxPhase3Scenario> scenarios,
        string name,
        int priorCount,
        bool exhaustive)
    {
        List<TrxTestResult> prior = [];
        List<Guid> priorExecutions = [];
        for (int i = 1; i <= priorCount; i++)
        {
            prior.Add(CreateResult(21, $"counter[{i}]", TrxTestOutcome.Passed, "Counter.Definition"));
            priorExecutions.Add(ExecutionId(200 + i));
        }

        TrxTestResult next = CreateResult(21, $"counter[{priorCount + 1}]", TrxTestOutcome.Passed, "Counter.Definition");
        AddAppendScenario(
            scenarios,
            name,
            prior,
            priorExecutions,
            next,
            ExecutionId(200 + priorCount + 1),
            definitionPadBytes: 1_024,
            entryPadBytes: 24_000,
            exhaustiveRoles: exhaustive ? [TrxPhase3WriteRole.Counter] : null,
            selectedRoles: exhaustive ? null : [TrxPhase3WriteRole.Counter]);
    }

    private static TrxPhase3Scenario CreateTimestampScenario()
    {
        DateTimeOffset finish = new(2026, 7, 20, 15, 45, 30, TimeSpan.FromHours(-4));
        return new TrxPhase3Scenario(
            "finish-timestamp",
            CreateExpectation([], []),
            CreateExpectation([], [], finishTime: finish),
            writer => writer.Initialize(),
            writer => writer.UpdateFinishTime(finish),
            exhaustiveRoles: [TrxPhase3WriteRole.Timestamp]);
    }

    private static TrxPhase3Scenario CreateOutcomeScenario()
    {
        TrxTestResult result = CreateResult(22, "clean-complete", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(22);
        return new TrxPhase3Scenario(
            "clean-outcome",
            CreateExpectation([result], [executionId]),
            CreateExpectation([result], [executionId], finishTime: FinishTime, summaryOutcome: "Completed"),
            writer =>
            {
                writer.Initialize();
                writer.AppendCompleted(result, executionId);
            },
            writer => writer.Complete(new TrxPrototypeCompletion { FinishTime = FinishTime }),
            exhaustiveRoles: [TrxPhase3WriteRole.Timestamp, TrxPhase3WriteRole.Outcome]);
    }

    private static TrxPhase3Scenario CreateSummaryScenario()
    {
        TrxTestResult result = CreateResult(23, "crash-summary", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(23);
        return new TrxPhase3Scenario(
            "summary-crash-details",
            CreateExpectation([result], [executionId]),
            CreateExpectation([result], [executionId], finishTime: FinishTime),
            writer =>
            {
                writer.Initialize();
                writer.AppendCompleted(result, executionId);
            },
            writer => writer.Complete(
                new TrxPrototypeCompletion
                {
                    FinishTime = FinishTime,
                    IsTestHostCrashed = true,
                    ExitCode = 17,
                    CrashText = "host crashed <&> 😀",
                    CollectorAttachmentHrefs = ["collector/first.bin", "collector/second.bin"],
                    AttachmentWarnings = ["warning one", "warning two"],
                }),
            selectedRoles: [TrxPhase3WriteRole.Summary]);
    }

    private static TrxPhase3Scenario CreateRunningClaimScenario(string name, bool hasPriorClaim)
    {
        const int slotCapacity = 320;
        Guid firstExecution = ExecutionId(31);
        Guid secondExecution = ExecutionId(32);
        string firstName = "first-running";
        string longSecondName = string.Concat("prefix<&>é漢😀", new string('界', 80), "😀tail");
        string renderedSecondName = ReadRunningName(
            new TrxPrototypeXmlRenderer(MachineName, TestModule, FrameworkUid, FrameworkVersion)
                .RenderRunningSlot(TestId(32), longSecondName, secondExecution, StartTime, slotCapacity));
        TrxExpectedRunningTest first = CreateExpectedRunning(31, firstExecution, firstName);
        TrxExpectedRunningTest second = CreateExpectedRunning(32, secondExecution, renderedSecondName);

        return new TrxPhase3Scenario(
            name,
            CreateExpectation([], [], hasPriorClaim ? [first] : []),
            CreateExpectation([], [], hasPriorClaim ? [first, second] : [second]),
            writer =>
            {
                writer.Initialize();
                if (hasPriorClaim)
                {
                    _ = writer.ClaimRunning(TestId(31), firstName, firstExecution, StartTime);
                }
            },
            writer => _ = writer.ClaimRunning(TestId(32), longSecondName, secondExecution, StartTime),
            runningSlotCount: 2,
            runningSlotByteCapacity: slotCapacity,
            exhaustiveRoles: [TrxPhase3WriteRole.RunningSlot]);
    }

    private static TrxPhase3Scenario CreateRunningReleaseScenario()
    {
        const int slotCapacity = 320;
        TrxTestResult result = CreateResult(33, "release-running", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(33);
        TrxExpectedRunningTest running = CreateExpectedRunning(33, executionId, result.DisplayName);
        return new TrxPhase3Scenario(
            "running-release",
            CreateExpectation([], [], [running]),
            CreateExpectation([result], [executionId]),
            writer =>
            {
                writer.Initialize();
                _ = writer.ClaimRunning(result.Uid, result.DisplayName, executionId, StartTime);
            },
            writer => writer.AppendCompleted(result, executionId, runningSlot: 0),
            runningSlotCount: 1,
            runningSlotByteCapacity: slotCapacity,
            exhaustiveRoles: [TrxPhase3WriteRole.RunningSlotClear]);
    }

    private static TrxPhase3Scenario CreateLargePayloadScenario()
    {
        string payload = string.Concat(
            "begin<&>é漢😀",
            new string('x', 5_200),
            "<middle>&amp;",
            new string('y', 3_000),
            "😀end");
        string metadata = string.Concat("metadata<&>é漢😀", new string('界', 300));
        TrxTestResult result = CreateResult(
            34,
            "large-payload",
            TrxTestOutcome.Failed,
            metadata:
            [
                new TrxTestMetadata { Key = "Description", Value = metadata },
                new TrxTestMetadata { Key = "Custom<&>", Value = metadata },
            ],
            messages:
            [
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = payload },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardError, Message = payload },
            ]);
        Guid executionId = ExecutionId(34);
        return new TrxPhase3Scenario(
            "large-payload",
            CreateExpectation([], []),
            CreateExpectation([result], [executionId]),
            writer => writer.Initialize(),
            writer => writer.AppendCompleted(result, executionId),
            definitionPadBytes: 4_096,
            selectedRoles: [TrxPhase3WriteRole.Definition, TrxPhase3WriteRole.ResultTail]);
    }

    private static TrxExpectedRunningTest CreateExpectedRunning(int number, Guid executionId, string displayName)
        => new()
        {
            TestId = Guid.Parse(TestId(number)).ToString(),
            ExecutionId = executionId.ToString(),
            TestName = displayName,
            ComputerName = MachineName,
            StartTime = StartTime,
        };

    private static string ReadRunningName(byte[] slot)
    {
        string xml = StrictUtf8.GetString(slot).TrimEnd();
        return XElement.Parse(xml).Attribute("testName")!.Value;
    }

    private static TrxPhase3Run Run(
        TrxPhase3Scenario scenario,
        TrxTerminationPlan? terminationPlan,
        bool captureSnapshots)
    {
        var operations = new TrxFaultInjectingFileOperations(
            terminationPlan: null,
            captureSnapshots: captureSnapshots,
            recordOperations: false);
        if (scenario.SeedTarget is not null)
        {
            operations.SeedFile(TargetPath, scenario.SeedTarget);
        }

        if (scenario.RecoveryBytes is not null)
        {
            operations.SeedFile(RecoveryPath, scenario.RecoveryBytes);
        }

        TrxIncrementalWriterPrototype writer = CreateWriter(
            operations,
            scenario.DefinitionPadBytes,
            scenario.EntryPadBytes,
            scenario.SummaryPadBytes,
            CounterWidth,
            scenario.RunningSlotCount,
            scenario.RunningSlotByteCapacity);
        scenario.Setup(writer);
        TrxVirtualFileSystemSnapshot preAct = operations.CaptureSnapshot();
        operations.BeginFaultWindow(terminationPlan);
        bool completed = false;
        try
        {
            scenario.Act(writer);
            completed = true;
        }
        catch (TrxSimulatedProcessTerminationException)
        {
            if (terminationPlan is null)
            {
                throw;
            }
        }

        TrxVirtualFileSystemSnapshot frozen = operations.CaptureSnapshot();
        return new TrxPhase3Run(
            operations,
            preAct,
            frozen,
            completed,
            frozen.Contains(TargetPath) ? frozen.GetFileBytes(TargetPath) : []);
    }

    private static TrxPhase3EvidenceObservation CreateBeforeFirstObservation(
        TrxPhase3Scenario scenario,
        TrxVirtualFileSystemSnapshot preAct)
    {
        bool present = preAct.Contains(TargetPath);
        byte[] bytes = present ? preAct.GetFileBytes(TargetPath) : [];
        TrxDocumentObservation classification = ClassifyBest(scenario, bytes, operation: null, cut: null, useBeforeOnly: true);
        return new TrxPhase3EvidenceObservation
        {
            Scenario = scenario.Name,
            Dimension = TrxPhase3EvidenceDimension.BeforeFirst,
            OperationIndex = -1,
            OperationKind = null,
            WriteRole = TrxPhase3WriteRole.None,
            Offset = 0,
            RequestedBytes = 0,
            Cut = null,
            TargetLength = bytes.Length,
            FlushCount = 0,
            TargetPresent = present,
            CleanupWasFrozen = true,
            Classification = classification.Classification,
            Diagnostic = CreateStableDiagnostic(
                scenario.Name,
                TrxPhase3EvidenceDimension.BeforeFirst,
                -1,
                null,
                TrxPhase3WriteRole.None,
                0,
                0,
                null,
                present,
                bytes,
                0,
                classification.Classification),
        };
    }

    private static TrxPhase3EvidenceObservation RunFaultCase(
        TrxPhase3Scenario scenario,
        TrxPhase3Run baseline,
        TrxFileOperationRecord baselineOperation,
        TrxPhase3EvidenceDimension dimension,
        int? cut)
    {
        var plan = new TrxTerminationPlan(baselineOperation.OperationIndex, cut);
        TrxPhase3Run fault = Run(scenario, plan, captureSnapshots: false);
        Assert.IsFalse(fault.Completed);
        Assert.IsTrue(fault.OperationsOwner.IsProcessDead);
        Assert.HasCount(baselineOperation.OperationIndex + 1, fault.Operations);
        TrxFileOperationRecord actualOperation = fault.Operations[fault.Operations.Count - 1];
        Assert.AreEqual(baselineOperation.Kind, actualOperation.Kind);
        Assert.AreEqual(baselineOperation.PrePosition, actualOperation.PrePosition);
        Assert.AreEqual(baselineOperation.RequestedByteCount, actualOperation.RequestedByteCount);
        Assert.AreEqual(
            cut ?? baselineOperation.RequestedByteCount,
            actualOperation.CommittedByteCount);

        byte[] expectedBytes = CreateExpectedFrozenBytes(
            baseline,
            baselineOperation,
            cut,
            out bool expectedPresent);
        Assert.AreEqual(expectedPresent, fault.TargetPresent);
        Assert.AreSequenceEqual(expectedBytes, fault.TargetBytes);

        int operationCount = fault.Operations.Count;
        string beforeRejectedCleanup = fault.FrozenSnapshot.ToString();
        _ = Assert.ThrowsExactly<TrxSimulatedProcessTerminationException>(
            () => fault.OperationsOwner.Delete(TargetPath));
        bool cleanupFrozen = operationCount == fault.Operations.Count
            && string.Equals(beforeRejectedCleanup, fault.OperationsOwner.CaptureSnapshot().ToString(), StringComparison.Ordinal);
        Assert.IsTrue(cleanupFrozen);

        TrxPhase3WriteRole role = baselineOperation.Kind == TrxFileOperationKind.Write
            ? InferWriteRole(scenario, baseline, baselineOperation)
            : TrxPhase3WriteRole.None;
        TrxDocumentObservation classification = ClassifyBest(scenario, fault.TargetBytes, baselineOperation, cut);
        int flushes = fault.Operations.Count(operation => operation.Kind == TrxFileOperationKind.Flush);
        long offset = baselineOperation.PrePosition ?? 0;
        string diagnostic = CreateStableDiagnostic(
            scenario.Name,
            dimension,
            baselineOperation.OperationIndex,
            baselineOperation.Kind,
            role,
            offset,
            baselineOperation.RequestedByteCount,
            cut,
            fault.TargetPresent,
            fault.TargetBytes,
            flushes,
            classification.Classification);
        return new TrxPhase3EvidenceObservation
        {
            Scenario = scenario.Name,
            Dimension = dimension,
            OperationIndex = baselineOperation.OperationIndex,
            OperationKind = baselineOperation.Kind,
            WriteRole = role,
            Offset = offset,
            RequestedBytes = baselineOperation.RequestedByteCount,
            Cut = cut,
            TargetLength = fault.TargetBytes.Length,
            FlushCount = flushes,
            TargetPresent = fault.TargetPresent,
            CleanupWasFrozen = cleanupFrozen,
            Classification = classification.Classification,
            Diagnostic = diagnostic,
        };
    }

    private static byte[] CreateExpectedFrozenBytes(
        TrxPhase3Run baseline,
        TrxFileOperationRecord operation,
        int? cut,
        out bool targetPresent)
    {
        TrxVirtualFileSystemSnapshot before = operation.OperationIndex == 0
            ? baseline.PreActSnapshot
            : baseline.Snapshots[operation.OperationIndex - 1];
        TrxVirtualFileSystemSnapshot after = baseline.Snapshots[operation.OperationIndex];
        targetPresent = after.Contains(TargetPath);
        if (!targetPresent)
        {
            return [];
        }

        if (operation.Kind != TrxFileOperationKind.Write || cut is null)
        {
            return after.GetFileBytes(TargetPath);
        }

        byte[] beforeBytes = before.Contains(TargetPath) ? before.GetFileBytes(TargetPath) : [];
        byte[] afterBytes = after.GetFileBytes(TargetPath);
        int committed = cut.Value;
        long offset = operation.PrePosition!.Value;
        long requiredLength = offset + committed;
        if (requiredLength > beforeBytes.LongLength)
        {
            Array.Resize(ref beforeBytes, checked((int)requiredLength));
        }

        if (committed > 0)
        {
            Array.Copy(afterBytes, offset, beforeBytes, offset, committed);
        }

        return beforeBytes;
    }

    private static TrxDocumentObservation ClassifyBest(
        TrxPhase3Scenario scenario,
        byte[] bytes,
        TrxFileOperationRecord? operation,
        int? cut,
        bool useBeforeOnly = false)
    {
        var beforeContext = new TrxDocumentObservationContext
        {
            OperationIndex = operation?.OperationIndex,
            OperationKind = operation?.Kind.ToString(),
            CommittedByteCount = cut,
            PreviousTargetLength = operation?.PreLength,
            PublishedEventSequenceNumber = 0,
            LatestEventSequenceNumber = 1,
        };
        TrxDocumentObservation before = TrxDocumentClassifier.Classify(
            bytes,
            scenario.BeforeExpectation,
            beforeContext,
            scenario.RecoveryBytes);
        if (useBeforeOnly || before.Classification == TrxDocumentClassification.Truthful)
        {
            return before;
        }

        var afterContext = new TrxDocumentObservationContext
        {
            OperationIndex = operation?.OperationIndex,
            OperationKind = operation?.Kind.ToString(),
            CommittedByteCount = cut,
            PreviousTargetLength = operation?.PreLength,
            PublishedEventSequenceNumber = 1,
            LatestEventSequenceNumber = 1,
        };
        TrxDocumentObservation after = TrxDocumentClassifier.Classify(
            bytes,
            scenario.AfterExpectation,
            afterContext,
            scenario.RecoveryBytes);
        return after.Classification == TrxDocumentClassification.Truthful
            ? after
            : before.Classification == TrxDocumentClassification.Repairable
                ? before
                : after.Classification == TrxDocumentClassification.Repairable
                    ? after
                    : before.Classification == TrxDocumentClassification.ParseableInconsistent
                        ? before
                        : after.Classification == TrxDocumentClassification.ParseableInconsistent ? after : before;
    }

    private static TrxPhase3WriteRole InferWriteRole(
        TrxPhase3Scenario scenario,
        TrxPhase3Run baseline,
        TrxFileOperationRecord operation)
    {
        byte[] written = GetWrittenBytes(baseline, operation);
        string text;
        try
        {
            text = StrictUtf8.GetString(written);
        }
        catch (DecoderFallbackException)
        {
            return TrxPhase3WriteRole.Unknown;
        }

        return written switch
        {
            _ when scenario.Name.StartsWith("startup-", StringComparison.Ordinal)
                => TrxPhase3WriteRole.StartupDocument,
            _ when written.Length == CounterWidth
                && written.All(value => value is >= (byte)'0' and <= (byte)'9')
                => TrxPhase3WriteRole.Counter,
            _ when written.Length == 10
                && (text.StartsWith("Completed\"", StringComparison.Ordinal)
                    || text.StartsWith("Failed\"", StringComparison.Ordinal))
                => TrxPhase3WriteRole.Outcome,
            _ when DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _)
                => TrxPhase3WriteRole.Timestamp,
            _ when written.All(value => value == (byte)' ')
                && written.Length == scenario.RunningSlotByteCapacity
                => TrxPhase3WriteRole.RunningSlotClear,
            _ when text.Contains("<UnitTestResult", StringComparison.Ordinal)
                && text.Contains("outcome=\"InProgress\"", StringComparison.Ordinal)
                && written.Length == scenario.RunningSlotByteCapacity
                => TrxPhase3WriteRole.RunningSlot,
            _ when text.Contains("</Results></TestRun>", StringComparison.Ordinal)
                => TrxPhase3WriteRole.ResultTail,
            _ when text.StartsWith("<UnitTest ", StringComparison.Ordinal)
                => TrxPhase3WriteRole.Definition,
            _ when text.StartsWith("<TestEntry ", StringComparison.Ordinal)
                => TrxPhase3WriteRole.Entry,
            _ when text.StartsWith("<CollectorDataEntries", StringComparison.Ordinal)
                || text.StartsWith("<RunInfos", StringComparison.Ordinal)
                => TrxPhase3WriteRole.Summary,
            _ => TrxPhase3WriteRole.Unknown,
        };
    }

    private static byte[] GetWrittenBytes(TrxPhase3Run baseline, TrxFileOperationRecord operation)
    {
        byte[] after = baseline.Snapshots[operation.OperationIndex].GetFileBytes(TargetPath);
        byte[] written = new byte[operation.RequestedByteCount];
        Array.Copy(after, operation.PrePosition!.Value, written, 0, written.Length);
        return written;
    }

    private static IReadOnlyList<int> CreateSelectedCuts(byte[] bytes)
    {
        var cuts = new SortedSet<int>
        {
            0,
            Math.Min(1, bytes.Length),
            Math.Max(0, bytes.Length - 1),
            bytes.Length,
        };
        AddNearby(cuts, bytes.Length, 512);
        AddNearby(cuts, bytes.Length, 4_096);

        List<int> utf8Boundaries = [];
        List<int> delimiters = [];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte value = bytes[i];
            if ((value & 0xC0) == 0xC0 || (i > 0 && (value & 0xC0) != 0x80 && (bytes[i - 1] & 0x80) != 0))
            {
                utf8Boundaries.Add(i);
            }

            if (value is (byte)'<' or (byte)'>' or (byte)'&' or (byte)';' or (byte)'"')
            {
                delimiters.Add(i);
                delimiters.Add(i + 1);
            }
        }

        AddFirstAndLast(cuts, utf8Boundaries, bytes.Length);
        AddFirstAndLast(cuts, delimiters, bytes.Length);
        return [.. cuts];
    }

    private static void AddNearby(SortedSet<int> cuts, int length, int boundary)
    {
        for (int delta = -1; delta <= 1; delta++)
        {
            int value = boundary + delta;
            if (value >= 0 && value <= length)
            {
                _ = cuts.Add(value);
            }
        }
    }

    private static void AddFirstAndLast(SortedSet<int> cuts, IReadOnlyList<int> candidates, int length)
    {
        foreach (int value in candidates.Take(6).Concat(candidates.Skip(Math.Max(0, candidates.Count - 6))))
        {
            for (int delta = -1; delta <= 1; delta++)
            {
                int candidate = value + delta;
                if (candidate >= 0 && candidate <= length)
                {
                    _ = cuts.Add(candidate);
                }
            }
        }
    }

    private static string CreateStableDiagnostic(
        string scenario,
        TrxPhase3EvidenceDimension dimension,
        int operationIndex,
        TrxFileOperationKind? operationKind,
        TrxPhase3WriteRole role,
        long offset,
        int requestedBytes,
        int? cut,
        bool targetPresent,
        byte[] targetBytes,
        int flushCount,
        TrxDocumentClassification classification)
        => string.Format(
            CultureInfo.InvariantCulture,
            "scenario={0}; dimension={1}; operation={2:D3}:{3}; role={4}; offset={5}; requested={6}; cut={7}; targetPresent={8}; targetLength={9}; flushes={10}; classification={11}; window={12}",
            scenario,
            dimension,
            operationIndex,
            operationKind?.ToString() ?? "-",
            role,
            offset,
            requestedBytes,
            cut?.ToString(CultureInfo.InvariantCulture) ?? "-",
            targetPresent,
            targetBytes.Length,
            flushCount,
            classification,
            CreateHexWindow(targetBytes, offset + (cut ?? 0)));

    private static string CreateHexWindow(byte[] bytes, long center)
    {
        const int windowSize = 16;
        int boundedCenter = checked((int)Math.Min(Math.Max(center, 0), bytes.LongLength));
        int start = Math.Max(0, boundedCenter - (windowSize / 2));
        if (start + windowSize > bytes.Length)
        {
            start = Math.Max(0, bytes.Length - windowSize);
        }

        int count = Math.Min(windowSize, bytes.Length - start);
        byte[] window = new byte[count];
        Array.Copy(bytes, start, window, 0, count);
        return $"{start}:{BitConverter.ToString(window).Replace("-", string.Empty)}";
    }

    private static string ComputeDigest(IEnumerable<string> diagnostics)
    {
        byte[] input = Encoding.UTF8.GetBytes(string.Join("\n", diagnostics));
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", string.Empty);
    }

    private sealed class TrxPhase3Scenario
    {
        public TrxPhase3Scenario(
            string name,
            TrxDocumentExpectation beforeExpectation,
            TrxDocumentExpectation afterExpectation,
            Action<TrxIncrementalWriterPrototype> setup,
            Action<TrxIncrementalWriterPrototype> act,
            int definitionPadBytes = 4_096,
            int entryPadBytes = 2_048,
            int summaryPadBytes = 2_048,
            int runningSlotCount = 2,
            int runningSlotByteCapacity = 320,
            byte[]? seedTarget = null,
            byte[]? recoveryBytes = null,
            IReadOnlyList<TrxPhase3WriteRole>? exhaustiveRoles = null,
            IReadOnlyList<TrxPhase3WriteRole>? selectedRoles = null)
        {
            Name = name;
            BeforeExpectation = beforeExpectation;
            AfterExpectation = afterExpectation;
            Setup = setup;
            Act = act;
            DefinitionPadBytes = definitionPadBytes;
            EntryPadBytes = entryPadBytes;
            SummaryPadBytes = summaryPadBytes;
            RunningSlotCount = runningSlotCount;
            RunningSlotByteCapacity = runningSlotByteCapacity;
            SeedTarget = seedTarget;
            RecoveryBytes = recoveryBytes;
            ExhaustiveRoles = [.. exhaustiveRoles ?? []];
            SelectedRoles = [.. selectedRoles ?? []];
        }

        public string Name { get; }

        public TrxDocumentExpectation BeforeExpectation { get; }

        public TrxDocumentExpectation AfterExpectation { get; }

        public Action<TrxIncrementalWriterPrototype> Setup { get; }

        public Action<TrxIncrementalWriterPrototype> Act { get; }

        public int DefinitionPadBytes { get; }

        public int EntryPadBytes { get; }

        public int SummaryPadBytes { get; }

        public int RunningSlotCount { get; }

        public int RunningSlotByteCapacity { get; }

        public byte[]? SeedTarget { get; }

        public byte[]? RecoveryBytes { get; }

        public HashSet<TrxPhase3WriteRole> ExhaustiveRoles { get; }

        public HashSet<TrxPhase3WriteRole> SelectedRoles { get; }
    }

    private sealed class TrxPhase3Run
    {
        public TrxPhase3Run(
            TrxFaultInjectingFileOperations operations,
            TrxVirtualFileSystemSnapshot preActSnapshot,
            TrxVirtualFileSystemSnapshot frozenSnapshot,
            bool completed,
            byte[] targetBytes)
        {
            OperationsOwner = operations;
            PreActSnapshot = preActSnapshot;
            FrozenSnapshot = frozenSnapshot;
            Completed = completed;
            TargetBytes = targetBytes;
        }

        public TrxFaultInjectingFileOperations OperationsOwner { get; }

        public TrxVirtualFileSystemSnapshot PreActSnapshot { get; }

        public TrxVirtualFileSystemSnapshot FrozenSnapshot { get; }

        public bool Completed { get; }

        public bool TargetPresent => FrozenSnapshot.Contains(TargetPath);

        public byte[] TargetBytes { get; }

        public IReadOnlyList<TrxFileOperationRecord> Operations => OperationsOwner.Operations;

        public IReadOnlyList<TrxVirtualFileSystemSnapshot> Snapshots => OperationsOwner.Snapshots;
    }
}
