// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsReporter
{
    private async Task WriteExceptionAsync(string testDisplayName, string testName, string? explanation, Exception? exception, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("Failure received.").ConfigureAwait(false);
        }

        bool isQuarantined = _quarantineFile?.Matches(testName) == true;
        if (isQuarantined && Interlocked.Exchange(ref _quarantineBuildTagEmitted, 1) == 0)
        {
            await _outputDisplay.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(QuarantineBuildTagLine), cancellationToken).ConfigureAwait(false);
        }

        string severity = GetSeverity(testName, isQuarantined);
        string annotationSuffix = BuildAnnotationSuffix(testName, isQuarantined);
        string? line = GetErrorText(
            testDisplayName,
            explanation,
            exception,
            severity,
            _fileSystem,
            _logger,
            _targetFrameworkMoniker,
            annotationSuffix,
            _userStackFrameFilters,
            StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime,
            _contextualAssemblyIdentity);
        if (line is null)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                await _logger.LogTraceAsync("Failure message is null, returning.").ConfigureAwait(false);
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync($"Showing failure message '{line}'.").ConfigureAwait(false);
        }

        await _outputDisplay.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker)
        => GetErrorText(testDisplayName, explanation, exception, severity, fileSystem, logger, targetFrameworkMoniker, additionalMessageSuffix: null, userStackFrameFilters: null, skipAssertionFrames: true);

    internal static /* for testing */ string? GetErrorText(string testDisplayName, string? explanation, Exception? exception, string severity, IFileSystem fileSystem, ILogger logger, string targetFrameworkMoniker, string? additionalMessageSuffix)
        => GetErrorText(testDisplayName, explanation, exception, severity, fileSystem, logger, targetFrameworkMoniker, additionalMessageSuffix, userStackFrameFilters: null, skipAssertionFrames: true);

    internal static /* for testing */ string? GetErrorText(
        string testDisplayName,
        string? explanation,
        Exception? exception,
        string severity,
        IFileSystem fileSystem,
        ILogger logger,
        string targetFrameworkMoniker,
        string? additionalMessageSuffix,
        Regex[]? userStackFrameFilters,
        bool skipAssertionFrames)
        => GetErrorText(
            testDisplayName,
            explanation,
            exception,
            severity,
            fileSystem,
            logger,
            targetFrameworkMoniker,
            additionalMessageSuffix,
            userStackFrameFilters,
            skipAssertionFrames,
            assemblyIdentity: null);

    internal static /* for testing */ string? GetErrorText(
        string testDisplayName,
        string? explanation,
        Exception? exception,
        string severity,
        IFileSystem fileSystem,
        ILogger logger,
        string targetFrameworkMoniker,
        string? additionalMessageSuffix,
        Regex[]? userStackFrameFilters,
        bool skipAssertionFrames,
        string? assemblyIdentity)
    {
        string message = explanation ?? exception?.Message ?? AzureDevOpsResources.NoFailureMessageFallback;
        // Agent.Sdk.CommandStringConvertor.Unescape decodes command escapes such as %3B
        // before decoding %AZP25. Encode percent only in this contextual identity so literal
        // sequences like "%3B" survive that order. With DECODE_PERCENTS=false, the agent
        // leaves %AZP25 literal.
        string? commandSafeAssemblyIdentity = assemblyIdentity?.Replace("%", "%AZP25");
        string formattedMessage = $"{FormatErrorMessage(testDisplayName, targetFrameworkMoniker, message, commandSafeAssemblyIdentity)}{additionalMessageSuffix}";

        if (exception?.StackTrace is { } stackTrace)
        {
            // A missing repository root only means source locations cannot be made repo-relative, so it
            // must degrade to the message-only annotation below rather than fault the data consumer.
            // This happens whenever the test application runs outside a git checkout — including on
            // browser-wasm, whose virtual filesystem has no '.git' anywhere. The GitHub Actions reporter
            // makes the same allowance in GitHubActionsRepositoryRoot.FindGitRoot.
            if (TryFindRepoRoot(logger) is { } repoRoot)
            {
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

    /// <summary>
    /// Resolves the repository root used to turn an absolute stack-frame path into a repo-relative
    /// <c>sourcepath</c>, or <see langword="null"/> when the test application does not run inside a git
    /// checkout (a published app, a container, or the browser-wasm virtual filesystem).
    /// </summary>
    private static string? TryFindRepoRoot(ILogger logger)
    {
        try
        {
            string repoRoot = RootFinder.Find();
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Found repo root '{repoRoot}'");
            }

            return repoRoot;
        }
        catch (InvalidOperationException)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("No repository root was found, emitting a message-only annotation.");
            }

            return null;
        }
    }

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
    internal static /* for testing */ string FormatErrorMessage(
        string testDisplayName,
        string targetFrameworkMoniker,
        string message)
        => FormatErrorMessage(testDisplayName, targetFrameworkMoniker, message, assemblyIdentity: null);

    internal static /* for testing */ string FormatErrorMessage(
        string testDisplayName,
        string targetFrameworkMoniker,
        string message,
        string? assemblyIdentity)
    {
        string titleLine = DisplayNameContainsTfm(testDisplayName, targetFrameworkMoniker)
            ? testDisplayName
            : $"{testDisplayName} [{targetFrameworkMoniker}]";

        if (!RoslynString.IsNullOrEmpty(assemblyIdentity))
        {
            titleLine = $"{titleLine} ({assemblyIdentity})";
        }

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
