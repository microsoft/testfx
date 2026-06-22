// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportEngine : ReportEngineBase
{
    // Default cap on the rendered testpath property. Test trees can in theory be very
    // deep, and each level contributes a (capped) display name to the path, so we put
    // an additional ceiling on the rendered string to keep the XML output bounded.
    internal const int MaxTestPathLength = 64 * 1024;

    public JUnitReportEngine(
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

    public Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain)
        => GenerateReportCoreAsync(results, parentChain, _clock.UtcNow);

    internal static string XmlSafeText(string? value)
        => JUnitXmlWriter.XmlSafeText(value);

    private async Task<(string FileName, string? Warning)> GenerateReportCoreAsync(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        DateTimeOffset finishTime)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool fileNameExplicitlyProvided = _commandLineOptions.TryGetOptionArgumentList(
            JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? ResolveXmlFileName(GetProvidedFileName(providedFileName))
            : BuildDefaultFileName("xml");

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

        // Two-pass strategy: build all suites and resolve testcase-name collisions in
        // memory first, then stream the XML once. This keeps the writer logic linear
        // and lets us know testsuite-level aggregates (tests/failures/...) up front.
        SuiteSet suites = new JUnitSuiteBuilder(_testApplicationModuleInfo, _testStartTime)
            .BuildSuites(results, parentChain, finishTime);

        return await WriteOutputAsync(finalPath, suites).ConfigureAwait(false);
    }

    private async Task<(string FileName, string? Warning)> WriteOutputAsync(
        string finalPath,
        SuiteSet suites)
    {
        // Stream-to-temp-then-rename: write to a unique "<final>.<random>.tmp" in the
        // same directory and atomically move it into place at the end. The random suffix
        // prevents concurrent runs that happen to produce the same default file name
        // (same second / same results dir) from clobbering each other's tmp file.
        string tempPath = finalPath + "." + Path.GetRandomFileName() + ".tmp";
        await new JUnitXmlWriter(_fileSystem, _environment, _testFramework, _exitCode, _cancellationToken)
            .WriteXmlAsync(tempPath, suites)
            .ConfigureAwait(false);

        // Always overwrite, regardless of whether the file name was explicitly provided or
        // generated from the default <asm>_<tfm>_<arch>.xml shape. Emit a warning when
        // overwriting so users have a single, predictable rule to reason about.
        bool willOverwrite = _fileSystem.ExistFile(finalPath);
        _fileSystem.MoveFile(tempPath, finalPath, overwrite: true);
        return (
            finalPath,
            willOverwrite
                ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.JUnitReportFileExistsAndWillBeOverwritten, finalPath)
                : null);
    }

    private string ResolveXmlFileName(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        return ReportFileNameHelper.ResolveAndSanitize(template, processName, processId, _clock.UtcNow);
    }

#pragma warning disable IDE0051 // Accessed by unit tests through reflection.
    private static string ReplaceInvalidFileNameChars(string fileName)
        => ReportFileNameSanitizer.ReplaceInvalidFileNameChars(fileName);
#pragma warning restore IDE0051
}
