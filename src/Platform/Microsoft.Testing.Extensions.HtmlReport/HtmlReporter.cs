// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReporter :
    ITestSessionLifetimeHandler,
    IDataConsumer,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private const string DeterministicBuildRoot = "/_/";

    private readonly Run _run = new();

    private readonly IOutputDevice _outputDisplay;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private static readonly char[] NewlineCharacters = ['\r', '\n'];
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private DateTimeOffset _startTime;
    private Project _project;

    public HtmlReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IClock clock)
    {
        _commandLine = commandLine;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDisplay = outputDisplay;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<HtmlReporter>();
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid { get; } = nameof(HtmlReporter);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = HtmlResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = HtmlResources.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
    {
        bool isEnabledByParameter = _commandLine.IsOptionSet(HtmlCommandLineOptions.HtmlOptionName);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{nameof(HtmlReport)} is {(isEnabledByParameter ? "enabled" : "disabled")}.");
        }

        if (!isEnabledByParameter)
        {
            return Task.FromResult(false);
        }

        _startTime = _clock.UtcNow;

        _project = new Project
        {
            Name = _testApplicationModuleInfo.TryGetAssemblyName(),
            Path = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
        };

        _run.Projects.Add(_project);

        return Task.FromResult(true);
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (value is not TestNodeUpdateMessage nodeUpdateMessage)
        {
            return;
        }

        TestNodeStateProperty? nodeState = nodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();

        switch (nodeState)
        {
            case PassedTestNodeStateProperty passed:

                break
            case SkippedTestNodeStateProperty skipped:
                break;
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(failed.Explanation, failed.Exception).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(error.Explanation, error.Exception).ConfigureAwait(false);
                break;
            case CancelledTestNodeStateProperty cancelled:
                await WriteExceptionAsync(cancelled.Explanation, cancelled.Exception).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(timeout.Explanation, timeout.Exception).ConfigureAwait(false);
                break;
        }
    }

    private async Task WriteExceptionAsync(string? explanation, Exception? exception)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Failure received.");
        }

        string? line = GetErrorText(explanation, exception, _severity, _fileSystem, _logger);
        if (line == null)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Failure message is null, returning.");
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"Showing failure message '{line}'.");
        }

        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(line)).ConfigureAwait(false);
    }

    internal static /* for testing */ string? GetErrorText(string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger)
    {
        if (exception == null || exception.StackTrace == null)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Exception or stack trace were null, returning.");
            }

            return null;
        }

        string message = explanation ?? exception.Message;

        if (message == null)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Explanation and exception message were null, returning.");
            }

            return null;
        }

        string repoRoot = RootFinder.Find();
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"Found repo root '{repoRoot}'");
        }

        string stackTrace = exception.StackTrace;
        foreach (string? stackFrame in stackTrace.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            (string Code, string File, int LineNumber)? location = GetStackFrameLocation(stackFrame);
            if (location == null)
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("StackFrame location was null, continuing to next.");
                }

                continue;
            }

            string file = location.Value.File;

            // TODO: We need better rule for stackframes to opt out from being interesting.
            if (file.EndsWith("Assert.cs", StringComparison.Ordinal))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("StackFrame location ends with 'Assert.cs' this is a special pattern that we skip, continuing to next.");
                }

                continue;
            }

            // Deterministic build paths start with "/_/"
            string relativePath;
            if (file.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{file}' is coming from deterministic build.");
                }

                relativePath = file.Substring(DeterministicBuildRoot.Length);

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Using relative path '{relativePath}'.");
                }
            }
            else if (file.StartsWith(repoRoot, StringComparison.CurrentCultureIgnoreCase))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{file}' is in current repo '{repoRoot}'.");
                }

                relativePath = file.Substring(repoRoot.Length);
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Using relative path '{relativePath}'.");
                }
            }
            else
            {
                // Path does not belong to current repo, keep it null.
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{file}' does not belong to current repo '{repoRoot}'. Continue to next.");
                }

                continue;
            }

            // Combine with repo root, to be able to resolve deterministic build paths.
            string fullPath = Path.Combine(repoRoot, relativePath);
            if (!fileSystem.Exists(fullPath))
            {
                // Path does not belong to current repository or does not exist, no need to report it because it will not show up in the PR error, we will only see it details of the run, which is the same
                // as not reporting it this way. Maybe there can be 2 modes, but right now we want this to be usable for GitHub + AzDo, not for pure AzDo.
                //
                // In case of deterministic build, all the paths will be relative, so if library carries symbols and matches our path we would see the error as coming from our file
                // even though it would not. That change is slim and something we have to live with.
                //
                // Deterministic build will also have paths normalized to /, luckily File.Exist does not care about the slash direction (on Windows).
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{fullPath}' does not exist on disk. Continue to next.");
                }

                continue;
            }

            // The slashes must be / for GitHub to render the error placement correctly.
            string relativeNormalizedPath = relativePath.Replace('\\', '/');
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Normalized path for GitHub '{relativeNormalizedPath}'.");
            }

            string err = AzDoEscaper.Escape(message);

            string line = $"##vso[task.logissue type={severity};sourcepath={relativeNormalizedPath};linenumber={location.Value.LineNumber};columnnumber=1]{err}";
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Reported full message '{line}'.");
            }

            // Report the error only for the first stack frame that is useful.
            return line;
        }

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"No stack trace line matched criteria, no failure line was reported.");
        }

        return null;
    }

    private static (string Code, string File, int LineNumber)? GetStackFrameLocation(string stackTraceLine)
    {
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            return null;
        }

        string code = match.Groups["code"].Value;
        bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(code);
        if (!weHaveFilePathAndCodeLine)
        {
            return null;
        }

        string file = match.Groups["file"].Value;
        if (RoslynString.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;

        return (code, file, line);
    }

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => throw new NotImplementedException();
}
