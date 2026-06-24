// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportGenerator : ReportGeneratorBase<HtmlReportGenerator, CapturedTestResult>
{
    public HtmlReportGenerator(IServiceProvider serviceProvider)
        : base(serviceProvider, HtmlReportGeneratorCommandLine.HtmlReportOptionName)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(HtmlReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.HtmlReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.HtmlReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.HtmlReportArtifactDisplayName;

    protected override string ArtifactDescription => ExtensionResources.HtmlReportArtifactDescription;

    protected override string GetGenerationLogMessage(int testResultCount)
        => $"Generating HTML report for {testResultCount} test result(s).";

    protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
        => TestResultCapture.TryCapture(update.TestNode);

    protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
    {
        var engine = new HtmlReportEngine(
            FileSystem,
            TestApplicationModuleInfo,
            Environment,
            CommandLineOptions,
            Configuration,
            Clock,
            TestFramework,
            testStartTime,
            exitCode,
            cancellationToken);
        return engine.GenerateReportAsync(tests);
    }
}
