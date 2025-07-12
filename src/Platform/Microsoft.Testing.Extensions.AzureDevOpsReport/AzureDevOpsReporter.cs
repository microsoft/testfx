// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsReporter :
    IDataConsumer,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private const string DeterministicBuildRoot = "/_/";

    private readonly IOutputDevice _outputDisplay;
    private readonly ILogger _logger;
    private static readonly char[] NewlineCharacters = ['\r', '\n'];
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private string _severity = "error";

    public AzureDevOpsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory)
    {
        _commandLine = commandLine;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDisplay = outputDisplay;
        _logger = loggerFactory.CreateLogger<AzureDevOpsReporter>();
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid => nameof(AzureDevOpsReporter);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = AzureDevOpsResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = AzureDevOpsResources.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
    {
        bool isEnabledByParameter = _commandLine.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{nameof(AzureDevOpsReport)} is {(isEnabledByParameter ? "enabled" : "disabled")}.");
        }

        if (!isEnabledByParameter)
        {
            return Task.FromResult(false);
        }

        bool isEnabledByEnvVariable = string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"TF_BUILD environment variable is {(isEnabledByEnvVariable ? "enabled. Will report errors to Azure DevOps, because we are running in CI." : "disabled. Will not report errors to Azure DevOps.")}.");
        }

        if (!isEnabledByEnvVariable)
        {
            return Task.FromResult(false);
        }

        bool found = _commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, out string[]? arguments);
        if (found && arguments?.Length > 0)
        {
            _severity = arguments[0].ToLowerInvariant();
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Severity is set to '{_severity}', by --report-azdo-severity parameter.");
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Severity is set to '{_severity}', you can override it by using --report-azdo-severity parameter.");
            }
        }

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
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(failed.Explanation, failed.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(error.Explanation, error.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case CancelledTestNodeStateProperty cancelled:
                await WriteExceptionAsync(cancelled.Explanation, cancelled.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(timeout.Explanation, timeout.Exception, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task WriteExceptionAsync(string? explanation, Exception? exception, CancellationToken cancellationToken)
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

        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
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
}
