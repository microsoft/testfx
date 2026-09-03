// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryOrchestrator : ITestHostExecutionOrchestrator, IOutputDeviceDataProducer, ITestHostControllerConnectionAuthorizationConsumer
{
    private const long MaxRecoveredArtifactManifestBytes = 16L * 1024 * 1024;
    private const int MaxRecoveredArtifactManifestLineBytes = 64 * 1024;
    private const int MaxRecoveredArtifactManifestRecords = 10_000;
    private const int MaxRecoveredArtifactPathChars = 32 * 1024;
    private const int MaxRecoveredArtifactKindChars = 1024;

    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;

    public RetryOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
        _fileSystem = _serviceProvider.GetFileSystem();
    }

    public string Uid => nameof(RetryOrchestrator);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName));

    private string CreateRetriesDirectory(string resultDirectory)
    {
        Exception? lastException = null;
        // Quite arbitrary. Keep trying to create the directory for 10 times.
        for (int i = 0; i < 10; i++)
        {
            string retryRootFolder = Path.Combine(resultDirectory, "Retries", RandomId.Next());
            if (_fileSystem.ExistDirectory(retryRootFolder))
            {
                continue;
            }

            try
            {
                _fileSystem.CreateDirectory(retryRootFolder);
                return retryRootFolder;
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        throw new IOException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailedToCreateRetryDirectoryBecauseOfCollision, resultDirectory));
    }

    public async Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
    {
        if (_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) && !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey))
        {
            throw new InvalidOperationException(ExtensionResources.RetryFailedTestsNotSupportedInServerModeErrorMessage);
        }

        IEnvironment environment = _serviceProvider.GetEnvironment();
        if (IsHotReloadEnabled(environment))
        {
            throw new InvalidOperationException(ExtensionResources.RetryFailedTestsNotSupportedInHotReloadErrorMessage);
        }

        environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TRX_TESTRUN_ID, Guid.NewGuid().ToString("N"));

        // Every attempt is a separate process writing its own report, but together they are one logical run.
        // Formats that can express that (CTRF 'runId') need all attempts to agree on a single id, so establish
        // one here and let the launched test hosts inherit it. Preference order:
        //   - an id already set explicitly (a CI job correlating several modules or machines) is kept;
        //   - otherwise the dotnet test execution id, which already identifies THIS test application's process
        //     tree, so the attempts stay part of that run instead of forming a separate one;
        //   - otherwise a fresh id, because a standalone retried run is its own logical run.
        if (RoslynString.IsNullOrEmpty(environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_LOGICAL_RUN_ID)))
        {
            string? executionId = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
            environment.SetEnvironmentVariable(
                EnvironmentVariableConstants.TESTINGPLATFORM_LOGICAL_RUN_ID,
                RoslynString.IsNullOrEmpty(executionId) ? Guid.NewGuid().ToString("D") : executionId!);
        }

        ILogger logger = _serviceProvider.GetLoggerFactory().CreateLogger<RetryOrchestrator>();
        IConfiguration configuration = _serviceProvider.GetConfiguration();

        ITestApplicationModuleInfo currentTestApplicationModuleInfo = _serviceProvider.GetTestApplicationModuleInfo();
        ExecutableInfo executableInfo = currentTestApplicationModuleInfo.GetCurrentExecutableInfo();

        if (!_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, out string[]? cmdRetries))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        ApplicationStateGuard.Ensure(cmdRetries is not null);
        int userMaxRetryCount = int.Parse(cmdRetries[0], CultureInfo.InvariantCulture);

        // Find out the retry args indices so we can clean up the command line when we restart.
        string[] originalExecutableArguments = [.. executableInfo.Arguments];
        string[] executableArguments = [.. executableInfo.ExpandedArguments];
        List<int> indexToCleanup = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        // Override the result directory with the attempt one
        string resultDirectory = configuration.GetTestResultDirectory();

        List<int> exitCodes = [];
        IOutputDevice outputDevice = _serviceProvider.GetOutputDevice();
        IFileSystem fileSystem = _serviceProvider.GetFileSystem();

        int attemptCount = 0;
        string[]? lastListOfFailedId = null;
        string? currentTryResultFolder = null;
        bool thresholdPolicyKickedIn = false;
        string retryRootFolder = CreateRetriesDirectory(resultDirectory);
        bool retryInterrupted = false;
        List<RetryAttemptArtifact> attemptArtifacts = [];

        // Retries are the single most useful thing to measure about a flaky suite, and the orchestrator is the only
        // component that sees every attempt. Emitting the count here (rather than inferring it from duplicated test
        // results downstream) makes "how much time do we burn on retries?" answerable from a dashboard.
        IPlatformOpenTelemetryService? otelService = _serviceProvider.GetPlatformOTelService();
        ICounter<int>? retryCounter = otelService?.CreateCounter<int>(
            TestingPlatformSemanticConventions.Metrics.TestRetryCount,
            TestingPlatformSemanticConventions.Units.Count,
            "Number of test cases scheduled for a retry attempt.");

        // Retry summary accounting (single-assembly). The orchestrator is the only component that observes every
        // attempt, so it reconciles them into one headline:
        //   retried = union of the scheduled retry sets whose following attempt reported at least one result
        //   flaky   = union of the tests each attempt reported as recovered
        // Recovery is reported by the attempt that observed it rather than inferred here as "retried and no longer
        // in the failed set": that inference also matches a test which never ran, so an attempt that crashed or
        // matched no filter would silently promote its pending tests to "passed" and "flaky".
        var orchestrationStopwatch = Stopwatch.StartNew();
        int suiteTotalTests = 0;
        int suiteSkippedTests = 0;
        bool suiteCountsKnown = false;
        Dictionary<string, string> retriedTests = [];
        Dictionary<string, string> flakyTestsByUid = [];
        int finalFailedTestResults = 0;
        int retriedExecutions = 0;
        bool sawSkippedResults = false;
        Dictionary<string, string> pendingRetriedTests = [];

        // Parse the delay once before the loop since command-line options don't change.
        TimeSpan? retryDelay = null;
        if (_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, out string[]? retryDelayArgs)
            && retryDelayArgs is { Length: > 0 }
            && TimeSpanParser.TryParse(retryDelayArgs[0], out TimeSpan parsedDelay))
        {
            retryDelay = parsedDelay;
        }

        while (attemptCount < userMaxRetryCount + 1)
        {
            attemptCount++;

            // Each attempt is a child span of the orchestrator, so a trace shows exactly how many attempts a run
            // needed and how long each of them took.
            using IPlatformActivity? attemptActivity = otelService?.StartActivity(
                "RetryAttempt",
                tags:
                [
                    new(TestingPlatformSemanticConventions.Attributes.TestCaseRetryAttempt, attemptCount),
                    new("test.retry.max_attempts", userMaxRetryCount + 1),
                    new("test.retry.scheduled_count", lastListOfFailedId?.Length ?? 0),
                ]);

            if (attemptCount > 1)
            {
                retryCounter?.Add(
                    lastListOfFailedId?.Length ?? 0,
                    [new(TestingPlatformSemanticConventions.Attributes.TestCaseRetryAttempt, attemptCount)]);
            }

            if (attemptCount > 1 && retryDelay is { } delay)
            {
                await outputDevice.DisplayAsync(
                    this,
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryWaitingBeforeNextAttempt, RetryOrchestratorHelper.FormatDelay(delay), attemptCount, userMaxRetryCount + 1))
                    {
                        ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
                    },
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            currentTryResultFolder = Path.Combine(retryRootFolder, attemptCount.ToString(CultureInfo.InvariantCulture));
            fileSystem.CreateDirectory(currentTryResultFolder);

            // Prepare the pipe server that collects the child process's failed test UIDs.
            using RetryFailedTestsPipeServer retryFailedTestsPipeServer = new(_serviceProvider, lastListOfFailedId ?? [], logger);

            RetryTestHostRunner.AttemptResult attemptResult;
            string[] generatedResponseFilePaths =
            [
                RetryArgumentsBuilder.GetArgumentsResponseFilePath(retryRootFolder, attemptCount),
                RetryArgumentsBuilder.GetFilterUidsResponseFilePath(retryRootFolder, attemptCount),
            ];
            try
            {
                List<string> finalArguments = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
                    _fileSystem,
                    executableArguments,
                    originalExecutableArguments,
                    indexToCleanup,
                    currentTryResultFolder,
                    retryRootFolder,
                    retryFailedTestsPipeServer.PipeName,
                    lastListOfFailedId,
                    attemptCount).ConfigureAwait(false);

                await LogResponseFileFallbackWarningAsync(
                    logger,
                    originalExecutableArguments,
                    finalArguments,
                    generatedResponseFilePaths[0]).ConfigureAwait(false);

                attemptResult = await RetryTestHostRunner.RunAttemptAsync(
                    _serviceProvider,
                    this,
                    outputDevice,
                    logger,
                    retryFailedTestsPipeServer,
                    executableInfo,
                    finalArguments,
                    attemptCount,
                    userMaxRetryCount,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                foreach (string responseFilePath in generatedResponseFilePaths)
                {
                    try
                    {
                        if (fileSystem.ExistFile(responseFilePath))
                        {
                            fileSystem.DeleteFile(responseFilePath);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        await logger.LogWarningAsync($"Failed to delete generated retry response file '{responseFilePath}': {ex}").ConfigureAwait(false);
                    }
                }
            }

            CollectRecoveredArtifacts(
                fileSystem,
                attemptResult.RecoveredArtifactManifestPath,
                retryFailedTestsPipeServer.Artifacts,
                logger);

            attemptArtifacts.AddRange(RetryArtifactProcessor.SnapshotAttemptArtifacts(
                fileSystem,
                retryFailedTestsPipeServer.Artifacts,
                attemptCount,
                currentTryResultFolder,
                retryRootFolder));

            if (attemptResult.ExitedBeforeConnect)
            {
                exitCodes.Add((int)ExitCode.GenericFailure);
                retryInterrupted = true;
                break;
            }

            exitCodes.Add(attemptResult.ExitCode);

            int failedThisAttempt = retryFailedTestsPipeServer.FailedTests.Count;
            sawSkippedResults |= retryFailedTestsPipeServer.SkippedTests > 0;
            if (attemptCount == 1)
            {
                // The first attempt runs the full suite, so its counts are the suite's. Skipped tests are tracked
                // separately because a retry attempt re-runs only the failed set, so no later attempt can observe
                // them — and because "total" must include them to line up with the platform run summary.
                //
                // An attempt that dies before its session finishes never reports counts, leaving them at zero. That
                // is recorded rather than presented as a suite of zero tests, which would print a total smaller
                // than the failed count.
                suiteCountsKnown = retryFailedTestsPipeServer.CountsReported;
                suiteTotalTests = retryFailedTestsPipeServer.TotalTestRan + retryFailedTestsPipeServer.SkippedTests;
                suiteSkippedTests = retryFailedTestsPipeServer.SkippedTests;
            }

            // Results this attempt actually observed. Reporting counts is not the same as having run something: an
            // attempt whose filter matched nothing reports a well-formed set of zeros, which must not be mistaken
            // for "nothing is failing any more".
            int observedResults = retryFailedTestsPipeServer.TotalTestRan + retryFailedTestsPipeServer.SkippedTests;
            bool attemptObservedResults = retryFailedTestsPipeServer.CountsReported && observedResults > 0;

            if (pendingRetriedTests.Count > 0)
            {
                if (attemptObservedResults)
                {
                    // Scheduling a retry is not enough to count it: the child can die before the selected tests
                    // produce results. Commit the pending retry only after the following attempt reports counts.
                    foreach (KeyValuePair<string, string> retriedTest in pendingRetriedTests)
                    {
                        retriedTests[retriedTest.Key] = retriedTest.Value;
                    }

                    retriedExecutions += observedResults;
                }

                pendingRetriedTests.Clear();
            }

            // Whatever this attempt recovered is final: a recovered test is not carried into the next attempt's
            // failed set, so it is never re-run and cannot regress.
            foreach (string recoveredUid in retryFailedTestsPipeServer.RecoveredTests)
            {
                if (retriedTests.TryGetValue(recoveredUid, out string? displayName))
                {
                    flakyTestsByUid[recoveredUid] = displayName;
                }
            }

            // A retried test that came back skipped also stops being retried, but it never passed, so it is not
            // reported as flaky above. Its outcome for the run therefore moved from failed to skipped while the
            // suite's skipped count — captured on the first attempt, where it was still failing — does not follow,
            // so it lands in the derived succeeded count instead. Correcting that needs the first attempt's
            // per-test skipped breakdown, because a folded data-driven test can contribute both failing and
            // skipped results under one uid and would otherwise be double-counted. Left as a known imprecision:
            // the total and failed counts stay correct and the block always adds up, which a naive adjustment
            // did not.

            // The run's failing count always reflects the most recent attempt that actually ran something, which
            // re-ran every test still failing. Counted per result so it stays in the same unit as the total. An
            // attempt that died before its session finished, or that ran nothing because its filter matched no
            // test, must not silently reset this to zero: that would render a red verdict above "failed: 0" and
            // derive the still-failing tests as succeeded.
            if (attemptObservedResults)
            {
                finalFailedTestResults = retryFailedTestsPipeServer.FailedTestResults;
            }

            if (attemptResult.ExitCode != (int)ExitCode.Success)
            {
                if (attemptResult.ExitCode != (int)ExitCode.AtLeastOneTestFailed)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedWithWrongExitCode, attemptResult.ExitCode)), cancellationToken).ConfigureAwait(false);
                    retryInterrupted = true;
                    break;
                }

                // Check thresholds only on the first attempt (computed against the full suite).
                if (attemptCount == 1 && await RetryThresholdPolicy.EvaluateAsync(_commandLineOptions, this, outputDevice, retryFailedTestsPipeServer, cancellationToken).ConfigureAwait(false))
                {
                    thresholdPolicyKickedIn = true;
                    break;
                }

                // Only announce an attempt as "retrying" when another attempt will actually follow; the final
                // failed attempt is reported by the summary verdict instead. Amber (not red) keeps mid-run
                // failures visually "expected", reserving red for the give-up summary.
                bool willRetry = attemptCount < userMaxRetryCount + 1;
                if (willRetry)
                {
                    await outputDevice.DisplayAsync(
                        this,
                        new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryAttemptFailedWillRetry, attemptCount, userMaxRetryCount + 1, failedThisAttempt))
                        {
                            ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow },
                        },
                        cancellationToken).ConfigureAwait(false);

                    // Keep the scheduled retry contribution pending. The next attempt may crash before producing
                    // test results, and such a launch must not be reported as an observed retry.
                    pendingRetriedTests.Clear();
                    foreach (KeyValuePair<string, string> failedTest in retryFailedTestsPipeServer.FailedTests)
                    {
                        pendingRetriedTests[failedTest.Key] = failedTest.Value;
                    }
                }

                lastListOfFailedId = [.. retryFailedTestsPipeServer.FailedTests.Keys];
            }
            else
            {
                break;
            }
        }

        orchestrationStopwatch.Stop();

        // The summary is reported on every path, including the ones that stopped retrying early: knowing what the
        // run ended up with (and which tests were flaky) is exactly as useful when the threshold policy disabled
        // retrying or an attempt exited unexpectedly as when the retry loop ran to completion.
        List<string> flakyTests = [.. flakyTestsByUid.Values];
        flakyTests.Sort(StringComparer.Ordinal);

        await RetrySummaryReporter.ReportSummaryAsync(
            this,
            outputDevice,
            new RetryRunSummary
            {
                ExitCodes = exitCodes,
                AttemptCount = attemptCount,
                UserMaxRetryCount = userMaxRetryCount,
                SuiteCountsKnown = suiteCountsKnown,
                SuiteTotalTests = suiteTotalTests,
                SuiteSkippedTests = suiteSkippedTests,
                FinalFailedTests = finalFailedTestResults,
                RetriedTests = retriedTests.Count,
                RetriedExecutions = retriedExecutions,
                FlakyTests = flakyTests,
                ShowFlakyTests = FlakyTestsReportingOptions.IsEnabled(_commandLineOptions),
                StoppedEarly = (thresholdPolicyKickedIn || retryInterrupted) && attemptCount < userMaxRetryCount + 1,
                Elapsed = orchestrationStopwatch.Elapsed,
            },
            cancellationToken).ConfigureAwait(false);

        ApplicationStateGuard.Ensure(currentTryResultFolder is not null);

        string postProcessingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"testfx-retry-postprocess-{Guid.NewGuid():N}");
        try
        {
            // Once retries run, any skipped result makes the final passed/skipped split ambiguous: a folded UID can
            // contain both failed and skipped rows, and a retry can move a row in either direction. Fail closed rather
            // than publishing authoritative-looking counts until the retry protocol carries per-UID row outcomes.
            bool logicalPassedAndSkippedCountsKnown = attemptCount == 1 || !sawSkippedResults;
            ArtifactPostProcessingRunSummary? artifactRunSummary = suiteCountsKnown
                && logicalPassedAndSkippedCountsKnown
                && finalFailedTestResults + suiteSkippedTests <= suiteTotalTests
                    ? new ArtifactPostProcessingRunSummary(
                        suiteTotalTests,
                        suiteTotalTests - finalFailedTestResults - suiteSkippedTests,
                        finalFailedTestResults,
                        suiteSkippedTests,
                        orchestrationStopwatch.Elapsed,
                        exitCodes[^1],
                        testModuleCount: 1)
                    : null;
            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                _serviceProvider,
                this,
                outputDevice,
                logger,
                attemptArtifacts,
                attemptCount,
                artifactRunSummary,
                postProcessingDirectory,
                cancellationToken).ConfigureAwait(false);

            await RetrySummaryReporter.MoveArtifactsAsync(
                this,
                outputDevice,
                fileSystem,
                logger,
                currentTryResultFolder,
                resultDirectory,
                replacements,
                cancellationToken).ConfigureAwait(false);
            RetryArtifactProcessor.PublishExternalArtifacts(
                fileSystem,
                attemptArtifacts,
                attemptCount,
                replacements);
        }
        finally
        {
            try
            {
                if (Directory.Exists(postProcessingDirectory))
                {
                    Directory.Delete(postProcessingDirectory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await logger.LogWarningAsync($"Failed to clean retry artifact post-processing directory '{postProcessingDirectory}': {ex}").ConfigureAwait(false);
            }
        }

        return exitCodes[^1];
    }

    // Copied from HotReloadTestHostTestFrameworkInvoker
    private static bool IsHotReloadEnabled(IEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    internal static async Task LogResponseFileFallbackWarningAsync(
        ILogger logger,
        string[] originalExecutableArguments,
        List<string> finalArguments,
        string generatedResponseFilePath)
    {
        if (originalExecutableArguments.Any(argument => argument.StartsWith("@", StringComparison.Ordinal))
            && !finalArguments.Contains($"@{generatedResponseFilePath}"))
        {
            await logger.LogWarningAsync(
                "Retry arguments could not be regenerated in a response file because an argument contains a literal double quote. "
                + "The retry command line may exceed the operating system limit.").ConfigureAwait(false);
        }
    }

    private static void CollectRecoveredArtifacts(
        IFileSystem fileSystem,
        string manifestPath,
        List<ArtifactRequest> artifacts,
        ILogger logger)
    {
        try
        {
            if (!fileSystem.ExistFile(manifestPath))
            {
                return;
            }

            using IFileStream stream = fileSystem.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var reader = new BoundedManifestLineReader(stream.Stream);
            int recordCount = 0;
            while (recordCount++ < MaxRecoveredArtifactManifestRecords)
            {
                BoundedManifestLineReadResult readResult = reader.ReadLine(out string line);
                if (readResult == BoundedManifestLineReadResult.End)
                {
                    return;
                }

                if (readResult == BoundedManifestLineReadResult.LimitExceeded)
                {
                    logger.LogWarning($"Stopped reading recovered retry artifact manifest '{manifestPath}' because it exceeded a configured size limit.");
                    return;
                }

                int separatorIndex = line.IndexOf('\t');
                if (separatorIndex <= 0)
                {
                    logger.LogWarning($"Ignoring malformed recovered retry artifact manifest entry in '{manifestPath}'.");
                    continue;
                }

                try
                {
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, separatorIndex)));
                    string encodedKind = line.Substring(separatorIndex + 1);
                    string? kind = encodedKind == "-"
                        ? null
                        : Encoding.UTF8.GetString(Convert.FromBase64String(encodedKind));
                    if (path.Length > MaxRecoveredArtifactPathChars
                        || kind?.Length > MaxRecoveredArtifactKindChars)
                    {
                        logger.LogWarning($"Ignoring oversized recovered retry artifact manifest entry in '{manifestPath}'.");
                        continue;
                    }

                    if (!fileSystem.ExistFile(path))
                    {
                        logger.LogWarning($"Ignoring recovered retry artifact '{path}' because it does not exist.");
                        continue;
                    }

                    if (kind is not null)
                    {
                        artifacts.RemoveAll(artifact => string.Equals(artifact.Kind, kind, StringComparison.Ordinal));
                    }
                    else if (artifacts.Any(artifact => string.Equals(artifact.Path, path, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    artifacts.Add(new ArtifactRequest(path, kind));
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException or PathTooLongException)
                {
                    logger.LogWarning($"Ignoring malformed recovered retry artifact manifest entry in '{manifestPath}': {ex.Message}");
                }
            }

            logger.LogWarning($"Stopped reading recovered retry artifact manifest '{manifestPath}' after the maximum of {MaxRecoveredArtifactManifestRecords} records.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning($"Failed to read recovered retry artifact manifest '{manifestPath}': {ex}");
        }
        finally
        {
            try
            {
                if (fileSystem.ExistFile(manifestPath))
                {
                    fileSystem.DeleteFile(manifestPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning($"Failed to delete recovered retry artifact manifest '{manifestPath}': {ex}");
            }
        }
    }

    private enum BoundedManifestLineReadResult
    {
        Line,
        End,
        LimitExceeded,
    }

    private sealed class BoundedManifestLineReader(Stream stream)
    {
        private const int BufferSize = 8192;

        private readonly byte[] _readBuffer = new byte[BufferSize];
        private readonly byte[] _lineBuffer = new byte[MaxRecoveredArtifactManifestLineBytes];
        private int _readOffset;
        private int _readCount;
        private long _bytesRead;

        public BoundedManifestLineReadResult ReadLine(out string line)
        {
            int lineLength = 0;
            while (TryReadByte(out byte value))
            {
                if (++_bytesRead > MaxRecoveredArtifactManifestBytes)
                {
                    line = string.Empty;
                    return BoundedManifestLineReadResult.LimitExceeded;
                }

                if (value == (byte)'\n')
                {
                    return DecodeLine(lineLength, out line);
                }

                if (lineLength >= MaxRecoveredArtifactManifestLineBytes)
                {
                    line = string.Empty;
                    return BoundedManifestLineReadResult.LimitExceeded;
                }

                _lineBuffer[lineLength++] = value;
            }

            if (lineLength == 0)
            {
                line = string.Empty;
                return BoundedManifestLineReadResult.End;
            }

            return DecodeLine(lineLength, out line);
        }

        private BoundedManifestLineReadResult DecodeLine(int lineLength, out string line)
        {
            if (lineLength > 0 && _lineBuffer[lineLength - 1] == (byte)'\r')
            {
                lineLength--;
            }

            line = Encoding.UTF8.GetString(_lineBuffer, 0, lineLength);
            return BoundedManifestLineReadResult.Line;
        }

        private bool TryReadByte(out byte value)
        {
            if (_readOffset >= _readCount)
            {
                _readCount = stream.Read(_readBuffer, 0, _readBuffer.Length);
                _readOffset = 0;
                if (_readCount == 0)
                {
                    value = default;
                    return false;
                }
            }

            value = _readBuffer[_readOffset++];
            return true;
        }
    }
}
