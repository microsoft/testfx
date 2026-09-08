// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

#pragma warning disable RS0051 // Crash-recovery serialization is an implementation detail.

internal sealed class HtmlReportGenerator : ReportGeneratorBase<HtmlReportGenerator, CapturedTestResult>
{
    internal const string HtmlArtifactKind = "microsoft.testing.html";
    internal const string JournalEnvironmentVariableName = "TESTINGPLATFORM_HTMLREPORT_JOURNAL";

    public HtmlReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, HtmlReportGeneratorCommandLine.HtmlReportOptionName, JournalEnvironmentVariableName)
    {
    }

    internal HtmlReportGenerator(IServiceProvider serviceProvider, RecoveredReportMetadata recoveredMetadata)
        : base(serviceProvider, HtmlReportGeneratorCommandLine.HtmlReportOptionName, recoveredMetadata)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(HtmlReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.HtmlReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.HtmlReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.HtmlReportArtifactDisplayName;

    protected override string? ArtifactKind => HtmlArtifactKind;

    protected override string ArtifactDescription => ExtensionResources.HtmlReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating HTML report for {testResultCount} test result(s).";

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
        => new HtmlReportEngine(CreateReportEngineContext(testStartTime, exitCode, cancellationToken)).GenerateReportAsync(tests);
}

[JsonSerializable(typeof(ReportJournalRecord<CapturedTestResult>))]
internal sealed partial class ReportJournalJsonSerializerContext : JsonSerializerContext;

#pragma warning restore RS0051
