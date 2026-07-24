// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Launches a Microsoft.Testing.Platform (MTP) application in JSON-RPC server mode and owns the
/// resulting <see cref="MtpJsonRpcConnection"/>.
/// </summary>
/// <remarks>
/// The client is the JSON-RPC <em>client</em>: it opens a loopback TCP listener, launches the MTP
/// application with <c>--server --client-port &lt;port&gt; --no-banner</c>, and the application dials
/// back to the listener. The accepted socket is wrapped in the reused <see cref="TcpMessageHandler"/>
/// (LSP-style <c>Content-Length</c> framing) and the platform's own <see cref="FormatterUtilities"/>
/// formatter (Jsonite on .NET Framework / netstandard, in-box System.Text.Json on .NET), so the wire
/// is byte-for-byte identical to the server's expectations.
/// </remarks>
internal sealed class MtpServerProcess : IDisposable
{
    private const string ServerArgument = "--server";
    private const string ClientPortArgument = "--client-port";
    private const string NoBannerArgument = "--no-banner";

    // How often the connect wait re-checks whether the launched process has already exited, so a child
    // that dies on startup fails fast instead of blocking the full ConnectionTimeout.
    private static readonly TimeSpan ProcessExitPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly TcpListener _listener;
    private readonly Process _process;
    private readonly IMtpClientLogger _logger;
    private readonly StringBuilder _standardError;
    private readonly TcpClient _client;

    private int _disposed;

    private MtpServerProcess(TcpListener listener, Process process, TcpClient client, MtpJsonRpcConnection connection, StringBuilder standardError, IMtpClientLogger logger)
    {
        _listener = listener;
        _process = process;
        _client = client;
        Connection = connection;
        _standardError = standardError;
        _logger = logger;
    }

    /// <summary>
    /// Gets the transport connection to the launched application. The read loop is NOT started yet;
    /// the owner must attach handlers and call <see cref="MtpJsonRpcConnection.Start"/>.
    /// </summary>
    public MtpJsonRpcConnection Connection { get; }

