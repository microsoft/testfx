// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
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
internal sealed class MtpServerProcess : IMtpServerHost
{
    private const string ServerArgument = MtpServerConnector.ServerArgument;
    private const string ClientPortArgument = MtpServerConnector.ClientPortArgument;
    private const string NoBannerArgument = MtpServerConnector.NoBannerArgument;

    // Bounded wait after killing the process so the OS releases the executable's file locks before a
    // caller (for example an acceptance test) deletes the application directory.
    private const int ProcessKillTimeoutMs = 5000;

    private const int UnixPermissionDeniedErrorCode = 13;

    // Upper bound on the retained standard-error text so a chatty or long-lived server process cannot grow
    // this buffer without limit. The tail is what matters for diagnosing a failure near exit, so when the
    // cap is exceeded the oldest text is dropped from the front and the most recent output is kept.
    private const int MaxStandardErrorLength = 64 * 1024;

    private static readonly object NoExitCode = new();

    private readonly TcpListener _listener;
    private readonly Process _process;
    private readonly IMtpClientLogger _logger;
    private readonly StringBuilder _standardError;
    private readonly TcpClient _client;
    private readonly object _shutdownLock = new();

    private Task? _shutdown;

    /// <summary>
    /// The exit code captured during teardown, boxed so the read is atomic. <see langword="null"/> means
    /// teardown has not captured a result yet; <see cref="NoExitCode"/> means teardown completed without an
    /// application-returned code.
    /// </summary>
    private object? _capturedExitCode;

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
    /// Gets the exit code of the launched application, or <see langword="null"/> while it is still running or
    /// when forced termination was required.
    /// </summary>
    /// <remarks>
    /// Once teardown has run this returns the value captured then: a <see cref="Process"/> cannot be read
    /// after it is disposed, so a live read would always report <see langword="null"/> afterwards.
    /// </remarks>
    public int? ExitCode
    {
        get
        {
            object? captured = Volatile.Read(ref _capturedExitCode);
            return captured switch
            {
                null => TryReadExitCode(),
                int exitCode => exitCode,
                _ => null,
            };
        }
    }

    private int? TryReadExitCode()
    {
        try
        {
            return _process.HasExited ? _process.ExitCode : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tears the process down without blocking the caller: killing a child process is bounded but still
    /// synchronous (it waits for the OS to release the executable's file locks), so it is moved off the
    /// calling thread. Shares the one teardown with <see cref="Dispose"/>.
    /// </summary>
    public Task ShutdownAsync()
        => StartShutdownAsync();

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

            process = CreateProcess(startInfo, standardError);
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

                process = CreateProcess(startInfo, standardError);
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
                () => startedProcess.HasExited
                    ? new MtpServerConnectionClosedException(
                        $"The Microsoft.Testing.Platform application '{source}' exited with code {startedProcess.ExitCode} before connecting back. {GetStandardError(standardError)}")
                    : null,
                () => new MtpServerConnectionClosedException(
                    $"The Microsoft.Testing.Platform application '{source}' did not connect back within {options.ConnectionTimeout.TotalSeconds:N0}s. {GetStandardError(standardError)}"),
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

    private static string GetStandardError(StringBuilder buffer)
    {
        lock (buffer)
        {
            string text = buffer.ToString().Trim();
            return text.Length == 0 ? string.Empty : $"Standard error: {text}";
        }
    }

    private static Process CreateProcess(ProcessStartInfo startInfo, StringBuilder standardError)
    {
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (standardError)
                {
                    standardError.AppendLine(e.Data);
                    if (standardError.Length > MaxStandardErrorLength)
                    {
                        standardError.Remove(0, standardError.Length - MaxStandardErrorLength);
                    }
                }
            }
        };

        return process;
    }

    internal static LaunchCommand BuildLaunch(string source, int port)
    {
        // Deliberately NOT composed from MtpServerConnector.BuildInProcessServerArguments: this string is the
        // command line an already-shipped API hands to already-shipped test applications, so it is kept
        // byte-identical rather than gaining the in-process path's explicit protocol and host. The server
        // maps its 'localhost' default to IPAddress.Loopback, which is what the listener binds, so the two
        // forms are equivalent on the wire; only the in-process array states them explicitly because an
        // embedded host reads it as the documentation of what the client asked for.
        string serverArgs = $"{ServerArgument} {ClientPortArgument} {port} {NoBannerArgument}";
        string workingDirectory = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        string extension = Path.GetExtension(source);

        // A managed .NET assembly must be launched through its apphost (preferred) or `dotnet <dll>`.
        // The apphost is only preferred when it passes the checks available on the consumer's target
        // framework. Process startup remains authoritative and retries through `dotnet <dll>` when Unix
        // rejects the candidate with EACCES.
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            string apphost = GetAppHostPath(source);
            return IsApphostCandidate(apphost)
                ? new LaunchCommand(apphost, serverArgs, workingDirectory)
                : CreateDotnetLaunch(source, serverArgs, workingDirectory);
        }

