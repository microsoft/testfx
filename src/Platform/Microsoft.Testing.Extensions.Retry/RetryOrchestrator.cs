// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryOrchestrator : ITestHostExecutionOrchestrator, IOutputDeviceDataProducer
{
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
        string[] executableArguments = [.. executableInfo.Arguments];
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

        // Retry summary accounting (single-assembly). The orchestrator only learns each attempt's failed UID set and
        // the total number of tests that ran, so the richer passed/skipped split stays in the per-attempt child
        // summaries; here we reconcile the attempts into one headline (mirroring the platform run summary idiom).
        var orchestrationStopwatch = Stopwatch.StartNew();
        int suiteTotalTests = 0;
        int firstAttemptFailedTests = 0;
        int finalFailedTests = 0;
        int retriedExecutions = 0;

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

            // Prepare the pipe server that collects the child process's failed test UIDs.
            using RetryFailedTestsPipeServer retryFailedTestsPipeServer = new(_serviceProvider, lastListOfFailedId ?? [], logger);

            List<string> finalArguments = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
                _fileSystem,
                executableArguments,
                indexToCleanup,
                currentTryResultFolder,
                retryRootFolder,
                retryFailedTestsPipeServer.PipeName,
                lastListOfFailedId,
                attemptCount).ConfigureAwait(false);

            RetryTestHostRunner.AttemptResult attemptResult = await RetryTestHostRunner.RunAttemptAsync(
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

            if (attemptResult.ExitedBeforeConnect)
            {
                return (int)ExitCode.GenericFailure;
            }

            exitCodes.Add(attemptResult.ExitCode);

            int failedThisAttempt = retryFailedTestsPipeServer.FailedUID?.Count ?? 0;
            if (attemptCount == 1)
            {
                // The first attempt runs the full suite, so its total is the suite size that the final summary
                // reconciles against; its failed set is the upper bound for the "flaky" (failed-then-passed) count.
                suiteTotalTests = retryFailedTestsPipeServer.TotalTestRan;
                firstAttemptFailedTests = failedThisAttempt;
            }

            if (attemptResult.ExitCode != (int)ExitCode.Success)
            {
                if (attemptResult.ExitCode != (int)ExitCode.AtLeastOneTestFailed)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedWithWrongExitCode, attemptResult.ExitCode)), cancellationToken).ConfigureAwait(false);
                    retryInterrupted = true;
                    break;
                }

                finalFailedTests = failedThisAttempt;

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

                    // Each retried attempt re-runs exactly this attempt's failed set, so its failed count is the
                    // number of extra executions added by the retry — the platform "(+N retried)" semantics.
                    retriedExecutions += failedThisAttempt;
                }

                lastListOfFailedId = retryFailedTestsPipeServer.FailedUID?.ToArray();
            }
            else
            {
                finalFailedTests = 0;
                break;
            }
        }

        orchestrationStopwatch.Stop();

        if (!thresholdPolicyKickedIn && !retryInterrupted)
        {
            await RetrySummaryReporter.ReportSummaryAsync(
                this,
                outputDevice,
                new RetryRunSummary
                {
                    ExitCodes = exitCodes,
                    AttemptCount = attemptCount,
                    UserMaxRetryCount = userMaxRetryCount,
                    SuiteTotalTests = suiteTotalTests,
                    FirstAttemptFailedTests = firstAttemptFailedTests,
                    FinalFailedTests = finalFailedTests,
                    RetriedExecutions = retriedExecutions,
                    Elapsed = orchestrationStopwatch.Elapsed,
                },
                cancellationToken).ConfigureAwait(false);
        }

        ApplicationStateGuard.Ensure(currentTryResultFolder is not null);

        await RetrySummaryReporter.MoveArtifactsAsync(this, outputDevice, fileSystem, logger, currentTryResultFolder, resultDirectory, cancellationToken).ConfigureAwait(false);

        return exitCodes[^1];
    }

    // Copied from HotReloadTestHostTestFrameworkInvoker
    private static bool IsHotReloadEnabled(IEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";
}
