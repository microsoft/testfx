// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.JUnitReport;

#pragma warning disable RS0051 // Crash-recovery serialization is an implementation detail.

internal sealed class JUnitReportGenerator : ReportGeneratorBase<JUnitReportGenerator, CapturedTestResult>
{
    internal const string JUnitArtifactKind = "microsoft.testing.junit";
    internal const string JournalEnvironmentVariableName = "TESTINGPLATFORM_JUNITREPORT_JOURNAL";

    // Parent chain for ALL TestNodeUpdateMessages (including Discovered / InProgress).
    // Keyed by the TestNodeUid value after truncation to TestResultCaptureHelper.MaxIdentityFieldLength
    // so it matches the capped RawUid / ParentRawUid keys used everywhere else in capture
    // (see TestResultCapture.GetParentChainEntry / TryCapture). The engine uses this to
    // reconstruct the testpath of every test case in the report.
    // MTP guarantees ConsumeAsync (and therefore OnTestNodeUpdate) is called sequentially
    // for a given consumer instance, so Dictionary<TKey, TValue> is safe here without locking.
    private readonly Dictionary<string, TestResultCapture.ParentChainEntry> _parentChain = [];

    public JUnitReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, JUnitReportGeneratorCommandLine.JUnitReportOptionName, JournalEnvironmentVariableName)
    {
    }

    internal JUnitReportGenerator(IServiceProvider serviceProvider, RecoveredReportMetadata recoveredMetadata)
        : base(serviceProvider, JUnitReportGeneratorCommandLine.JUnitReportOptionName, recoveredMetadata)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(JUnitReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.JUnitReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.JUnitReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.JUnitReportArtifactDisplayName;

    protected override string? ArtifactKind => JUnitArtifactKind;

    protected override string ArtifactDescription => ExtensionResources.JUnitReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating JUnit XML report for {testResultCount} test result(s).";

    protected override string SerializeJournalRecord(ReportJournalRecord<CapturedTestResult> record)
        => JsonSerializer.Serialize(record, typeof(ReportJournalRecord<CapturedTestResult>), ReportJournalJsonSerializerContext.Default);

    internal static ReportJournalRecord<CapturedTestResult>? DeserializeJournalRecord(string json)
        => (ReportJournalRecord<CapturedTestResult>?)JsonSerializer.Deserialize(
            json,
            typeof(ReportJournalRecord<CapturedTestResult>),
            ReportJournalJsonSerializerContext.Default);

    protected override async Task OnTestNodeUpdateAsync(TestNodeUpdateMessage update, CancellationToken cancellationToken)
    {
        // Record the parent chain entry for EVERY update so non-terminal parent
        // nodes (Discovered / InProgress) are still available when reconstructing
        // the path of a terminal child test. Later updates for the same UID just
        // refresh the entry (frameworks may emit several updates per node).
        // The raw UID is test-controlled and unbounded by the platform, so we
        // truncate it to a fixed identity budget before using it as a dictionary
        // key. Capture-side `RawUid`/`ParentRawUid` values are truncated to the
        // same budget so cross-lookups remain consistent.
        string rawUid = TestResultCaptureHelper.Truncate(update.TestNode.Uid.Value, TestResultCaptureHelper.MaxIdentityFieldLength)!;
        _parentChain[rawUid] = TestResultCapture.GetParentChainEntry(update);

        await base.OnTestNodeUpdateAsync(update, cancellationToken).ConfigureAwait(false);
    }

    protected override ReportJournalParentEntry? CaptureParentEntry(TestNodeUpdateMessage update)
    {
        string rawUid = TestResultCaptureHelper.Truncate(update.TestNode.Uid.Value, TestResultCaptureHelper.MaxIdentityFieldLength)!;
        TestResultCapture.ParentChainEntry parent = TestResultCapture.GetParentChainEntry(update);
        return new ReportJournalParentEntry
        {
            Uid = rawUid,
            DisplayName = parent.DisplayName,
            ParentUid = parent.ParentRawUid,
        };
    }

    protected override void RestoreParentEntry(ReportJournalParentEntry parent)
        => _parentChain[parent.Uid] = new TestResultCapture.ParentChainEntry(parent.DisplayName, parent.ParentUid);

    protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
        // A test framework that retries a test in-process reports every attempt under the same test node uid.
        // JUnit has no notion of attempts: keeping the superseded ones would add extra <testcase> elements (renamed
        // "[attempt N]" by JUnitSuiteBuilder) and inflate the suite totals, so only the final attempt is captured.
        // CTRF and the HTML report deliberately do the opposite and keep the whole history.
        => update.TestNode.IsSupersededRetryAttempt()
            ? null
            : TestResultCapture.TryCapture(update);

    protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
        => new JUnitReportEngine(CreateReportEngineContext(testStartTime, exitCode, cancellationToken)).GenerateReportAsync(tests, _parentChain);
}

[JsonSerializable(typeof(ReportJournalRecord<CapturedTestResult>))]
internal sealed partial class ReportJournalJsonSerializerContext : JsonSerializerContext;

#pragma warning restore RS0051
