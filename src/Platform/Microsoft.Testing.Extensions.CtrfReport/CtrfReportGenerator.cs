// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.CtrfReport;

#pragma warning disable RS0051 // Crash-recovery serialization is an implementation detail.

internal sealed class CtrfReportGenerator : ReportGeneratorBase<CtrfReportGenerator, CapturedTestResult>
{
    internal const string CtrfArtifactKind = "microsoft.testing.ctrf";
    internal const string JournalEnvironmentVariableName = "TESTINGPLATFORM_CTRFREPORT_JOURNAL";

    public CtrfReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, CtrfReportGeneratorCommandLine.CtrfReportOptionName, JournalEnvironmentVariableName)
    {
    }

    internal CtrfReportGenerator(IServiceProvider serviceProvider, RecoveredReportMetadata recoveredMetadata)
        : base(serviceProvider, CtrfReportGeneratorCommandLine.CtrfReportOptionName, recoveredMetadata)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(CtrfReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.CtrfReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.CtrfReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.CtrfReportArtifactDisplayName;

    protected override string? ArtifactKind => CtrfArtifactKind;

    protected override string ArtifactDescription => ExtensionResources.CtrfReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating CTRF report for {testResultCount} test result(s).";

    protected override string SerializeJournalRecord(ReportJournalRecord<CapturedTestResult> record)
        => JsonSerializer.Serialize(record, typeof(ReportJournalRecord<CapturedTestResult>), ReportJournalJsonSerializerContext.Default);

    internal static ReportJournalRecord<CapturedTestResult>? DeserializeJournalRecord(string json)
        => (ReportJournalRecord<CapturedTestResult>?)JsonSerializer.Deserialize(
            json,
            typeof(ReportJournalRecord<CapturedTestResult>),
            ReportJournalJsonSerializerContext.Default);

    protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
        => TestResultCapture.TryCapture(update.TestNode);

    protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
        => new CtrfReportEngine(CreateReportEngineContext(testStartTime, exitCode, cancellationToken)).GenerateReportAsync(tests);
}

[JsonSerializable(typeof(ReportJournalRecord<CapturedTestResult>))]
internal sealed partial class ReportJournalJsonSerializerContext : JsonSerializerContext;

#pragma warning restore RS0051
