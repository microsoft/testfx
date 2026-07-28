// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportGenerator : ReportGeneratorBase<CtrfReportGenerator, CapturedTestResult>
{
    public CtrfReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, CtrfReportGeneratorCommandLine.CtrfReportOptionName)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(CtrfReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.CtrfReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.CtrfReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.CtrfReportArtifactDisplayName;

    protected override string? ArtifactKind => "microsoft.testing.ctrf";

    protected override string ArtifactDescription => ExtensionResources.CtrfReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating CTRF report for {testResultCount} test result(s).";

    protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
        => TestResultCapture.TryCapture(update.TestNode);

    protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
        => new CtrfReportEngine(CreateReportEngineContext(testStartTime, exitCode, cancellationToken)).GenerateReportAsync(tests);
}
