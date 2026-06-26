// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        (string finalPath, bool fileNameExplicitlyProvided) = ResolveOutputPath(
            CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName,
            () => BuildDefaultFileName(finishTime));

        byte[] bytes = BuildCtrfJson(results, finishTime);

        return await WriteWithRetryAsync(finalPath, bytes, fileNameExplicitlyProvided).ConfigureAwait(false);
    }
}
