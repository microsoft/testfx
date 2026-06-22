// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    /// <summary>
    /// Builds the default report file name using the deterministic
    /// <c>&lt;asm&gt;_&lt;tfm&gt;_&lt;arch&gt;.&lt;extension&gt;</c> shape.
    /// A second run into the same TestResults folder overwrites the previous file
    /// (with a warning), matching the behaviour of an explicitly-provided file name.
    /// </summary>
    protected string BuildDefaultFileName(string extension)
    {
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker();
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string raw = $"{moduleName}_{targetFrameworkMoniker}_{architecture}.{extension}";
        return ReportFileNameSanitizer.ReplaceInvalidFileNameChars(raw);
    }
}
