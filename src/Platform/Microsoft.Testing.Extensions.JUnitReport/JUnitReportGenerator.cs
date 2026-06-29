// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportGenerator : ReportGeneratorBase<JUnitReportGenerator, CapturedTestResult>
{
    // Parent chain for ALL TestNodeUpdateMessages (including Discovered / InProgress).
    // Keyed by the TestNodeUid value after truncation to TestResultCaptureHelper.MaxIdentityFieldLength
    // so it matches the capped RawUid / ParentRawUid keys used everywhere else in capture
    // (see TestResultCapture.GetParentChainEntry / TryCapture). The engine uses this to
    // reconstruct the testpath of every test case in the report.
    // MTP guarantees ConsumeAsync (and therefore OnTestNodeUpdate) is called sequentially
    // for a given consumer instance, so Dictionary<TKey, TValue> is safe here without locking.
    private readonly Dictionary<string, TestResultCapture.ParentChainEntry> _parentChain = [];

    public JUnitReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, JUnitReportGeneratorCommandLine.JUnitReportOptionName)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(JUnitReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.JUnitReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.JUnitReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.JUnitReportArtifactDisplayName;

    protected override string ArtifactDescription => ExtensionResources.JUnitReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating JUnit XML report for {testResultCount} test result(s).";

    protected override void OnTestNodeUpdate(TestNodeUpdateMessage update)
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

        base.OnTestNodeUpdate(update);
    }

    protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
        => TestResultCapture.TryCapture(update);

    protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
        => new JUnitReportEngine(CreateReportEngineContext(testStartTime, exitCode, cancellationToken)).GenerateReportAsync(tests, _parentChain);
}
