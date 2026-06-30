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

        // Find out the retry args index inside the arguments to after cleanup the command line when we restart
        List<int> indexToCleanup = [];
        string[] executableArguments = [.. executableInfo.Arguments];
        int argIndex = GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, executableArguments);
        if (argIndex < 0)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        indexToCleanup.Add(argIndex);
        indexToCleanup.Add(argIndex + 1);

        argIndex = GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        argIndex = GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        argIndex = GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            if (argIndex + 1 < executableArguments.Length)
            {
                indexToCleanup.Add(argIndex + 1);
            }
        }

        argIndex = GetOptionArgumentIndex(PlatformCommandLineProvider.ResultDirectoryOptionKey, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        // Override the result directory with the attempt one
        string resultDirectory = configuration.GetTestResultDirectory();

        List<int> exitCodes = [];
        IOutputDevice outputDevice = _serviceProvider.GetOutputDevice();
        IFileSystem fileSystem = _serviceProvider.GetFileSystem();

        int attemptCount = 0;
        List<string> finalArguments = [];
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
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryWaitingBeforeNextAttempt, FormatDelay(delay), attemptCount, userMaxRetryCount + 1))
                    {
                        ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
                    },
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            // Cleanup the arguments
            for (int i = 0; i < executableArguments.Length; i++)
            {
                if (indexToCleanup.Contains(i))
                {
                    continue;
                }

                finalArguments.Add(executableArguments[i]);
            }

            // Fix result folder
            currentTryResultFolder = Path.Combine(retryRootFolder, attemptCount.ToString(CultureInfo.InvariantCulture));
            finalArguments.Add($"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}");
            finalArguments.Add(currentTryResultFolder);

            // Prepare the pipeserver
            using RetryFailedTestsPipeServer retryFailedTestsPipeServer = new(_serviceProvider, lastListOfFailedId ?? [], logger);
            finalArguments.Add($"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}");
            finalArguments.Add(retryFailedTestsPipeServer.PipeName);

            // When retrying, replace any existing test filter with --filter-uid for the failed tests
            if (lastListOfFailedId is { Length: > 0 })
            {
                RemoveOption(finalArguments, TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter);
                RemoveOption(finalArguments, PlatformCommandLineProvider.FilterUidOptionKey);

                // Strip --minimum-expected-tests on retry attempts: a retry only re-runs the previously
                // failed tests, so propagating the original threshold (computed against the full test
                // set) would always trip the policy and fail the run. See issue #5639.
                RemoveOption(finalArguments, PlatformCommandLineProvider.MinimumExpectedTestsOptionKey);

                // The RSP parser (ResponseFileHelper.SplitCommandLine) strips all '"' characters
                // from tokens, so UIDs containing literal '"' (e.g. parameterized tests with
                // string arguments that include double quotes) cannot safely round-trip through
                // a response file. In that case we must always use inline arguments.
                bool hasUidsWithQuotes = false;
                foreach (string uid in lastListOfFailedId)
                {
                    if (uid.IndexOf('"') >= 0)
                    {
                        hasUidsWithQuotes = true;
                        break;
                    }
                }

                bool useResponseFile = false;
                if (!hasUidsWithQuotes)
                {
                    // Estimate command line length to avoid hitting OS limits (~32K on Windows).
                    // Add per-argument overhead to account for PasteArguments quoting on pre-.NET 8
                    // targets where each argument may gain wrapping quotes and a separator space.
                    const int CommandLineLengthLimit = 30_000;
                    const int PerArgumentOverhead = 3;
                    int predictedLength = 0;
                    foreach (string arg in finalArguments)
                    {
                        predictedLength += arg.Length + PerArgumentOverhead;
                    }

                    predictedLength += 2 + PlatformCommandLineProvider.FilterUidOptionKey.Length + 1;
                    foreach (string uid in lastListOfFailedId)
                    {
                        predictedLength += uid.Length + PerArgumentOverhead;
                    }

                    useResponseFile = predictedLength > CommandLineLengthLimit;
                }

                if (!useResponseFile)
                {
                    finalArguments.Add($"--{PlatformCommandLineProvider.FilterUidOptionKey}");
                    finalArguments.AddRange(lastListOfFailedId);
                }
                else
                {
                    // Use a response file to avoid exceeding command-line length limits.
                    // Write to retryRootFolder (not the per-attempt folder) so it won't be included
                    // in the final results move.
                    string responseFilePath = Path.Combine(retryRootFolder, $"retry-filter-uids-{attemptCount}.rsp");
                    using (IFileStream stream = _fileSystem.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
                    using (var writer = new StreamWriter(stream.Stream))
                    {
                        // Write all UIDs on a single line, each quoted. The RSP parser splits
                        // by whitespace and uses '"' for grouping, so quoting handles UIDs
                        // containing whitespace or starting with '#' (comment marker).
                        await writer.WriteAsync($"--{PlatformCommandLineProvider.FilterUidOptionKey}").ConfigureAwait(false);
                        foreach (string uid in lastListOfFailedId)
                        {
                            await writer.WriteAsync($" \"{uid}\"").ConfigureAwait(false);
                        }

                        await writer.WriteLineAsync().ConfigureAwait(false);
                    }

                    finalArguments.Add($"@{responseFilePath}");
                }
            }

#if NET8_0_OR_GREATER
            // On net8.0+, we can pass the arguments as a collection directly to ProcessStartInfo.
            // When passing the collection, it's expected to be unescaped, so we pass what we have directly.
            List<string> arguments = finalArguments;
#else
            // Current target framework (.NET Framework and .NET Standard 2.0) only supports arguments as a single string.
            // In this case, escaping is essential. For example, one of the arguments could already contain spaces.
            // PasteArguments is borrowed from dotnet/runtime.
            var builder = new StringBuilder();
            foreach (string arg in finalArguments)
            {
                PasteArguments.AppendArgument(builder, arg);
            }

            string arguments = builder.ToString();
#endif

            // Prepare the process start
            ProcessStartInfo processStartInfo = new(executableInfo.FilePath, arguments)
            {
                UseShellExecute = false,
            };

            await logger.LogDebugAsync($"Starting test host process, attempt {attemptCount}/{userMaxRetryCount}").ConfigureAwait(false);
            using IProcess testHostProcess = _serviceProvider.GetProcessHandler().Start(processStartInfo)
                ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsCannotStartProcessErrorMessage, processStartInfo.FileName));

            using var processExitedCancellationToken = new CancellationTokenSource();
            EventHandler exitedHandler = (sender, e) =>
            {
                try
                {
                    processExitedCancellationToken.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    // The handler can race with the end-of-iteration cleanup: if the OS process
                    // exit signal is queued to the thread pool before we detach the handler but
                    // executes after the CTS has been disposed, Cancel() throws. Log at debug
                    // level so an unexpected pattern stays observable without becoming a fatal
                    // failure in the retry loop.
                    logger.LogDebug($"CancellationTokenSource already disposed when process exited: {ex.Message}");
                }

                logger.LogDebug($"Test host process exited, PID: '{(sender as Process)?.Id}'");
            };

            testHostProcess.Exited += exitedHandler;
            try
            {
                using var timeout = new CancellationTokenSource(TimeoutHelper.DefaultHangTimeSpanTimeout);
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
                using var linkedToken2 = CancellationTokenSource.CreateLinkedTokenSource(linkedToken.Token, processExitedCancellationToken.Token);

                await logger.LogDebugAsync("Wait connection from the test host process").ConfigureAwait(false);
                try
                {
#if NETCOREAPP
                    await retryFailedTestsPipeServer.WaitForConnectionAsync(linkedToken2.Token).ConfigureAwait(false);
#else
                    // We don't know why but if the cancellation is called quickly in `testHostProcess.Exited`: `processExitedCancellationToken.Cancel();` for netfx we stuck sometime here, like if
                    // the token we pass to the named pipe is not "correctly" verified inside the pipe implementation self.
                    // We fallback with our custom agnostic cancellation mechanism in that case.
                    // We see it happen only in .NET FX and not in .NET Core so for now we don't do it for core.
                    await retryFailedTestsPipeServer.WaitForConnectionAsync(linkedToken2.Token).WithCancellationAsync(linkedToken2.Token).ConfigureAwait(false);
#endif
                }
                catch (OperationCanceledException) when (processExitedCancellationToken.IsCancellationRequested)
                {
                    await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestHostProcessExitedBeforeRetryCouldConnect, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                    return (int)ExitCode.GenericFailure;
                }
            }
            finally
            {
                testHostProcess.Exited -= exitedHandler;
            }

            await testHostProcess.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            exitCodes.Add(testHostProcess.ExitCode);

            int failedThisAttempt = retryFailedTestsPipeServer.FailedUID?.Count ?? 0;
            if (attemptCount == 1)
            {
                // The first attempt runs the full suite, so its total is the suite size that the final summary
                // reconciles against; its failed set is the upper bound for the "flaky" (failed-then-passed) count.
                suiteTotalTests = retryFailedTestsPipeServer.TotalTestRan;
                firstAttemptFailedTests = failedThisAttempt;
            }

            if (testHostProcess.ExitCode != (int)ExitCode.Success)
            {
                if (testHostProcess.ExitCode != (int)ExitCode.AtLeastOneTestFailed)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedWithWrongExitCode, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                    retryInterrupted = true;
                    break;
                }

                finalFailedTests = failedThisAttempt;

                // Check thresholds
                if (attemptCount == 1)
                {
                    double? maxFailedTests = null;
                    double? maxPercentage = null;
                    double? maxCount = null;
                    if (_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, out string[]? retryFailedTestsMaxPercentage))
                    {
                        maxPercentage = double.Parse(retryFailedTestsMaxPercentage[0], CultureInfo.InvariantCulture);
                        maxFailedTests = maxPercentage / 100 * retryFailedTestsPipeServer.TotalTestRan;
                    }

                    if (_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, out string[]? retryFailedTestsMaxCount))
                    {
                        maxCount = double.Parse(retryFailedTestsMaxCount[0], CultureInfo.InvariantCulture);
                        maxFailedTests = maxCount.Value;
                    }

                    // If threshold policy enable
                    if (maxFailedTests is not null)
                    {
                        if ((retryFailedTestsPipeServer.FailedUID?.Count ?? 0) > maxFailedTests)
                        {
                            thresholdPolicyKickedIn = true;
                            StringBuilder explanation = new();
                            explanation.AppendLine(ExtensionResources.FailureThresholdPolicy);
                            if (maxPercentage is not null)
                            {
                                double failedPercentage = Math.Round(retryFailedTestsPipeServer.FailedUID!.Count / (double)retryFailedTestsPipeServer.TotalTestRan * 100, 2);
                                explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxPercentage, maxPercentage, failedPercentage, retryFailedTestsPipeServer.FailedUID.Count, retryFailedTestsPipeServer.TotalTestRan));
                            }

                            if (maxCount is not null)
                            {
                                explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxCount, maxCount, retryFailedTestsPipeServer.FailedUID!.Count));
                            }

                            await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(explanation.ToString()), cancellationToken).ConfigureAwait(false);
                            break;
                        }
                    }
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

                finalArguments.Clear();
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
            bool runSucceeded = exitCodes[^1] == (int)ExitCode.Success;
            int flakyTests = Math.Max(0, firstAttemptFailedTests - finalFailedTests);
            int totalAttempts = userMaxRetryCount + 1;

            // Headline verdict, colored by the FINAL outcome so a run rescued by retry reads as green.
            if (runSucceeded)
            {
                string header = attemptCount == 1
                    ? ExtensionResources.RetrySummaryPassedNoRetry
                    : string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryPassed, attemptCount, totalAttempts);
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(header) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGreen } }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailed, attemptCount, totalAttempts)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } }, cancellationToken).ConfigureAwait(false);
            }

            if (!runSucceeded && finalFailedTests > 0)
            {
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedLine, finalFailedTests)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } }, cancellationToken).ConfigureAwait(false);
            }

            // "flaky" = failed at least once but eventually passed — the headline value of the retry feature.
            if (flakyTests > 0)
            {
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFlakyLine, flakyTests)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow } }, cancellationToken).ConfigureAwait(false);
            }

            string totalLine = string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryTotalLine, suiteTotalTests);
            if (retriedExecutions > 0)
            {
                totalLine += string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryRetriedSuffix, retriedExecutions);
            }

            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(totalLine), cancellationToken).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryDurationLine, FormatDuration(orchestrationStopwatch.Elapsed))), cancellationToken).ConfigureAwait(false);
        }

        ApplicationStateGuard.Ensure(currentTryResultFolder is not null);

        string[] filesToMove = _fileSystem.GetFiles(currentTryResultFolder, "*.*", SearchOption.AllDirectories);
        if (filesToMove.Length > 0)
        {
            // Move last attempt assets. The per-file detail is demoted to a debug log; the user-facing output is a
            // single collapsed line so a large artifact set no longer spams the console.
            foreach (string file in filesToMove)
            {
                string finalFileLocation = file.Replace(currentTryResultFolder, resultDirectory);

                // Create the directory if missing
                fileSystem.CreateDirectory(Path.GetDirectoryName(finalFileLocation)!);

                logger.LogDebug($"Moving file '{file}' to '{finalFileLocation}'");
#if NETCOREAPP
                fileSystem.MoveFile(file, finalFileLocation, overwrite: true);
#else
                fileSystem.CopyFile(file, finalFileLocation, overwrite: true);
                fileSystem.DeleteFile(file);
#endif
            }

            await outputDevice.DisplayAsync(
                this,
                new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryArtifactsMoved, filesToMove.Length, resultDirectory))
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
                },
                cancellationToken).ConfigureAwait(false);
        }

        return exitCodes[^1];
    }

    // Copied from HotReloadTestHostTestFrameworkInvoker
    private static bool IsHotReloadEnabled(IEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    private static int GetOptionArgumentIndex(string optionName, string[] executableArgs)
    {
        int index = Array.IndexOf(executableArgs, "-" + optionName);
        if (index >= 0)
        {
            return index;
        }

        index = Array.IndexOf(executableArgs, "--" + optionName);
        return index >= 0 ? index : -1;
    }

    private static void RemoveOption(List<string> arguments, string optionName)
    {
        string longForm = $"--{optionName}";
        string shortForm = $"-{optionName}";

        // Remove all occurrences since options like --filter-uid can appear multiple times.
        // Also handle --option=value and --option:value forms produced by the command-line parser.
        while (true)
        {
            int idx = -1;
            for (int i = 0; i < arguments.Count; i++)
            {
                string arg = arguments[i];
                if (arg == longForm || arg == shortForm
                    || arg.StartsWith(longForm + "=", StringComparison.Ordinal) || arg.StartsWith(longForm + ":", StringComparison.Ordinal)
                    || arg.StartsWith(shortForm + "=", StringComparison.Ordinal) || arg.StartsWith(shortForm + ":", StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                break;
            }

            arguments.RemoveAt(idx);

            // Always remove subsequent non-option arguments (the option's values),
            // even when the first value was provided inline with = or :, because
            // multi-arity options (e.g. --filter-uid=1 2) can have trailing values.
            while (idx < arguments.Count && (arguments[idx].Length == 0 || arguments[idx][0] != '-'))
            {
                arguments.RemoveAt(idx);
            }
        }
    }

    // Renders a retry delay the same way the --retry-failed-tests-delay option accepts it (e.g. '500ms', '1s')
    // so the displayed wait is consistent with how the user configured it.
    private static string FormatDelay(TimeSpan delay)
        => delay.TotalMilliseconds < 1000
            ? string.Format(CultureInfo.CurrentCulture, "{0}ms", (int)delay.TotalMilliseconds)
            : delay.TotalSeconds < 60 && delay.Milliseconds == 0
                ? string.Format(CultureInfo.CurrentCulture, "{0}s", (int)delay.TotalSeconds)
                : delay.ToString("c", CultureInfo.CurrentCulture);

    // Compact, human-friendly duration for the retry summary (milliseconds / seconds / minutes-seconds).
    private static string FormatDuration(TimeSpan duration)
        => duration.TotalSeconds < 1
            ? string.Format(CultureInfo.CurrentCulture, "{0}ms", (int)duration.TotalMilliseconds)
            : duration.TotalMinutes < 1
                ? string.Format(CultureInfo.CurrentCulture, "{0:0.000}s", duration.TotalSeconds)
                : string.Format(CultureInfo.CurrentCulture, "{0}m {1:00}s", (int)duration.TotalMinutes, duration.Seconds);
}