    /// <summary>
    /// Gets the process id of the launched application, or 0 if it has already exited.
    /// </summary>
    public int ProcessId
    {
        get
        {
            try
            {
                return _process.HasExited ? 0 : _process.Id;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> and waits for it to connect back.
    /// </summary>
    /// <param name="source">
    /// Path to the test application. May be a managed <c>.dll</c> (launched via its sibling apphost
    /// <c>.exe</c> when present, otherwise via <c>dotnet &lt;dll&gt;</c>) or a native <c>.exe</c>.
    /// </param>
    /// <param name="options">Client options (name, connection timeout, environment, logger).</param>
    public static MtpServerProcess Start(string source, MtpServerClientOptions? options = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

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
        SerializerUtilities.RegisterClientSerializers();
        IMessageFormatter formatter = FormatterUtilities.CreateFormatter();

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

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

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var standardError = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (standardError)
                {
                    standardError.AppendLine(e.Data);
                }
            }
        };

        TcpClient? acceptedClient = null;
        try
        {
            process.Start();
            process.BeginErrorReadLine();

            // Drain stdout so the child never blocks on a full pipe (banner, diagnostics).
            process.BeginOutputReadLine();

            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

            // Wait for the app to dial back, but poll the process alongside the accept: if the child exits
            // early (bad arguments, startup crash) we fail fast with its exit code + captured stderr instead
            // of blocking the full ConnectionTimeout and then reporting a misleading timeout. Polling
            // process.HasExited (rather than racing the accept against Process.Exited) keeps this free of a
            // TaskCompletionSource ordering race.
            var connectStopwatch = Stopwatch.StartNew();
            while (!acceptTask.Wait(ProcessExitPollInterval))
            {
                if (process.HasExited)
                {
                    throw new MtpServerConnectionClosedException(
                        $"The Microsoft.Testing.Platform application '{source}' exited with code {process.ExitCode} before connecting back. {GetStandardError(standardError)}");
                }

                if (connectStopwatch.Elapsed >= options.ConnectionTimeout)
                {
                    throw new MtpServerConnectionClosedException(
                        $"The Microsoft.Testing.Platform application '{source}' did not connect back within {options.ConnectionTimeout.TotalSeconds:N0}s. {GetStandardError(standardError)}");
                }
            }

            acceptedClient = acceptTask.GetAwaiter().GetResult();
            acceptedClient.NoDelay = true;
            NetworkStream stream = acceptedClient.GetStream();

            var handler = new TcpMessageHandler(acceptedClient, stream, stream, formatter);
            var connection = new MtpJsonRpcConnection(handler, logger);

            // NOTE: the read loop is intentionally NOT started here. The owner (MtpServerClient) wires its
            // notification/server-request handlers first and then calls Connection.Start(), so no server ->
            // client message can slip past before the handlers are attached.
            return new MtpServerProcess(listener, process, acceptedClient, connection, standardError, logger);
        }
        catch
        {
            acceptedClient?.Dispose();
            SafeStop(listener, logger);
            SafeKill(process, logger);
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the captured standard-error output of the launched application.
    /// </summary>
    public string GetStandardError()
        => GetStandardError(_standardError);

    private static string GetStandardError(StringBuilder buffer)
    {
        lock (buffer)
        {
            string text = buffer.ToString().Trim();
            return text.Length == 0 ? string.Empty : $"Standard error: {text}";
        }
    }

    private static LaunchCommand BuildLaunch(string source, int port)
    {
        string serverArgs = $"{ServerArgument} {ClientPortArgument} {port} {NoBannerArgument}";
        string workingDirectory = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        string extension = Path.GetExtension(source);

        // A managed .NET assembly must be launched through its apphost (preferred) or `dotnet <dll>`.
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            string apphost = GetAppHostPath(source);
            return File.Exists(apphost)
                ? new LaunchCommand(apphost, serverArgs, workingDirectory)
                : new LaunchCommand("dotnet", $"\"{source}\" {serverArgs}", workingDirectory);
        }

        // Otherwise `source` is already a native executable: a Windows `.exe` apphost or an
        // extensionless native apphost on Linux/macOS. Run it directly.
        return new LaunchCommand(source, serverArgs, workingDirectory);
    }

    // A named launch descriptor rather than a value tuple: System.ValueTuple is not in the .NET
    // Framework before 4.7, and this source is compiled into consumers that may target net462 without
    // referencing the System.ValueTuple package. A tiny class keeps the package dependency-free.
    private sealed class LaunchCommand
    {
        public LaunchCommand(string fileName, string arguments, string workingDirectory)
        {
            FileName = fileName;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public string FileName { get; }

        public string Arguments { get; }

        public string WorkingDirectory { get; }
    }

    private static string GetAppHostPath(string managedAssembly)
    {
        string directory = Path.GetDirectoryName(managedAssembly) ?? string.Empty;
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(managedAssembly);
#if NETFRAMEWORK
        // System.Runtime.InteropServices.RuntimeInformation is not available on .NET Framework before
        // 4.7.1, and this source is compiled into consumers that may target net462. .NET Framework only
        // runs on Windows (and, rarely, Unix via Mono), so PlatformID.Win32NT is a reliable Windows check.
        bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        string appHostFileName = isWindows
            ? nameWithoutExtension + ".exe"
            : nameWithoutExtension;
        return Path.Combine(directory, appHostFileName);
    }

    private static void SafeStop(TcpListener listener, IMtpClientLogger logger)
    {
        try
        {
            listener.Stop();
        }
        catch (SocketException ex)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Stopping the TCP listener threw: {ex}");
        }
    }

    private static void SafeKill(Process process, IMtpClientLogger logger)
    {
        try
        {
            if (!process.HasExited)
            {
#if NETCOREAPP
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Killing the MTP server process threw: {ex}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // Dispose the connection first (cancels the read loop, disposes the handler -> socket/streams).
        Connection.Dispose();

        try
        {
            _client.Dispose();
        }
        catch (SocketException ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Debug, $"Disposing the accepted client socket threw: {ex}");
        }

        SafeStop(_listener, _logger);
        SafeKill(_process, _logger);
        _process.Dispose();
    }
}
