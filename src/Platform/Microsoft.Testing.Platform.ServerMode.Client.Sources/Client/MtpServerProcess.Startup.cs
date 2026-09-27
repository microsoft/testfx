// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerProcess
{
    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> and waits for it to connect back.
    /// </summary>
    /// <param name="source">
    /// Path to the test application. May be a managed <c>.dll</c> (launched via its sibling apphost when that
    /// apphost is usable on the current OS, otherwise via <c>dotnet &lt;dll&gt;</c>) or a native executable.
    /// </param>
    /// <param name="options">Client options (name, connection timeout, environment, logger).</param>
    public static MtpServerProcess Start(string source, MtpServerClientOptions? options = null)
        => StartAsync(source, options, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> and asynchronously waits for it to connect back.
    /// </summary>
    /// <param name="source">Path to the managed test application or native executable.</param>
    /// <param name="options">Client options (name, connection timeout, environment, logger).</param>
    /// <param name="cancellationToken">Cancels the launch and connection wait.</param>
    public static async Task<MtpServerProcess> StartAsync(
        string source,
        MtpServerClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        cancellationToken.ThrowIfCancellationRequested();
        options ??= new MtpServerClientOptions();
        IMtpClientLogger logger = options.Logger ?? NullMtpClientLogger.Instance;

        // Resolve to an absolute path up front. BuildLaunch derives the working directory from the source
        // directory and then launches the (possibly relative) source; if both stay relative, launching a
        // managed dll as `dotnet <relativeDll>` with the working directory already set to that same
        // relative directory double-nests the lookup. Absolutizing once here keeps the launch and every
        // diagnostic message consistent regardless of the caller's current directory.
        source = Path.GetFullPath(source);

        // The serializers must be registered BEFORE the formatter is created: the .NET
        // System.Text.Json formatter snapshots the registered serializer/deserializer type sets into
        // its per-type engine at construction time.
        IMessageFormatter formatter = MtpServerConnector.CreateFormatter();

        TcpListener? listener = null;
        Process? process = null;
        TcpClient? acceptedClient = null;
        var standardError = new StringBuilder();
        try
        {
            // Everything that can throw (binding the listener, resolving the launch command, starting the
            // process) lives inside the try so the catch can tear down whatever was already created. In
            // particular, if listener.Start() fails to bind or the process fails to start, the listener is
            // stopped rather than leaked.
            listener = MtpServerConnector.StartLoopbackListener(out int port);

            LaunchCommand launch = BuildLaunch(source, port);
            string fileName = launch.FileName;
            string arguments = launch.Arguments;
            string workingDirectory = launch.WorkingDirectory;
            logger.SafeLog(MtpClientLogLevel.Debug, $"Launching MTP server '{fileName} {arguments}' (cwd '{workingDirectory}') listening on port {port}.");

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (KeyValuePair<string, string?> variable in options.EnvironmentVariables)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }

            process = CreateProcess(startInfo, standardError, out Task standardErrorDrained);
            try
            {
                process.Start();
            }
            catch (Win32Exception ex) when (ShouldRetryApphostThroughDotnet(source, launch, ex))
            {
                process.Dispose();

                launch = CreateDotnetLaunch(source, launch.Arguments, workingDirectory);
                startInfo.FileName = launch.FileName;
                startInfo.Arguments = launch.Arguments;
                logger.SafeLog(
                    MtpClientLogLevel.Warning,
                    $"The sibling apphost could not be executed; retrying through '{launch.FileName} {launch.Arguments}'.");

                process = CreateProcess(startInfo, standardError, out standardErrorDrained);
                process.Start();
            }

            process.BeginErrorReadLine();

            // Drain stdout so the child never blocks on a full pipe (banner, diagnostics).
            process.BeginOutputReadLine();

            // Wait for the app to dial back, but poll the process alongside the accept: if the child exits
            // early (bad arguments, startup crash) we fail fast with its exit code + captured stderr instead
            // of blocking the full ConnectionTimeout and then reporting a misleading timeout. Polling
            // process.HasExited (rather than racing the accept against Process.Exited) keeps this free of a
            // TaskCompletionSource ordering race.
            Process startedProcess = process;
            acceptedClient = await MtpServerConnector.AcceptAsync(
                listener,
                token => TryGetProcessStoppedFailureAsync(startedProcess, source, standardError, standardErrorDrained, token),
                token => CreateTimeoutOrStoppedFailureAsync(
                    startedProcess,
                    source,
                    standardError,
                    standardErrorDrained,
                    options.ConnectionTimeout,
                    token),
                options.ConnectionTimeout,
                serverCompletion: null,
                cancellationToken).ConfigureAwait(false);

            // NOTE: the read loop is intentionally NOT started here. The owner (MtpServerClient) wires its
            // notification/server-request handlers first and then calls Connection.Start(), so no server ->
            // client message can slip past before the handlers are attached.
            MtpJsonRpcConnection connection = MtpServerConnector.CreateConnection(acceptedClient, formatter, logger);
            return new MtpServerProcess(listener, process, acceptedClient, connection, standardError, logger);
        }
        catch
        {
            if (process is not null)
            {
                SafeKill(process, logger);
                process.Dispose();
            }

            acceptedClient?.Dispose();

            if (listener is not null)
            {
                MtpServerConnector.SafeStop(listener, logger);
            }

            throw;
        }
    }

    /// <summary>
    /// Gets the captured standard-error output of the launched application.
    /// </summary>
    public string GetStandardError()
        => GetStandardError(_standardError);

    private static Task<Exception?> TryGetProcessStoppedFailureAsync(
        Process process,
        string source,
        StringBuilder standardError,
        Task standardErrorDrained,
        CancellationToken cancellationToken)
        => !process.HasExited
            ? Task.FromResult<Exception?>(null)
            : WaitForStandardErrorAndCreateEarlyExitFailureAsync(
                process,
                source,
                standardError,
                standardErrorDrained,
                cancellationToken);

    private static async Task<Exception?> WaitForStandardErrorAndCreateEarlyExitFailureAsync(
        Process process,
        string source,
        StringBuilder standardError,
        Task standardErrorDrained,
        CancellationToken cancellationToken)
    {
        _ = await Task.WhenAny(
            standardErrorDrained,
            Task.Delay(StandardErrorDrainTimeout, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return CreateEarlyExitFailure(process, source, standardError);
    }

    private static async Task<Exception> CreateTimeoutOrStoppedFailureAsync(
        Process process,
        string source,
        StringBuilder standardError,
        Task standardErrorDrained,
        TimeSpan connectionTimeout,
        CancellationToken cancellationToken)
    {
        Exception? stopped = await TryGetProcessStoppedFailureAsync(
            process,
            source,
            standardError,
            standardErrorDrained,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return stopped
            ?? new MtpServerConnectionClosedException(
                $"The Microsoft.Testing.Platform application '{source}' did not connect back within {connectionTimeout.TotalSeconds:N0}s. "
                + GetStandardError(standardError));
    }

    private static MtpServerConnectionClosedException CreateEarlyExitFailure(
        Process process,
        string source,
        StringBuilder standardError)
        => new(
            $"The Microsoft.Testing.Platform application '{source}' exited with code {process.ExitCode} before connecting back. "
            + GetStandardError(standardError));

    private static string GetStandardError(StringBuilder buffer)
    {
        lock (buffer)
        {
            string text = buffer.ToString().Trim();
            return text.Length == 0 ? string.Empty : $"Standard error: {text}";
        }
    }

    private static Process CreateProcess(ProcessStartInfo startInfo, StringBuilder standardError, out Task standardErrorDrained)
    {
        var standardErrorDrainedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        standardErrorDrained = standardErrorDrainedSource.Task;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                _ = standardErrorDrainedSource.TrySetResult(true);
                return;
            }

            lock (standardError)
            {
                standardError.AppendLine(e.Data);
                if (standardError.Length > MaxStandardErrorLength)
                {
                    standardError.Remove(0, standardError.Length - MaxStandardErrorLength);
                }
            }
        };

        return process;
    }
}
