// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Writes a markdown roll-up of the test run (totals, failures, slowest tests) to the file pointed to by
/// the <c>GITHUB_STEP_SUMMARY</c> environment variable. GitHub renders that file on the workflow run's
/// summary page. See
/// <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary"/>.
/// </summary>
internal sealed class GitHubActionsSummaryReporter :
    IDataConsumer,
    IDataProducer,
    ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer
{
    private const string StepSummaryEnvironmentVariable = "GITHUB_STEP_SUMMARY";
    private const int MaxFailures = 20;
    private const int MaxSlowestTests = 10;

    // GITHUB_STEP_SUMMARY is a single shared file that every test-host process appends to. Under a
    // concurrent multi-assembly `dotnet test` run, contention is resolved by an exclusive-append retry loop
    // (see AppendStepSummaryWithRetryAsync). Twenty attempts at 50 ms bound the wait to ~1s, which is ample
    // to serialize the tiny per-assembly writes while still failing fast (into a best-effort warning) on a
    // genuinely unwritable path.
    private const int StepSummaryMaxWriteAttempts = 20;
    private static readonly TimeSpan StepSummaryRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;
    private readonly bool _includeFailureDetails;
    private readonly Func<bool> _shouldDeferToArtifactPostProcessing;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly Dictionary<string, TestRecord> _records = new Dictionary<string, TestRecord>(StringComparer.Ordinal);
#pragma warning restore IDE0028

    public GitHubActionsSummaryReporter(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ILoggerFactory loggerFactory,
        Func<bool> shouldDeferToArtifactPostProcessing)
    {
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _messageBus = messageBus;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _logger = loggerFactory.CreateLogger<GitHubActionsSummaryReporter>();
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
        _includeFailureDetails = GitHubActionsFeature.IsKnobEnabled(commandLineOptions, GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);
        _shouldDeferToArtifactPostProcessing = shouldDeferToArtifactPostProcessing;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    public string Uid => nameof(GitHubActionsSummaryReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        lock (_stateLock)
        {
            _records.Clear();
        }

        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.FirstOrDefault<TestNodeStateProperty>();
            TerminalKind kind = SummaryReporterHelpers.GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            // Resolve the stable, fully-qualified name the same way the annotation and slow-test reporters do
            // (preferring TestMethodIdentifierProperty) so a given test renders identically across all three surfaces.
            string fullyQualifiedName = TestNodeIdentity.GetTestName(update.TestNode);

            TimingProperty? timing = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is TimingProperty t)
                {
                    timing = t;
                    break;
                }
            }

            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

            TestFailureDetails? failureDetails = kind == TerminalKind.Failed && _includeFailureDetails
                ? CaptureFailureDetails(update.TestNode, state)
                : null;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration, failureDetails);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Captures the diagnostics of a failing test — explanation/exception message, exception type, stack trace and
    /// the source location — so the job summary can expand the failure beyond its name.
    /// </summary>
    /// <remarks>
    /// The location is resolved the same way <see cref="GitHubActionsAnnotationReporter"/> resolves it: prefer the
    /// exception's call site (it pinpoints the failing statement) and fall back to the location the test framework
    /// reported for the test itself, so frameworks without a usable stack trace still get a location. Values are
    /// clipped here rather than at render time so an enormous stack trace never reaches the aggregation fragment
    /// written to disk.
    /// </remarks>
    private TestFailureDetails? CaptureFailureDetails(TestNode testNode, TestNodeStateProperty? state)
    {
        (string? Explanation, Exception? Exception)? failure = state switch
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
            return null;
        }

        Exception? exception = failure.Value.Exception;
        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        (string RelativeNormalizedPath, int LineNumber)? stackLocation = StackTraceSourceLocationResolver.TryResolve(
            exception?.StackTrace,
            repoRoot,
            _fileSystem,
            _logger,
            StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);
        GitHubActionsSourceLocation? location = stackLocation is { } resolved
            ? new GitHubActionsSourceLocation(resolved.RelativeNormalizedPath, resolved.LineNumber)
            : GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, repoRoot, _fileSystem);

        return new TestFailureDetails(
            GitHubActionsFailureDetails.Clip(failure.Value.Explanation ?? exception?.Message, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows),
            exception?.GetType().FullName,
            GitHubActionsFailureDetails.Clip(exception?.StackTrace, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows),
            location?.RelativeNormalizedPath,
            location?.LineNumber ?? 0);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled)
            {
                return;
            }

            string? path = _environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
            if (RoslynString.IsNullOrWhiteSpace(path))
            {
                // Outside a GitHub Actions step (or when summaries are unsupported) there is nowhere to
                // write. Stay quiet apart from a low-noise trace so local/dev runs don't get a warning.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"'{StepSummaryEnvironmentVariable}' is not set; skipping job summary.");
                }

                return;
            }

            List<TestRecord> snapshot;
            lock (_stateLock)
            {
                snapshot = [.. _records.Values];
            }

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name";
            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext);
                string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                    resultsDirectory,
                    GitHubActionsSummaryArtifactPostProcessor.Provider,
                    GitHubActionsSummaryArtifactPostProcessor.ProviderSlug,
                    module).ConfigureAwait(false);
                await _messageBus.PublishAsync(
                    this,
                    new SessionFileArtifact(
                        testSessionContext.SessionUid,
                        new FileInfo(fragmentPath),
                        GitHubActionsResources.DisplayName,
                        GitHubActionsResources.Description,
                        GitHubActionsSummaryArtifactPostProcessor.FragmentArtifactKind)).ConfigureAwait(false);
                return;
            }

            // The 1 MiB cap applies to the whole GITHUB_STEP_SUMMARY file, which every test project in the job
            // appends to. Measure what earlier projects already wrote and claim only the remainder, so a job with
            // many test projects degrades gracefully instead of the last ones pushing the file over the cap (at
            // which point GitHub drops the summary entirely).
            int detailsBudget = GetRemainingDetailsBudget(_fileSystem, path!, _logger);

            string markdown = detailsBudget <= 0 && IsSummaryNearLimit(_fileSystem, path!, _logger)
                // Even a details-free section costs a few KB per project (heading, totals table, failure lines).
                // Once the shared file is close to the cap, that per-project overhead is itself what would push
                // it over, so collapse to a single line that still reports this project's verdict.
                ? BuildMinimalMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode)
                : BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode, _includeFailureDetails, detailsBudget);

            try
            {
                await AppendStepSummaryWithRetryAsync(_fileSystem, path!, markdown, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay, testSessionContext.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string warning = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, path, ex.Message);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(warning);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private CiRunSummaryModule CreateModule(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        ITestSessionContext testSessionContext)
        => CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode());

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;

    /// <summary>
    /// Appends <paramref name="content"/> to the shared <c>GITHUB_STEP_SUMMARY</c> file in a way that is safe
    /// when multiple test-host processes (one per assembly / target framework in a <c>dotnet test</c> run) write
    /// concurrently.
    /// </summary>
    /// <remarks>
    /// <see cref="FileMode.Append"/> only seeks to the end of the file once, at open time, and performs no
    /// atomic OS-level append. Opening with <see cref="FileShare.ReadWrite"/> would therefore let two processes
    /// position at the same offset and interleave or overwrite each other's section. We instead open with
    /// <see cref="FileShare.Read"/> — which denies other writers — so at most one process appends at a time, and
    /// retry on the resulting sharing violation (an <see cref="IOException"/>) until the holder releases the file.
    /// Each write is a single small section, so contention clears almost immediately; the bounded attempt count
    /// still lets a genuinely unlockable file surface as the caller's best-effort warning rather than looping
    /// forever.
    /// <para>
    /// Retries are scoped to <em>acquiring</em> the exclusive append handle only. Once the handle is acquired the
    /// process appends alone, so contention can no longer occur; a failure that happens <em>during</em> the write
    /// (e.g. disk full) may already have appended a partial section, and retrying would re-append the full section
    /// on top of it and corrupt the summary. Such a mid-write failure is therefore propagated straight to the
    /// caller's best-effort warning path instead of being retried.
    /// </para>
    /// </remarks>
    internal static /* for testing */ async Task AppendStepSummaryWithRetryAsync(
        IFileSystem fileSystem,
        string path,
        string content,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream stream;
            try
            {
                stream = fileSystem.NewFileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // Another test-host process currently holds the summary file open for writing. Back off briefly
                // and retry so this assembly's section is appended intact once the holder releases the file.
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // The exclusive append handle is acquired: from here on we append alone, so any failure is a genuine
            // write error (not contention) and must not be retried — a partial append followed by a full re-append
            // would corrupt the summary. Let it propagate to the caller's best-effort warning path.
            using (stream)
            using (var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            return;
        }
    }

    internal static async Task UpsertStepSummaryWithRetryAsync(
        IFileSystem fileSystem,
        string path,
        string aggregationId,
        string content,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        string startMarker = $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:start -->";
        string endMarker = $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:end -->";
        string section = $"{startMarker}\n{content.TrimEnd()}\n{endMarker}\n";
        // Keep one stable lock entry for the lifetime of the GitHub step. Deleting it after releasing the handle
        // would let a third writer create a new inode while a second writer still holds the unlinked old lock.
        string lockPath = path + ".microsoft-testing-platform.lock";

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream lockStream;
            try
            {
                lockStream = fileSystem.NewFileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (lockStream)
                {
                    string existing;
                    using (IFileStream summaryStream = fileSystem.NewFileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(summaryStream.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
#if NET8_0_OR_GREATER
                        existing = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016 // The target framework has no cancellation-aware StreamReader overload.
                        existing = await reader.ReadToEndAsync().ConfigureAwait(false);
#pragma warning restore CA2016
#endif
                    }

                    int start = existing.IndexOf(startMarker, StringComparison.Ordinal);
                    if (start >= 0)
                    {
                        int end = existing.IndexOf(endMarker, start, StringComparison.Ordinal);
                        if (end < 0)
                        {
                            throw new FormatException("The existing GitHub step summary contains an incomplete Microsoft Testing Platform summary section.");
                        }

                        existing = existing.Remove(start, end + endMarker.Length - start).Insert(start, section.TrimEnd());
                    }
                    else
                    {
                        existing = existing.Length == 0
                            ? section
                            : existing.TrimEnd() + "\n\n" + section;
                    }

                    using (IFileStream tempStream = fileSystem.NewFileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(tempStream.Stream, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(existing).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
#pragma warning disable CA2016 // The target framework has no cancellation-aware StreamWriter overload.
                        await writer.FlushAsync().ConfigureAwait(false);
#pragma warning restore CA2016
#endif
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    fileSystem.ReplaceFile(tempPath, path);
                }
            }
            finally
            {
                try
                {
                    fileSystem.DeleteFile(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup must not hide a successful write or its primary failure.
                }
            }

            return;
        }
    }

    /// <summary>
    /// Indicates whether the shared summary file is close enough to its target size that even a compact
    /// per-project section risks pushing it over.
    /// </summary>
    internal static /* for testing */ bool IsSummaryNearLimit(IFileSystem fileSystem, string path, ILogger logger)
        => GetSummaryLength(fileSystem, path, logger) is long length
            && length >= GitHubActionsFailureDetails.MaxSummaryLength - GitHubActionsFailureDetails.PerProjectOverheadReserve;

    /// <summary>
    /// Renders a single-line verdict for this test project. Used only when the shared summary file is already
    /// near GitHub's cap, where the few kilobytes of a normal section would be the thing that overflows it.
    /// </summary>
    internal static /* for testing */ string BuildMinimalMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        foreach (TestRecord record in records)
        {
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} `{1}` ({2}): {3} total, {4} passed, {5} failed, {6} skipped — {7}\n\n",
            runFailed ? "❌" : "✅",
            EscapeInlineCode(assemblyName),
            EscapeInlineCode(targetFrameworkMoniker),
            records.Count.ToString(CultureInfo.InvariantCulture),
            passed.ToString(CultureInfo.InvariantCulture),
            failed.ToString(CultureInfo.InvariantCulture),
            skipped.ToString(CultureInfo.InvariantCulture),
            GitHubActionsResources.SummaryCondensed);
    }

    private static long? GetSummaryLength(IFileSystem fileSystem, string path, ILogger logger)
    {
        try
        {
            if (!fileSystem.ExistFile(path))
            {
                return null;
            }

            using IFileStream stream = fileSystem.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Stream.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Could not measure '{path}' to size the failure-details budget: {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Returns the characters of expanded failure detail this test project may still write, given what other
    /// projects in the same GitHub Actions job have already appended to the shared summary file.
    /// </summary>
    /// <remarks>
    /// Each test project runs in its own process and cannot know how many siblings will run, or whether it is
    /// first or last. It can, however, observe the shared file: whatever is already there is a lower bound on
    /// the space consumed. Claiming only the remainder — minus a reserve for this project's own headings,
    /// tables and failure lines — keeps the whole file near
    /// <see cref="GitHubActionsFailureDetails.MaxSummaryLength"/> regardless of project count, which matters
    /// because GitHub silently drops a summary that exceeds its 1 MiB cap.
    /// <para>
    /// A file that cannot be measured (not yet created, or an I/O failure) yields the full budget: the file
    /// length is an optimization, and failing to read it must not suppress diagnostics.
    /// </para>
    /// </remarks>
    internal static /* for testing */ int GetRemainingDetailsBudget(IFileSystem fileSystem, string path, ILogger logger)
    {
        if (GetSummaryLength(fileSystem, path, logger) is not long alreadyWritten)
        {
            return GitHubActionsFailureDetails.MaxTotalDetailsLength;
        }

        long remaining = GitHubActionsFailureDetails.MaxSummaryLength
            - alreadyWritten
            - GitHubActionsFailureDetails.PerProjectOverheadReserve;
        return remaining <= 0 ? 0 : (int)Math.Min(remaining, GitHubActionsFailureDetails.MaxTotalDetailsLength);
    }

    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode, bool includeFailureDetails = true, int detailsBudget = GitHubActionsFailureDetails.MaxTotalDetailsLength)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failures = new List<TestRecord>();

        foreach (TestRecord record in records)
        {
            totalDuration += record.Duration;
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    if (failures.Count < MaxFailures)
                    {
                        failures.Add(record);
                    }

                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        // Reflect the process verdict, not just the failed-test count: a run can end in failure with zero failed
        // tests (e.g. zero tests discovered or a --minimum-expected-tests violation), which must not show ✅.
        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        string statusIcon = runFailed ? "❌" : "✅";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Test Run Summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(passed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(failed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(skipped.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(totalDuration)).Append(" |\n\n");

        // Surface a non-test-result failure that this reporter can observe once the session has finished
        // (zero tests, --minimum-expected-tests, --maximum-failed-tests, test-adapter session failure) as a
        // GitHub alert callout. Plain pass / at-least-one-failed outcomes are already conveyed by the totals
        // table and the failures section, so no callout is added for them.
        if (!GitHubActionsExitCode.IsTestResultOutcome(exitCode))
        {
            string calloutText = string.Format(
                CultureInfo.InvariantCulture,
                GitHubActionsResources.ExitCodeCallout,
                exitCode.ToString(CultureInfo.InvariantCulture),
                GitHubActionsExitCode.GetName(exitCode),
                GitHubActionsExitCode.GetReason(exitCode));
            builder.Append("> [!WARNING]\n> ").Append(EscapeInlineCode(calloutText)).Append("\n\n");
        }

        if (failures.Count > 0)
        {
            int remainingBudget = detailsBudget;
            GitHubActionsFailureDetails.AppendFailuresSection(
                builder,
                "###",
                [.. failures.Select(static failure => new GitHubActionsFailureEntry(
                    failure.FullyQualifiedName,
                    failure.Duration,
                    failure.Failure?.Message,
                    failure.Failure?.ExceptionType,
                    failure.Failure?.StackTrace,
                    failure.Failure?.FilePath,
                    failure.Failure?.LineNumber ?? 0))],
                failed,
                includeFailureDetails,
                ref remainingBudget);
        }

        IEnumerable<TestRecord> slowest = records
            .Where(static r => r.Duration > TimeSpan.Zero)
            .OrderByDescending(static r => r.Duration)
            .Take(MaxSlowestTests);

        bool slowestEmitted = false;
        foreach (TestRecord record in slowest)
        {
            if (!slowestEmitted)
            {
                builder.Append("### ⏱ Slowest tests\n\n");
                slowestEmitted = true;
            }

            builder.Append("- `").Append(EscapeInlineCode(record.FullyQualifiedName)).Append("` — ").Append(FormatDuration(record.Duration)).Append('\n');
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate, bool includeFailureDetails = true)
    {
        bool failed = aggregate.ExitCode is int exitCode
            ? GitHubActionsExitCode.IndicatesFailure(exitCode)
            : aggregate.FailedTests > 0;
        string statusIcon = failed
            ? "❌"
            : aggregate.IsPartial || !aggregate.HasAuthoritativeRunSummary
                ? "⚠️"
                : "✅";
        string duration = aggregate.Duration is { } value ? FormatDuration(value) : "Unavailable";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Overall Test Run Summary\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(aggregate.TotalTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.PassedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.FailedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(aggregate.SkippedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(duration).Append(" |\n\n");

        if (aggregate.IsPartial)
        {
            builder.Append("> [!WARNING]\n> This summary is partial because the test run was truncated.\n\n");
        }
        else if (!aggregate.HasAuthoritativeRunSummary)
        {
            builder.Append("> [!NOTE]\n> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
        }

        if (aggregate.ExitCode is int authoritativeExitCode
            && !GitHubActionsExitCode.IsTestResultOutcome(authoritativeExitCode))
        {
            string calloutText = string.Format(
                CultureInfo.InvariantCulture,
                GitHubActionsResources.ExitCodeCallout,
                authoritativeExitCode.ToString(CultureInfo.InvariantCulture),
                GitHubActionsExitCode.GetName(authoritativeExitCode),
                GitHubActionsExitCode.GetReason(authoritativeExitCode));
            builder.Append("> [!WARNING]\n> ").Append(EscapeInlineCode(calloutText)).Append("\n\n");
        }

        // The 1 MiB cap applies to the whole file, so the budget is shared across every module rather than
        // granted per module. Reserve each module's non-detail overhead (heading, tables, failure lines) up
        // front so the rendered file lands near MaxSummaryLength rather than that much detail *plus* overhead,
        // then divide the rest so an early module with many large failures cannot starve the later ones.
        int moduleCount = Math.Max(1, aggregate.Modules.Count);
        int overheadReserve = moduleCount * GitHubActionsFailureDetails.PerProjectOverheadReserve;
        int detailsBudget = Math.Max(0, GitHubActionsFailureDetails.MaxSummaryLength - overheadReserve);
        int perModuleBudget = detailsBudget / moduleCount;
        int remainingBudget = 0;
        int modulesWithOmittedDetails = 0;

        foreach (CiRunSummaryModule module in aggregate.Modules)
        {
            bool needsDiscriminator = HasDuplicateModuleIdentity(aggregate.Modules, module);
            builder.Append("<details>\n<summary>")
                .Append(HtmlEncode(module.AssemblyName))
                .Append(" (").Append(HtmlEncode(module.TargetFramework)).Append(", ")
                .Append(HtmlEncode(module.Architecture));
            if (needsDiscriminator)
            {
                builder.Append(", attempt ").Append(module.AttemptNumber.ToString(CultureInfo.InvariantCulture))
                    .Append(", session ").Append(HtmlEncode(module.SessionUid));
            }

            builder.Append(")</summary>\n\n");

            // Top up with this module's share, keeping whatever earlier modules left unspent.
            remainingBudget += perModuleBudget;
            if (AppendModuleMarkdown(builder, module, headingLevel: 3, includeFailureDetails, ref remainingBudget) > 0)
            {
                modulesWithOmittedDetails++;
            }

            builder.Append("</details>\n\n");
        }

        // Surface budget exhaustion at the file level too. A per-module note is easy to miss when it is buried
        // inside one of dozens of collapsed module sections, and the reader needs to know the summary as a whole
        // is not showing everything it collected.
        if (modulesWithOmittedDetails > 0)
        {
            builder.Append("> [!NOTE]\n> ")
                .Append(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.ModuleDetailsOmitted,
                    modulesWithOmittedDetails.ToString(CultureInfo.InvariantCulture),
                    aggregate.Modules.Count.ToString(CultureInfo.InvariantCulture)))
                .Append("\n\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders one module's section, returning the number of its listed failures whose diagnostics did not fit
    /// the shared budget.
    /// </summary>
    private static int AppendModuleMarkdown(StringBuilder builder, CiRunSummaryModule module, int headingLevel, bool includeFailureDetails, ref int remainingBudget)
    {
        string heading = new('#', headingLevel);
        bool runFailed = module.FailedTests > 0 || GitHubActionsExitCode.IndicatesFailure(module.ExitCode);
        builder.Append(heading).Append(' ').Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeInlineCode(module.AssemblyName)).Append("\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Test duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n\n");

        if (!GitHubActionsExitCode.IsTestResultOutcome(module.ExitCode))
        {
            builder.Append("> Module exit code: `").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append("` (")
                .Append(EscapeInlineCode(GitHubActionsExitCode.GetName(module.ExitCode))).Append(")\n\n");
        }

        int omittedDetails = 0;
        if (module.Failures.Length > 0)
        {
            omittedDetails = GitHubActionsFailureDetails.AppendFailuresSection(
                builder,
                heading + "#",
                [.. module.Failures.Select(static failure => new GitHubActionsFailureEntry(
                    failure.FullyQualifiedName,
                    TimeSpan.FromTicks(failure.DurationTicks),
                    failure.ErrorMessage,
                    failure.ErrorType,
                    failure.StackTrace,
                    failure.FilePath,
                    failure.LineNumber ?? 0))],
                module.FailedTests,
                includeFailureDetails,
                ref remainingBudget);
        }

        if (module.SlowestTests.Length > 0)
        {
            builder.Append(heading).Append("# ⏱ Slowest tests\n\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("- `").Append(EscapeInlineCode(test.FullyQualifiedName)).Append("` — ")
                    .Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append('\n');
            }

            builder.Append('\n');
        }

        return omittedDetails;
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}
