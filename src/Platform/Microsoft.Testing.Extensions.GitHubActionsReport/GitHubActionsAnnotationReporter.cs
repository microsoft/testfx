// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
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
/// pull request's "Files changed" diff gutter. Skipped tests are surfaced as title-only <c>::warning</c>
/// workflow commands so they are visible in the Annotations tab too.
/// </summary>
internal sealed class GitHubActionsAnnotationReporter :
    IDataConsumer,
    IOutputDeviceDataProducer
{
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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (value is not TestNodeUpdateMessage nodeUpdateMessage)
            {
                return;
            }

            // FirstOrDefault (not SingleOrDefault): a malformed node that somehow carries more than one state
            // property must degrade to "no annotation for this test" rather than throwing into the platform's
            // data-consumer dispatch.
            TestNodeStateProperty? nodeState = nodeUpdateMessage.TestNode.Properties.FirstOrDefault<TestNodeStateProperty>();

            (string? Explanation, Exception? Exception)? failure = nodeState switch
            {
                FailedTestNodeStateProperty failed => (failed.Explanation, failed.Exception),
                ErrorTestNodeStateProperty error => (error.Explanation, error.Exception),
                TimeoutTestNodeStateProperty timeout => (timeout.Explanation, timeout.Exception),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                CancelledTestNodeStateProperty cancelled => (cancelled.Explanation, cancelled.Exception),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                _ => null,
            };

            if (failure is null)
            {
                // Skipped tests carry no exception (and therefore no source location); surface them as a
                // title-only '::warning' so intentionally or unexpectedly skipped tests are visible in the
                // workflow Annotations tab alongside failures, rather than being silently absent.
                if (nodeState is SkippedTestNodeStateProperty skipped)
                {
                    await WriteSkippedAnnotationAsync(GetTestName(nodeUpdateMessage.TestNode), skipped.Explanation, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            await WriteAnnotationAsync(GetTestName(nodeUpdateMessage.TestNode), failure.Value.Explanation, failure.Value.Exception, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Mirror the sibling reporters: a failure while building/emitting an annotation (e.g. a malformed
            // stack-trace path making IFileSystem.ExistFile throw) degrades to "no annotation" instead of
            // propagating into the platform's consumer dispatch.
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }
    }

    private async Task WriteAnnotationAsync(string testName, string? explanation, Exception? exception, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Failure received.");
        }

        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        string line = GetErrorAnnotation(testName, explanation, exception, repoRoot, _fileSystem, _logger, StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);

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

    internal static /* for testing */ string GetErrorAnnotation(string testName, string? explanation, Exception? exception, string? repoRoot, IFileSystem fileSystem, ILogger logger, bool skipAssertionFrames)
    {
        string message = explanation ?? exception?.Message ?? GitHubActionsResources.NoFailureMessageFallback;
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AnnotationTitle, testName);

        (string RelativeNormalizedPath, int LineNumber)? location = StackTraceSourceLocationResolver.TryResolve(exception?.StackTrace, repoRoot, fileSystem, logger, skipAssertionFrames);
        if (location is not null)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "::error file={0},line={1},col=1,title={2}::{3}",
                GitHubActionsEscaper.EscapeProperty(location.Value.RelativeNormalizedPath),
                location.Value.LineNumber.ToString(CultureInfo.InvariantCulture),
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

    private async Task WriteSkippedAnnotationAsync(string testName, string? explanation, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Skip received.");
        }

        string line = GetSkippedAnnotation(testName, explanation);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"Showing skip annotation '{line}'.");
        }

        // Prepend a newline for the same reason as the failure annotation: it guarantees the '::warning'
        // workflow command starts at column 0 on its own line so GitHub recognizes it.
        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData($"\n{line}"), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string GetSkippedAnnotation(string testName, string? explanation)
    {
        string message = explanation ?? GitHubActionsResources.NoSkipReasonFallback;
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.SkippedAnnotationTitle, testName);

        // Skipped nodes never carry a stack trace, so there is no file/line to pin the annotation to; a
        // title-only '::warning' still surfaces in the workflow Annotations tab.
        return string.Format(
            CultureInfo.InvariantCulture,
            "::warning title={0}::{1}",
            GitHubActionsEscaper.EscapeProperty(title),
            GitHubActionsEscaper.EscapeData(message));
    }

    private static string GetTestName(TestNode testNode)
        => TestNodeIdentity.GetTestName(testNode);
}
