// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine : ReportEngineBase
{
    // CTRF spec: https://github.com/ctrf-io/ctrf
    private const string CtrfReportFormat = "CTRF";
    private const string CtrfSpecVersion = "0.0.0";

    public CtrfReportEngine(
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IEnvironment environment,
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IClock clock,
        ITestFramework testFramework,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
        : base(
            fileSystem,
            testApplicationModuleInfo,
            environment,
            commandLineOptions,
            configuration,
            clock,
            testFramework,
            testStartTime,
            exitCode,
            cancellationToken)
    {
    }

    public Task<(string FileName, string? Warning)> GenerateReportAsync(CapturedTestResult[] results)
        => GenerateReportCoreAsync(results, _clock.UtcNow);

    private async Task<(string FileName, string? Warning)> GenerateReportCoreAsync(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool fileNameExplicitlyProvided = _commandLineOptions.TryGetOptionArgumentList(
            CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? ResolveProvidedFileName(GetProvidedFileName(providedFileName))
            : BuildDefaultFileName(finishTime);

        string outputDirectory = _configuration.GetTestResultDirectory();
        // Path.Combine short-circuits when the second argument is rooted, so an absolute
        // user-provided file name overrides the test results directory while validated
        // relative paths stay nested under it.
        string finalPath = Path.Combine(outputDirectory, fileName);
        string? finalDirectory = Path.GetDirectoryName(finalPath);
        if (!RoslynString.IsNullOrEmpty(finalDirectory))
        {
            _fileSystem.CreateDirectory(finalDirectory);
        }

        byte[] bytes = BuildCtrfJson(results, finishTime);

        return await WriteWithRetryAsync(finalPath, bytes, fileNameExplicitlyProvided).ConfigureAwait(false);
    }
}