        // Otherwise `source` is already a native executable: a Windows `.exe` apphost or an
        // extensionless native apphost on Linux/macOS. Run it directly.
        return new LaunchCommand(source, serverArgs, workingDirectory);
    }

    private static LaunchCommand CreateDotnetLaunch(string source, string serverArgs, string workingDirectory)
        => new("dotnet", $"\"{source}\" {serverArgs}", workingDirectory);

    // A named launch descriptor rather than a value tuple: System.ValueTuple is not in the .NET
    // Framework before 4.7, and this source is compiled into consumers that may target net462 without
    // referencing the System.ValueTuple package. A tiny class keeps the package dependency-free.
    internal sealed class LaunchCommand
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
        // The probe is OS-aware rather than always appending ".exe": a test payload built on a Windows
        // agent and executed on a Linux machine (the dotnet/aspnetcore Helix layout) ships a Windows PE
        // `Foo.exe` next to `Foo.dll`. Probing for ".exe" on Linux finds that Windows binary and launching
        // it aborts the run, which is the CI failure fixed in microsoft/vstest#16336. A Unix apphost has
        // no extension, so asking for the right name per OS never selects the foreign one.
        string appHostFileName = IsWindows()
            ? nameWithoutExtension + ".exe"
            : nameWithoutExtension;
        return Path.Combine(directory, appHostFileName);
    }

    /// <summary>
    /// Determines whether <paramref name="apphost"/> is worth attempting on the current operating system.
    /// </summary>
    /// <remarks>
    /// Existence is not sufficient on Unix. Archive formats used to move test payloads between agents (zip in
    /// particular) do not carry the POSIX permission bits, so an extensionless apphost that survives a
    /// Windows-build/Linux-run round trip can arrive without its execute bit. Launching such a file throws
    /// <c>Permission denied</c> instead of degrading, which is the second half of the fix in
    /// microsoft/vstest#16336. On .NET 7+, requiring at least one execute bit cheaply rejects the common case.
    /// This is only a preflight check: the bit can belong to a POSIX identity class that does not apply to the
    /// current process. The process-start path therefore handles EACCES and retries through
    /// <c>dotnet &lt;dll&gt;</c>.
    /// </remarks>
    internal static bool IsApphostCandidate(string apphost)
    {
        if (!File.Exists(apphost))
        {
            return false;
        }

#if NET7_0_OR_GREATER
        // Fenced on the target framework because File.GetUnixFileMode is .NET 7+. Deliberately not the
        // package's modern-.NET compilation symbol: that one records which JSON slice a consumer compiles
        // and is defined only for net8.0+, while NuGet serves the net5.0 slice to net5.0, net6.0 and net7.0
        // consumers alike. Fencing on it would drop a Linux net7.0 consumer back to the existence-only
        // preflight even though it has the API. net462, netstandard2.0, net5.0 and net6.0 consumers genuinely
        // lack the API and keep the existence check above; all target frameworks still recover from EACCES
        // during Process.Start.
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(apphost) & ExecuteBits) != 0;
        }
#endif

        return true;
    }

    private static bool ShouldRetryApphostThroughDotnet(string source, LaunchCommand launch, Win32Exception exception)
        => Path.GetExtension(source).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !launch.FileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !IsWindows()
            && exception.NativeErrorCode == UnixPermissionDeniedErrorCode;

#if NETFRAMEWORK
    // System.Runtime.InteropServices.RuntimeInformation is not available on .NET Framework before
    // 4.7.1, and this source is compiled into consumers that may target net462. .NET Framework only
    // runs on Windows (and, rarely, Unix via Mono), so PlatformID.Win32NT is a reliable Windows check.
    private static bool IsWindows()
        => Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
    private static bool IsWindows()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

    private static bool SafeKill(Process process, IMtpClientLogger logger)
    {
        bool killed = false;
        try
        {
            if (!process.HasExited)
            {
#if NETCOREAPP
                process.Kill(entireProcessTree: true);
#else
                // .NET Framework's Process.Kill cannot kill the whole process tree; any child processes the
                // server spawned are left to the OS. This is best-effort teardown on that platform.
                process.Kill();
#endif
                killed = true;

                // Block (bounded) for the OS to finish tearing the process down so a caller can immediately
                // delete the application directory without racing a file lock on the still-exiting executable.
                process.WaitForExit(ProcessKillTimeoutMs);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Killing the MTP server process threw: {ex}");
        }

        return killed;
    }

    /// <summary>
    /// Kills the launched process and releases the transport, waiting synchronously for the bounded kill so a
    /// caller can immediately delete the application directory.
    /// </summary>
    /// <remarks>
    /// Joins the one teardown rather than starting a second, so a <see cref="Dispose"/> that races or follows
    /// <see cref="ShutdownAsync"/> still returns only once the process has actually gone.
    /// </remarks>
    public void Dispose()
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks - this IS the synchronous disposal path; ShutdownAsync is the awaitable one.
        => StartShutdownAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    private Task StartShutdownAsync()
    {
        lock (_shutdownLock)
        {
            return _shutdown ??= Task.Run(ShutdownCore);
        }
    }

    private void ShutdownCore()
    {
        try
        {
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

            MtpServerConnector.SafeStop(_listener, _logger);

            // Capture before killing, so an application that already exited on its own reports its real exit
            // code rather than the kill's, and before Dispose(), after which the Process cannot be read.
            int? exitCode = TryReadExitCode();
            Volatile.Write(ref _capturedExitCode, exitCode is int captured ? captured : NoExitCode);
            bool killed = SafeKill(_process, _logger);

            // Preserve the race where the process exits naturally between the first read and SafeKill's
            // HasExited check, but never publish the operating system's forced-termination status as though
            // it were an application-returned exit code.
            if (exitCode is null && !killed && TryReadExitCode() is int racedExitCode)
            {
                Volatile.Write(ref _capturedExitCode, racedExitCode);
            }

            _process.Dispose();
        }
        catch (Exception ex)
        {
            // The shared teardown task must never fault: every current and future Dispose/ShutdownAsync
            // caller awaits this one task, so a fault here would throw from every subsequent disposal.
            _logger.SafeLog(MtpClientLogLevel.Error, $"Tearing down the MTP server process threw: {ex}");
        }
    }
}
