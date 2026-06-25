// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared base class for report engine implementations (CTRF, JUnit, HTML, ...) that all consume
/// the same set of platform services (file system, configuration, clock, ...) and need a common
/// way to extract the user-provided report file name from a parsed command-line argument list.
/// </summary>
internal abstract class ReportEngineBase
{
#pragma warning disable SA1401 // Fields should be private - intentional: derived report engines access these directly through their partial files.
    protected readonly IFileSystem _fileSystem;
    protected readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    protected readonly IEnvironment _environment;
    protected readonly ICommandLineOptions _commandLineOptions;
    protected readonly IConfiguration _configuration;
    protected readonly IClock _clock;
    protected readonly ITestFramework _testFramework;
    protected readonly DateTimeOffset _testStartTime;
    protected readonly int _exitCode;
    protected readonly CancellationToken _cancellationToken;
#pragma warning restore SA1401

    protected ReportEngineBase(
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

    protected static string GetProvidedFileName(string[]? providedFileName)
        => providedFileName is { Length: > 0 }
            ? providedFileName[0]
            : throw ApplicationStateGuard.Unreachable();

    protected static string ReplaceInvalidFileNameChars(string fileName)
        => ReportFileNameSanitizer.ReplaceInvalidFileNameChars(fileName);

    protected string ResolveProvidedFileName(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        return ReportFileNameHelper.ResolveAndSanitize(template, processName, processId, _clock.UtcNow);
    }

    protected string BuildDefaultFileName(string extension)
    {
        // Deterministic <asm>_<tfm>_<arch>.<extension> shape — discoverable across
        // reruns and multi-target/multi-arch matrices. A second run into the same
        // TestResults folder overwrites the previous file (with a warning), matching
        // the behavior of an explicitly-provided file name.
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker();
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string raw = $"{moduleName}_{targetFrameworkMoniker}_{architecture}.{extension}";
        return ReplaceInvalidFileNameChars(raw);
    }

    protected (string FinalPath, bool WasExplicit) ResolveOutputPath(string fileNameOptionName, string extension)
        => ResolveOutputPath(fileNameOptionName, () => BuildDefaultFileName(extension));

    protected (string FinalPath, bool WasExplicit) ResolveOutputPath(string fileNameOptionName, Func<string> defaultFileNameFactory)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool wasExplicit = _commandLineOptions.TryGetOptionArgumentList(fileNameOptionName, out string[]? providedFileName);
        string fileName = wasExplicit
            ? ResolveProvidedFileName(GetProvidedFileName(providedFileName))
            : defaultFileNameFactory();

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

        return (finalPath, wasExplicit);
    }
}
