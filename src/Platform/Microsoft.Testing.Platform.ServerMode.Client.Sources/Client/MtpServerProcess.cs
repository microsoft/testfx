// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
internal sealed partial class MtpServerProcess : IMtpServerHost
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

    // Give asynchronous stderr callbacks a short chance to publish the direct child's final diagnostics.
    // The wait is polled rather than blocking because a descendant can inherit the pipe and keep it open.
    private static readonly TimeSpan StandardErrorDrainTimeout = TimeSpan.FromSeconds(2);

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

}
