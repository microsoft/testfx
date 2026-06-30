// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Emits a GitHub Actions <c>::error</c> workflow command for each failing test so the failure surfaces
/// both in the workflow run's Annotations tab and, when the source location can be resolved, on the
/// pull request's "Files changed" diff gutter.
/// </summary>
internal sealed class GitHubActionsAnnotationReporter :
    IDataConsumer,
    IOutputDeviceDataProducer
{
    private const string DeterministicBuildRoot = "/_/";
    private static readonly char[] NewlineCharacters = ['\r', '\n'];

    // Fully-qualified type prefixes for MSTest assertion implementations. A stack frame whose
    // 'code' starts with any of these is treated as framework internals and skipped when looking
    // for the user's call site to annotate.
    private static readonly string[] AssertionImplementationCodePrefixes =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.Assert.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.",
    ];

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDisplay;
    private readonly ILogger _logger;
    private readonly bool _isEnabled;

    public GitHubActionsAnnotationReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory)
    {
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDisplay = outputDisplay;
        _logger = loggerFactory.CreateLogger<GitHubActionsAnnotationReporter>();
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLine, environment, GitHubActionsCommandLineOptions.GitHubActionsAnnotations);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    /// <inheritdoc />
    public string Uid => nameof(GitHubActionsAnnotationReporter);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = GitHubActionsResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = GitHubActionsResources.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is not TestNodeUpdateMessage nodeUpdateMessage)
        {
            return;
        }

        TestNodeStateProperty? nodeState = nodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        string testDisplayName = nodeUpdateMessage.TestNode.DisplayName;

        switch (nodeState)
        {
            case FailedTestNodeStateProperty failed:
                await WriteAnnotationAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), failed.Explanation, failed.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteAnnotationAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), error.Explanation, error.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteAnnotationAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), timeout.Explanation, timeout.Exception, cancellationToken).ConfigureAwait(false);
                break;
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            case CancelledTestNodeStateProperty cancelled:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                await WriteAnnotationAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), cancelled.Explanation, cancelled.Exception, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task WriteAnnotationAsync(string testDisplayName, string testName, string? explanation, Exception? exception, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Failure received.");
        }

        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        string line = GetErrorAnnotation(testName, explanation, exception, repoRoot, _fileSystem, _logger);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"Showing failure annotation '{line}'.");
        }

        // Prepend a newline so the '::error' workflow command always starts at column 0 on its own line.
        // In CI the terminal output device runs in SimpleAnsi mode and emits a color reset ('\e[m') WITHOUT a
        // trailing newline after the preceding colored "failed" test block. Emitting the annotation directly
        // would yield "\e[m::error ..." and GitHub only recognizes a workflow command when the line begins
        // with '::', so the dangling reset would silently drop the annotation. The leading newline pushes the
        // reset onto its own (ignored) line and keeps the annotation parseable.
        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData($"\n{line}"), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string GetErrorAnnotation(string testName, string? explanation, Exception? exception, string? repoRoot, IFileSystem fileSystem, ILogger logger)
    {
        string message = explanation ?? exception?.Message ?? GitHubActionsResources.NoFailureMessageFallback;
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AnnotationTitle, testName);

        (string File, int Line)? location = TryGetSourceLocation(exception, repoRoot, fileSystem, logger);
        if (location is not null)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "::error file={0},line={1},col=1,title={2}::{3}",
                GitHubActionsEscaper.EscapeProperty(location.Value.File),
                location.Value.Line.ToString(CultureInfo.InvariantCulture),
                GitHubActionsEscaper.EscapeProperty(title),
                GitHubActionsEscaper.EscapeData(message));
        }

        // Fallback: source location could not be resolved. The file/line/col properties are optional;
        // a title-only annotation still surfaces in the workflow Annotations tab.
        return string.Format(
            CultureInfo.InvariantCulture,
            "::error title={0}::{1}",
            GitHubActionsEscaper.EscapeProperty(title),
            GitHubActionsEscaper.EscapeData(message));
    }

    private static (string File, int Line)? TryGetSourceLocation(Exception? exception, string? repoRoot, IFileSystem fileSystem, ILogger logger)
    {
        if (exception?.StackTrace is not { } stackTrace || RoslynString.IsNullOrEmpty(repoRoot))
        {
            return null;
        }

        foreach (string stackFrame in stackTrace.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            (string Code, string File, int LineNumber)? location = GetStackFrameLocation(stackFrame);
            if (location is null)
            {
                continue;
            }

            string file = location.Value.File;
            string code = location.Value.Code;

            if (IsAssertionImplementationFrame(code))
            {
                continue;
            }

            string relativePath;
            if (file.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file.Substring(DeterministicBuildRoot.Length);
            }
            else if (file.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file.Substring(repoRoot.Length);
            }
            else
            {
                continue;
            }

            string fullPath = Path.Combine(repoRoot, relativePath);
            if (!fileSystem.ExistFile(fullPath))
            {
                continue;
            }

            // GitHub annotations expect a workspace-relative path with forward slashes.
            string relativeNormalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Normalized path for GitHub annotation '{relativeNormalizedPath}'.");
            }

            return (relativeNormalizedPath, location.Value.LineNumber);
        }

        return null;
    }

    private static bool IsAssertionImplementationFrame(string code)
    {
        foreach (string prefix in AssertionImplementationCodePrefixes)
        {
            if (code.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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

    private static string GetTestName(TestNode testNode)
        => TestNodeIdentity.GetTestName(testNode);
}
