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

internal sealed class RetryOrchestrator : ITestHostOrchestrator, IOutputDeviceDataProducer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;

    public RetryOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
    }

    public string Uid => nameof(RetryOrchestrator);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName));

    private static string CreateRetriesDirectory(string resultDirectory)
    {
        Exception? lastException = null;
        // Quite arbitrary. Keep trying to create the directory for 10 times.
        for (int i = 0; i < 10; i++)
        {
            string retryRootFolder = Path.Combine(resultDirectory, "Retries", RandomId.Next());
            if (Directory.Exists(retryRootFolder))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(retryRootFolder);
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

    public async Task<int> OrchestrateTestHostExecutionAsync()
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
        CancellationToken cancellationToken = _serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        while (attemptCount < userMaxRetryCount + 1)
        {
            attemptCount++;

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

            // Prepare the process start
            ProcessStartInfo processStartInfo = new()
            {
                FileName = executableInfo.FilePath,
#if !NETCOREAPP
                UseShellExecute = false,
#endif
            };

            foreach (string argument in finalArguments)
            {
#if !NETCOREAPP
                processStartInfo.Arguments += argument + " ";
#else
                processStartInfo.ArgumentList.Add(argument);
#endif
            }

            await logger.LogDebugAsync($"Starting test host process, attempt {attemptCount}/{userMaxRetryCount}").ConfigureAwait(false);
            IProcess testHostProcess = _serviceProvider.GetProcessHandler().Start(processStartInfo)
                ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsCannotStartProcessErrorMessage, processStartInfo.FileName));

            CancellationTokenSource processExitedCancellationToken = new();
            testHostProcess.Exited += (sender, e) =>
            {
                processExitedCancellationToken.Cancel();
                var processExited = sender as Process;
                logger.LogDebug($"Test host process exited, PID: '{processExited?.Id}'");
            };

            using (var timeout = new CancellationTokenSource(TimeoutHelper.DefaultHangTimeSpanTimeout))
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken))
            using (var linkedToken2 = CancellationTokenSource.CreateLinkedTokenSource(linkedToken.Token, processExitedCancellationToken.Token))
            {
                await logger.LogDebugAsync("Wait connection from the test host process").ConfigureAwait(false);
                try
                {
#if NETCOREAPP
                    await retryFailedTestsPipeServer.WaitForConnectionAsync(linkedToken2.Token).ConfigureAwait(false);
#else
                    // We don't know why but if the cancellation is called quickly in line 171 for netfx we stuck sometime here, like if
                    // the token we pass to the named pipe is not "correctly" verified inside the pipe implementation self.
                    // We fallback with our custom agnostic cancellation mechanism in that case.
                    // We see it happen only in .NET FX and not in .NET Core so for now we don't do it for core.
                    await retryFailedTestsPipeServer.WaitForConnectionAsync(linkedToken2.Token).WithCancellationAsync(linkedToken2.Token).ConfigureAwait(false);
#endif
                }
                catch (OperationCanceledException) when (processExitedCancellationToken.IsCancellationRequested)
                {
                    await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestHostProcessExitedBeforeRetryCouldConnect, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                    return ExitCodes.GenericFailure;
                }
            }

            await testHostProcess.WaitForExitAsync().ConfigureAwait(false);

            exitCodes.Add(testHostProcess.ExitCode);
            if (testHostProcess.ExitCode != ExitCodes.Success)
            {
                if (testHostProcess.ExitCode != ExitCodes.AtLeastOneTestFailed)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedWithWrongExitCode, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                    retryInterrupted = true;
                    break;
                }

                await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailed, retryFailedTestsPipeServer.FailedUID?.Count ?? 0, testHostProcess.ExitCode, attemptCount, userMaxRetryCount + 1)), cancellationToken).ConfigureAwait(false);

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

                finalArguments.Clear();
                lastListOfFailedId = retryFailedTestsPipeServer.FailedUID?.ToArray();
            }
            else
            {
                break;
            }
        }

        if (!thresholdPolicyKickedIn && !retryInterrupted)
        {
            if (exitCodes[^1] != ExitCodes.Success)
            {
                await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedInAllAttempts, userMaxRetryCount + 1)), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteCompletedSuccessfully, attemptCount)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGreen } }, cancellationToken).ConfigureAwait(false);
            }
        }

        ApplicationStateGuard.Ensure(currentTryResultFolder is not null);

        string[] filesToMove = Directory.GetFiles(currentTryResultFolder, "*.*", SearchOption.AllDirectories);
        if (filesToMove.Length > 0)
        {
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(ExtensionResources.MoveFiles), cancellationToken).ConfigureAwait(false);

            // Move last attempt assets
            foreach (string file in filesToMove)
            {
                string finalFileLocation = file.Replace(currentTryResultFolder, resultDirectory);

                // Create the directory if missing
                Directory.CreateDirectory(Path.GetDirectoryName(finalFileLocation)!);

                await outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.MovingFileToLocation, file, finalFileLocation)), cancellationToken).ConfigureAwait(false);
#if NETCOREAPP
                File.Move(file, finalFileLocation, overwrite: true);
#else
                File.Copy(file, finalFileLocation, overwrite: true);
                File.Delete(file);
#endif
            }
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
}
