// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
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
    private const double KnownFlakyFailureRateThreshold = 0.25;
    private const string DeterministicBuildRoot = "/_/";
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";
    private const int MinSamplesForRegressionAnnotation = 5;
    private const string QuarantineBuildTagLine = "##vso[build.addbuildtag]has-quarantined-test-failure";
    private const string WarningSeverity = "warning";
    private static readonly char[] NewlineCharacters = ['\r', '\n'];

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
        _targetFrameworkMoniker = GetTargetFrameworkMoniker();
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

        bool isEnabledByEnvVariable = string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);
        if (isEnabledByEnvVariable)
        {
            EnsureEnabledConfigurationLoaded();
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"TF_BUILD environment variable is {(isEnabledByEnvVariable ? "enabled. Will report errors to Azure DevOps, because we are running in CI." : "disabled. Will not report errors to Azure DevOps.")}");
            _logger.LogTrace($"Severity is set to '{_severity ?? "error"}', you can override it by using --report-azdo-severity parameter.");
        }

        return Task.FromResult(isEnabledByEnvVariable);
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
        string testName = GetTestName(nodeUpdateMessage.TestNode);

        switch (nodeState)
        {
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(testDisplayName, testName, failed.Explanation, failed.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(testDisplayName, testName, error.Explanation, error.Exception, cancellationToken).ConfigureAwait(false);
                break;
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            case CancelledTestNodeStateProperty cancelled:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                await WriteExceptionAsync(testDisplayName, testName, cancelled.Explanation, cancelled.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(testDisplayName, testName, timeout.Explanation, timeout.Exception, cancellationToken).ConfigureAwait(false);
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
            await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(QuarantineBuildTagLine), cancellationToken).ConfigureAwait(false);
        }

        string severity = GetSeverity(testName, isQuarantined);
        string annotationSuffix = BuildAnnotationSuffix(testName, isQuarantined);
        string? line = GetErrorText(testDisplayName, explanation, exception, severity, _fileSystem, _logger, _targetFrameworkMoniker, annotationSuffix);
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

        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker)
        => GetErrorText(testDisplayName, explanation, exception, severity, fileSystem, logger, targetFrameworkMoniker, additionalMessageSuffix: null);

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker, string? additionalMessageSuffix)
    {
        if (exception is null || exception.StackTrace is null)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Exception or stack trace were null, returning.");
            }

            return null;
        }

        string message = explanation ?? exception.Message;
        if (message is null)
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
            if (location is null)
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("StackFrame location was null, continuing to next.");
                }

                continue;
            }

            string file = location.Value.File;
            if (file.EndsWith("Assert.cs", StringComparison.Ordinal))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("StackFrame location ends with 'Assert.cs' this is a special pattern that we skip, continuing to next.");
                }

                continue;
            }

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
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{file}' does not belong to current repo '{repoRoot}'. Continue to next.");
                }

                continue;
            }

            string fullPath = Path.Combine(repoRoot, relativePath);
            if (!fileSystem.ExistFile(fullPath))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Path '{fullPath}' does not exist on disk. Continue to next.");
                }

                continue;
            }

            string relativeNormalizedPath = relativePath.Replace('\\', '/');
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Normalized path for GitHub '{relativeNormalizedPath}'.");
            }

            string formattedMessage = $"[{testDisplayName}] [{targetFrameworkMoniker}] {message}{additionalMessageSuffix}";
            string line = $"##vso[task.logissue type={severity};sourcepath={relativeNormalizedPath};linenumber={location.Value.LineNumber};columnnumber=1]{AzDoEscaper.Escape(formattedMessage)}";
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Reported full message '{line}'.");
            }

            return line;
        }

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("No stack trace line matched criteria, no failure line was reported.");
        }

        return null;
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
        _hasLoadedEnabledConfiguration = true;
    }

    private static string GetTargetFrameworkMoniker()
        => TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);

    private static string GetTestName(TestNode testNode)
        => testNode.Properties
            .OfType<SerializableKeyValuePairStringProperty>()
            .FirstOrDefault(static property => property.Key == FullyQualifiedNamePropertyKey)?.Value
            ?? testNode.DisplayName;

    private static (string Code, string File, int LineNumber)? GetStackFrameLocation(string stackTraceLine)
    {
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            return null;
        }

        string code = match.Groups["code"].Value;
        if (RoslynString.IsNullOrWhiteSpace(code))
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
