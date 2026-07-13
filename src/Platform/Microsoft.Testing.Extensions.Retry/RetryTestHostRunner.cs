// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
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
        processStartInfo.Environment[EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER] =
            attemptCount.ToString(CultureInfo.InvariantCulture);

        await logger.LogDebugAsync($"Starting test host process, attempt {attemptCount}/{userMaxRetryCount}").ConfigureAwait(false);
        using IProcess testHostProcess = serviceProvider.GetProcessHandler().Start(processStartInfo)
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
                await outputDevice.DisplayAsync(producer, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestHostProcessExitedBeforeRetryCouldConnect, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
                return new AttemptResult { ExitCode = testHostProcess.ExitCode, ExitedBeforeConnect = true };
            }
        }
        finally
        {
            testHostProcess.Exited -= exitedHandler;
        }

        await testHostProcess.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        return new AttemptResult { ExitCode = testHostProcess.ExitCode, ExitedBeforeConnect = false };
    }
}
