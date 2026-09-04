// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed partial class RetryOrchestrator
{
    private void InitializeEnvironment()
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
    }

    private int GetUserMaxRetryCount()
    {
        if (!_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, out string[]? cmdRetries))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        ApplicationStateGuard.Ensure(cmdRetries is not null);
        return int.Parse(cmdRetries[0], CultureInfo.InvariantCulture);
    }

    // The orchestrator is the only component that sees every attempt, so emitting the count here makes
    // "how much time do we burn on retries?" answerable without inferring it from duplicated test results.
    private static ICounter<int>? CreateRetryCounter(IPlatformOpenTelemetryService? otelService)
        => otelService?.CreateCounter<int>(
            TestingPlatformSemanticConventions.Metrics.TestRetryCount,
            TestingPlatformSemanticConventions.Units.Count,
            "Number of test cases scheduled for a retry attempt.");

    private TimeSpan? GetRetryDelay()
        => _commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, out string[]? retryDelayArgs)
            && retryDelayArgs is { Length: > 0 }
            && TimeSpanParser.TryParse(retryDelayArgs[0], out TimeSpan parsedDelay)
                ? parsedDelay
                : null;

    private async Task WaitBeforeNextAttemptAsync(
        IOutputDevice outputDevice,
        TimeSpan delay,
        int attemptCount,
        int userMaxRetryCount,
        CancellationToken cancellationToken)
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

    private async Task<RetryTestHostRunner.AttemptResult> ExecuteAttemptAsync(
        IFileSystem fileSystem,
        ILogger logger,
        IOutputDevice outputDevice,
        ExecutableInfo executableInfo,
        string[] executableArguments,
        string[] originalExecutableArguments,
        List<int> indexToCleanup,
        string currentTryResultFolder,
        string retryRootFolder,
        RetryFailedTestsPipeServer retryFailedTestsPipeServer,
        string[]? lastListOfFailedId,
        int attemptCount,
        int userMaxRetryCount,
        CancellationToken cancellationToken)
    {
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

            return await RetryTestHostRunner.RunAttemptAsync(
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
    }

    private async Task ProcessArtifactsAsync(
        IOutputDevice outputDevice,
        IFileSystem fileSystem,
        ILogger logger,
        List<RetryAttemptArtifact> attemptArtifacts,
        int attemptCount,
        bool suiteCountsKnown,
        int suiteTotalTests,
        int suiteSkippedTests,
        int finalFailedTestResults,
        bool sawSkippedResults,
        TimeSpan elapsed,
        int exitCode,
        string? currentTryResultFolder,
        string resultDirectory,
        CancellationToken cancellationToken)
    {
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
                        elapsed,
                        exitCode,
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
    }
}
