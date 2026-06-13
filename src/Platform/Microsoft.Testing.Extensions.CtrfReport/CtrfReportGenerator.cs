// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportGenerator : ReportGeneratorBase<CtrfReportGenerator, CapturedTestResult>
{
    public CtrfReportGenerator(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IMessageBus messageBus,
        IClock clock,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITestFramework testFramework,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ILogger<CtrfReportGenerator> logger)
        : base(
            configuration,
            commandLineOptions,
            fileSystem,
            testApplicationModuleInfo,
            messageBus,
            clock,
            environment,
            outputDevice,
            testFramework,
            testApplicationProcessExitCode,
            logger,
            CtrfReportGeneratorCommandLine.CtrfReportOptionName)
    {
    }

    /// <inheritdoc />
    public override string Uid => nameof(CtrfReportGenerator);

    /// <inheritdoc />
    public override string DisplayName { get; } = ExtensionResources.CtrfReportGeneratorDisplayName;

    /// <inheritdoc />
    public override string Description { get; } = ExtensionResources.CtrfReportGeneratorDescription;

    protected override string ArtifactDisplayName => ExtensionResources.CtrfReportArtifactDisplayName;

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
    {
        var engine = new CtrfReportEngine(
            _fileSystem,
            _testApplicationModuleInfo,
            _environment,
            _commandLineOptions,
            _configuration,
            _clock,
            _testFramework,
            testStartTime,
            exitCode,
            cancellationToken);
        return engine.GenerateReportAsync(tests);
    }
}
