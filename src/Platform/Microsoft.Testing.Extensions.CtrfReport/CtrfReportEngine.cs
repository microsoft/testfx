// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    // CTRF spec: https://github.com/ctrf-io/ctrf
    private const string CtrfReportFormat = "CTRF";
    private const string CtrfSpecVersion = "0.0.0";

    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ITestFramework _testFramework;
    private readonly DateTimeOffset _testStartTime;
    private readonly int _exitCode;
    private readonly CancellationToken _cancellationToken;

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
    {
        _fileSystem = fileSystem;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _clock = clock;
        _testFramework = testFramework;
        _testStartTime = testStartTime;
        _exitCode = exitCode;
        _cancellationToken = cancellationToken;
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
            ? ResolveJsonFileName(GetProvidedFileName(providedFileName))
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

    private static string GetProvidedFileName(string[]? providedFileName)
        => providedFileName is { Length: > 0 }
            ? providedFileName[0]
            : throw ApplicationStateGuard.Unreachable();
}
