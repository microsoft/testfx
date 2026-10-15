// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Browser.Resources;

namespace Microsoft.Testing.Platform.Browser;

internal static class BrowserSupervisor
{
    private static readonly TimeSpan WorkerStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(10);

    internal const string WorkerReadyMarker = "__MTP_BROWSER_WORKER_READY__";
    internal const string StartWorkerCommand = "start";
    internal const string CancelWorkerCommand = "cancel";

    public static async Task<int> RunAsync(string[] args)
    {
        WorkerProcessContainment? containment = null;
        Process? worker = null;
        Task? stderr = null;
        bool containmentAttached = false;
        try
        {
            containment = WorkerProcessContainment.Create();
            worker = StartWorker(args);
            stderr = CopyAsync(worker.StandardError, Console.Error);
            string? ready = await worker.StandardOutput.ReadLineAsync()
                .WaitAsync(WorkerStartupTimeout).ConfigureAwait(false);
            if (ready != WorkerReadyMarker)
            {
                throw new BrowserLauncherException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.BrowserWorkerNotReady,
                        ready ?? "<end-of-stream>"));
            }

            containment.Attach(worker);
            containmentAttached = true;
            await worker.StandardInput.WriteLineAsync(StartWorkerCommand).ConfigureAwait(false);
            await worker.StandardInput.FlushAsync().ConfigureAwait(false);

            Task stdout = CopyAsync(worker.StandardOutput, Console.Out);
            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _ = cancellationTokenSource.CancelAsync();
            };
            Console.CancelKeyPress += cancelHandler;
            using PosixSignalRegistration? interruptRegistration = RegisterPosixSignal(
                PosixSignal.SIGINT,
                cancellationTokenSource);
            using PosixSignalRegistration? terminationRegistration = RegisterPosixSignal(
                PosixSignal.SIGTERM,
                cancellationTokenSource);

            try
            {
                Task workerExit = worker.WaitForExitAsync();
                Task completed = await Task.WhenAny(
                    workerExit,
                    Task.Delay(Timeout.InfiniteTimeSpan, cancellationTokenSource.Token)).ConfigureAwait(false);
                if (completed != workerExit)
                {
                    await TrySendWorkerCommandAsync(worker, CancelWorkerCommand).ConfigureAwait(false);
                    try
                    {
                        await workerExit.WaitAsync(WorkerShutdownTimeout).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        await containment.TerminateAsync(worker, WorkerShutdownTimeout).ConfigureAwait(false);
                    }
                }

                await workerExit.ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr!).WaitAsync(WorkerShutdownTimeout).ConfigureAwait(false);
                return worker.ExitCode;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.ErrorPrefix,
                    ex)).ConfigureAwait(false);
            if (worker is { HasExited: false })
            {
                try
                {
                    if (containmentAttached)
                    {
                        await containment!.TerminateAsync(worker, WorkerShutdownTimeout).ConfigureAwait(false);
                    }
                    else
                    {
                        worker.Kill(entireProcessTree: true);
                        await worker.WaitForExitAsync().WaitAsync(WorkerShutdownTimeout).ConfigureAwait(false);
                    }
                }
                catch (Exception cleanupException)
                {
                    await Console.Error.WriteLineAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            ExtensionResources.BrowserWorkerTerminationFailed,
                            cleanupException.Message)).ConfigureAwait(false);
                }
            }

            return 1;
        }
        finally
        {
            worker?.StandardInput.Close();
            if (stderr is not null)
            {
                try
                {
                    await stderr.WaitAsync(WorkerShutdownTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException)
                {
                    await Console.Error.WriteLineAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            ExtensionResources.BrowserWorkerDiagnosticsReadFailed,
                            ex.Message)).ConfigureAwait(false);
                }
            }

            worker?.Dispose();
            containment?.Dispose();
        }
    }

    private static Process StartWorker(string[] args)
    {
        string processPath = Environment.ProcessPath
            ?? throw new BrowserLauncherException(ExtensionResources.BrowserWorkerProcessPathUnavailable);
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add(assemblyPath);
        }

        startInfo.ArgumentList.Add("--internal-browser-worker");
        foreach (string argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new BrowserLauncherException(ExtensionResources.BrowserWorkerStartFailed);
    }

    private static async Task CopyAsync(StreamReader reader, TextWriter writer)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static async Task TrySendWorkerCommandAsync(Process worker, string command)
    {
        try
        {
            await worker.StandardInput.WriteLineAsync(command).ConfigureAwait(false);
            await worker.StandardInput.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            // The worker can exit concurrently with a best-effort control command.
        }
    }

    private static PosixSignalRegistration? RegisterPosixSignal(
        PosixSignal signal,
        CancellationTokenSource cancellationTokenSource)
        => OperatingSystem.IsWindows()
            ? null
            : PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true;
                _ = cancellationTokenSource.CancelAsync();
            });
}
