// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsReporter :
    IDataConsumer,
    IOutputDeviceDataProducer
{
    internal const double KnownFlakyFailureRateThreshold = 0.25;
    private const int MinSamplesForRegressionAnnotation = 5;
    private const string QuarantineBuildTagLine = "##vso[build.addbuildtag]has-quarantined-test-failure";
    private const string WarningSeverity = "warning";

    private readonly IOutputDevice _outputDisplay;
    private readonly ILogger _logger;
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IAzureDevOpsHistoryService _historyService;
    private readonly string _targetFrameworkMoniker;
    private string? _severity;
    private bool _demoteKnownFlaky;
    private QuarantineFile? _quarantineFile;
    private bool _hasLoadedEnabledConfiguration;
    private int _quarantineBuildTagEmitted;
    private Regex[]? _userStackFrameFilters;

    public AzureDevOpsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory,
        IAzureDevOpsHistoryService historyService)
    {
        _commandLine = commandLine;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDisplay = outputDisplay;
        _historyService = historyService;
        _logger = loggerFactory.CreateLogger<AzureDevOpsReporter>();
        _targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage)
    ];

    /// <inheritdoc />
    public string Uid => nameof(AzureDevOpsReporter);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

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

        bool isEnabledByEnvVariable = AzureDevOpsConstants.IsRunningInAzureDevOps(_environment);
        if (isEnabledByEnvVariable)
        {
            EnsureEnabledConfigurationLoaded();
        }

        bool annotationsEnabled = AzureDevOpsConstants.IsFeatureKnobEnabled(_commandLine, AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{AzureDevOpsConstants.TfBuildEnvironmentVariableName} environment variable is {(isEnabledByEnvVariable ? "enabled. Will report errors to Azure DevOps, because we are running in CI." : "disabled. Will not report errors to Azure DevOps.")}");
            _logger.LogTrace($"Severity is set to '{_severity ?? "error"}', you can override it by using --report-azdo-severity parameter.");
            _logger.LogTrace($"Failure annotations are {(annotationsEnabled ? "enabled" : "disabled")}, you can toggle them by using --report-azdo-annotations parameter.");
        }

        return Task.FromResult(isEnabledByEnvVariable && annotationsEnabled);
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is not TestNodeUpdateMessage nodeUpdateMessage)
        {
            return;
        }

        EnsureEnabledConfigurationLoaded();
        TestNodeStateProperty? nodeState = nodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        string testDisplayName = nodeUpdateMessage.TestNode.DisplayName;

        // Defer GetTestName() to failure branches only: for passing/skipped/in-progress tests
        // nodeState falls through the switch with no match and testName is never needed.
        switch (nodeState)
        {
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), failed.Explanation, failed.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), error.Explanation, error.Exception, cancellationToken).ConfigureAwait(false);
                break;
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            case CancelledTestNodeStateProperty cancelled:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), cancelled.Explanation, cancelled.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), timeout.Explanation, timeout.Exception, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task WriteExceptionAsync(string testDisplayName, string testName, string? explanation, Exception? exception, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Failure received.");
        }

        bool isQuarantined = _quarantineFile?.Matches(testName) == true;
        if (isQuarantined && Interlocked.Exchange(ref _quarantineBuildTagEmitted, 1) == 0)
        {
            await _outputDisplay.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(QuarantineBuildTagLine), cancellationToken).ConfigureAwait(false);
        }

        string severity = GetSeverity(testName, isQuarantined);
        string annotationSuffix = BuildAnnotationSuffix(testName, isQuarantined);
        string? line = GetErrorText(testDisplayName, explanation, exception, severity, _fileSystem, _logger, _targetFrameworkMoniker, annotationSuffix, _userStackFrameFilters, StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);
        if (line is null)
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

        await _outputDisplay.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker)
        => GetErrorText(testDisplayName, explanation, exception, severity, fileSystem, logger, targetFrameworkMoniker, additionalMessageSuffix: null, userStackFrameFilters: null, skipAssertionFrames: true);

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker, string? additionalMessageSuffix)
        => GetErrorText(testDisplayName, explanation, exception, severity, fileSystem, logger, targetFrameworkMoniker, additionalMessageSuffix, userStackFrameFilters: null, skipAssertionFrames: true);

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker, string? additionalMessageSuffix, Regex[]? userStackFrameFilters, bool skipAssertionFrames)
    {
        string message = explanation ?? exception?.Message ?? AzureDevOpsResources.NoFailureMessageFallback;
        string formattedMessage = $"{FormatErrorMessage(testDisplayName, targetFrameworkMoniker, message)}{additionalMessageSuffix}";

        if (exception?.StackTrace is { } stackTrace)
        {
            string repoRoot = RootFinder.Find();
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Found repo root '{repoRoot}'");
            }

            (string RelativeNormalizedPath, int LineNumber)? location = StackTraceSourceLocationResolver.TryResolve(
                stackTrace,
                repoRoot,
                fileSystem,
                logger,
                skipAssertionFrames,
                code => IsUserStackFrameFilterMatch(code, userStackFrameFilters, logger));

            if (location is not null)
            {
                string line = $"##vso[task.logissue type={severity};sourcepath={location.Value.RelativeNormalizedPath};linenumber={location.Value.LineNumber};columnnumber=1]{AzDoEscaper.Escape(formattedMessage)}";
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Reported full message '{line}'.");
                }

                return line;
            }

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("No stack trace line matched criteria, falling back to a message-only annotation.");
            }
        }
        else if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(exception is null
                ? "Exception was null, emitting a message-only annotation."
                : "Exception stack trace was null, emitting a message-only annotation.");
        }

        // Fallback: source location could not be resolved. The Azure DevOps logissue command only
        // requires 'type' and the message; sourcepath/linenumber/columnnumber are optional. Without
        // this fallback, failures whose stack frame cannot be resolved to a local repo file (no
        // exception, no stack trace, frames outside the repo root, or paths that do not exist on
        // disk) would be silently suppressed. See https://github.com/microsoft/testfx/issues/5979.
        string fallbackLine = $"##vso[task.logissue type={severity}]{AzDoEscaper.Escape(formattedMessage)}";
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"Reported message-only annotation '{fallbackLine}'.");
        }

        return fallbackLine;
    }

    private string GetConfiguredSeverity()
        => _commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, out string[]? arguments)
            && arguments is [string configuredSeverity]
                ? configuredSeverity.ToLowerInvariant()
                : "error";

    private QuarantineFile? LoadQuarantineFile()
    {
        if (!_commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, out string[]? arguments)
            || arguments is not [string quarantineFilePath])
        {
            return null;
        }

        // NOTE: The value is treated as an explicit filesystem path supplied by the caller; this extension only validates existence and readability.
        if (!_fileSystem.ExistFile(quarantineFilePath))
        {
            _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileMissingWarning, quarantineFilePath));
            return null;
        }

        try
        {
            return new QuarantineFile(quarantineFilePath, _fileSystem, _logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileLoadFailedWarning, quarantineFilePath, ex.Message));
            return null;
        }
    }

    private string GetSeverity(string testName, bool isQuarantined)
        => isQuarantined || (_demoteKnownFlaky && _historyService.IsLikelyFlaky(testName, KnownFlakyFailureRateThreshold))
            ? WarningSeverity
            : _severity ?? "error";

    private string BuildAnnotationSuffix(string testName, bool isQuarantined)
    {
        string? historyAnnotation = GetHistoryAnnotation(testName);
        if (historyAnnotation is null && !isQuarantined)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        if (historyAnnotation is not null)
        {
            builder.Append(' ').Append(historyAnnotation);
        }

        if (isQuarantined)
        {
            builder.Append(' ').Append(AzureDevOpsResources.QuarantinedAnnotation);
        }

        return builder.ToString();
    }

    private string? GetHistoryAnnotation(string testName)
        => !_historyService.TryGetStats(testName, out FlakyStats stats) || stats.TotalCount == 0
            ? null
            : stats.FailCount == 0
                ? stats.TotalCount >= MinSamplesForRegressionAnnotation
                    ? AzureDevOpsResources.FlakyHistoryRegressionAnnotation
                    : null
                : string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryFailureAnnotation, stats.FailCount, stats.TotalCount, _historyService.HistoryWindowInDays);

    private void EnsureEnabledConfigurationLoaded()
    {
        if (_hasLoadedEnabledConfiguration)
        {
            return;
        }

        _severity = GetConfiguredSeverity();
        _demoteKnownFlaky = _commandLine.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky);
        _quarantineFile = LoadQuarantineFile();
        _userStackFrameFilters = LoadUserStackFrameFilters();
        _hasLoadedEnabledConfiguration = true;
    }

    private Regex[] LoadUserStackFrameFilters()
    {
        if (!_commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter, out string[]? patterns)
            || patterns is not { Length: > 0 })
        {
            return [];
        }

        var compiled = new List<Regex>(patterns.Length);
        foreach (string pattern in patterns)
        {
            try
            {
                compiled.Add(new Regex(
                    pattern,
                    RegexOptions.CultureInvariant | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(AzureDevOpsCommandLineProvider.StackFrameFilterMatchTimeoutMs)));
            }
            catch (ArgumentException ex)
            {
                // Should have been caught at validation time, but log and skip if not.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"Skipping invalid '--report-azdo-stackframe-filter' regex '{pattern}': {ex.Message}");
                }
            }
        }

        return [.. compiled];
    }

    private static string GetTestName(TestNode testNode)
        => TestNodeIdentity.GetTestName(testNode);

    /// <summary>
    /// Formats the reporter message so the test name lands on its own line.
    /// PR check UIs (GitHub Checks via the dotnet problem matcher and Azure DevOps)
    /// render the first line of the message as the bold annotation title, so we
    /// keep the test display name compact and push the assertion text to the body.
    /// </summary>
    /// <remarks>
    /// MTP includes the TFM in the display name in multi-TFM mode (e.g. "MyTest (net8.0)").
    /// To avoid noise like "MyTest (net8.0) [net8.0]" we skip the bracketed TFM
    /// suffix when the display name already ends with "({tfm})" or "(\"{tfm}\")".
    /// </remarks>
    internal static /* for testing */ string FormatErrorMessage(string testDisplayName, string targetFrameworkMoniker, string message)
    {
        string titleLine = DisplayNameContainsTfm(testDisplayName, targetFrameworkMoniker)
            ? testDisplayName
            : $"{testDisplayName} [{targetFrameworkMoniker}]";

        return $"{titleLine}\n{message}";
    }

    private static bool DisplayNameContainsTfm(string displayName, string tfm)
        => displayName.EndsWith($"({tfm})", StringComparison.Ordinal)
            || displayName.EndsWith($"(\"{tfm}\")", StringComparison.Ordinal);

    private static bool IsUserStackFrameFilterMatch(string code, Regex[]? userStackFrameFilters, ILogger logger)
    {
        if (userStackFrameFilters is null || userStackFrameFilters.Length == 0)
        {
            return false;
        }

        foreach (Regex filter in userStackFrameFilters)
        {
            try
            {
                if (filter.IsMatch(code))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"'--report-azdo-stackframe-filter' regex '{filter}' timed out matching frame '{code}': {ex.Message}. Treating as no-match.");
                }
            }
        }

        return false;
    }
}
