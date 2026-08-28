// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Owns a single retry attempt's child-process lifecycle: builds the process command line, starts the test host,
/// waits for the retry pipe connection (with hang timeout), and returns the attempt's exit code.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal static class RetryTestHostRunner
{
    private static readonly TimeSpan TestHostTerminationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Result of running one attempt. When <see cref="ExitedBeforeConnect"/> is <see langword="true"/>, the child
    /// process died before it could connect to the retry pipe, and the orchestrator should stop with a generic failure.
    /// </summary>
    public readonly struct AttemptResult
    {
        public required int ExitCode { get; init; }

        public required bool ExitedBeforeConnect { get; init; }
    }

    public static async Task<AttemptResult> RunAttemptAsync(
        IServiceProvider serviceProvider,
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        ILogger logger,
        RetryFailedTestsPipeServer retryFailedTestsPipeServer,
        ExecutableInfo executableInfo,
        List<string> finalArguments,
        int attemptCount,
        int userMaxRetryCount,
        CancellationToken cancellationToken)
    {
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

        // Tell the launched test host which retry attempt it is, so it can report an explicit AttemptNumber in
        // its dotnet test handshake instead of the consumer having to infer it from a change in InstanceId.
        processStartInfo.EnvironmentVariables[EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER] =
            attemptCount.ToString(CultureInfo.InvariantCulture);

        await logger.LogDebugAsync($"Starting test host process, attempt {attemptCount}/{userMaxRetryCount}").ConfigureAwait(false);
        ITestHostLauncher? testHostLauncher = serviceProvider.GetServiceInternal<ITestHostLauncher>();
        using IProcess testHostProcess = testHostLauncher is null
            ? serviceProvider.GetProcessHandler().Start(processStartInfo)
                ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsCannotStartProcessErrorMessage, processStartInfo.FileName))
            : await LaunchUsingCustomLauncherAsync(testHostLauncher, processStartInfo, finalArguments, logger, cancellationToken).ConfigureAwait(false);

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
        if (testHostProcess.HasExited)
        {
#if NET8_0_OR_GREATER
            await processExitedCancellationToken.CancelAsync().ConfigureAwait(false);
#else
            processExitedCancellationToken.Cancel();
#endif
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeoutHelper.DefaultHangTimeSpanTimeout);
            using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

            await logger.LogDebugAsync("Wait connection from the test host process").ConfigureAwait(false);
            Task waitForConnectionTask = retryFailedTestsPipeServer.WaitForConnectionAsync(linkedToken.Token);
            var processExitedTask = Task.Delay(Timeout.InfiniteTimeSpan, processExitedCancellationToken.Token);
            Task completedTask = await Task.WhenAny(waitForConnectionTask, processExitedTask).ConfigureAwait(false);

            // A launcher can return an already-exited handle after the child successfully connected. Prefer
            // that completed connection over the exit notification so the attempt results are still consumed.
            if (completedTask != waitForConnectionTask && !waitForConnectionTask.IsCompleted)
            {
                // ConnectAsync on the client can complete just before the server-side completion is scheduled.
                // Give that already-established connection a brief chance to win over the exit notification.
                Task connectionOrGracePeriod = await Task.WhenAny(
                    waitForConnectionTask,
                    Task.Delay(TimeSpan.FromSeconds(1), linkedToken.Token)).ConfigureAwait(false);
                linkedToken.Token.ThrowIfCancellationRequested();
                completedTask = connectionOrGracePeriod;
            }

            if (completedTask == waitForConnectionTask || waitForConnectionTask.IsCompleted)
            {
#if NETCOREAPP
                await waitForConnectionTask.ConfigureAwait(false);
#else
                await waitForConnectionTask.WithCancellationAsync(linkedToken.Token).ConfigureAwait(false);
#endif
            }
            else
            {
                await outputDevice.DisplayAsync(producer, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestHostProcessExitedBeforeRetryCouldConnect, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                return new AttemptResult { ExitCode = testHostProcess.ExitCode, ExitedBeforeConnect = true };
            }
        }
        catch (OperationCanceledException)
        {
            await TerminateAndWaitForExitAsync(testHostProcess, logger).ConfigureAwait(false);
            throw;
        }
        finally
        {
            testHostProcess.Exited -= exitedHandler;
        }

        await testHostProcess.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        return new AttemptResult { ExitCode = testHostProcess.ExitCode, ExitedBeforeConnect = false };
    }

    private static async Task TerminateAndWaitForExitAsync(IProcess testHostProcess, ILogger logger)
    {
        try
        {
            testHostProcess.Kill();
        }
        catch (Exception ex)
        {
            await logger.LogDebugAsync($"Ignoring failure while terminating the retry test host: {ex}").ConfigureAwait(false);
        }

        using var timeout = new CancellationTokenSource(TestHostTerminationTimeout);
        try
        {
            await testHostProcess.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (testHostProcess is TestHostHandleToProcessAdapter adapter)
            {
                adapter.DeferDisposalUntilExit();
            }

            await logger.LogWarningAsync(
                $"Retry test host did not exit within {TestHostTerminationTimeout} after termination was requested; continuing cancellation.").ConfigureAwait(false);
        }
    }

    private static async Task<IProcess> LaunchUsingCustomLauncherAsync(
        ITestHostLauncher testHostLauncher,
        ProcessStartInfo processStartInfo,
        IReadOnlyList<string> arguments,
        ILogger logger,
        CancellationToken cancellationToken)
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified — populated from a runtime loop.
        Dictionary<string, string?> environmentVariables = new(StringComparer.Ordinal);
#pragma warning restore IDE0028
        foreach (string key in processStartInfo.EnvironmentVariables.Keys)
        {
            environmentVariables[key] = processStartInfo.EnvironmentVariables[key];
        }

        string? workingDirectory = RoslynString.IsNullOrEmpty(processStartInfo.WorkingDirectory) ? null : processStartInfo.WorkingDirectory;
        TestHostLaunchContext context = new(processStartInfo.FileName, arguments, environmentVariables, workingDirectory);

        await logger.LogDebugAsync($"Delegating retry test host launch to '{testHostLauncher.DisplayName}' (UID: {testHostLauncher.Uid})").ConfigureAwait(false);
        ITestHostHandle handle = await testHostLauncher.LaunchTestHostAsync(context, cancellationToken).ConfigureAwait(false);
        await logger.LogDebugAsync($"Retry test host launched by '{testHostLauncher.Uid}' (Identifier: '{handle.Identifier ?? "<none>"}')").ConfigureAwait(false);
        return new TestHostHandleToProcessAdapter(handle);
    }
}
