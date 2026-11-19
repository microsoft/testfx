// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Default <see cref="IMtpServerClient"/> implementation over a <see cref="MtpJsonRpcConnection"/>.
/// </summary>
/// <remarks>
/// Three ways to obtain a client:
/// <list type="bullet">
/// <item><see cref="Launch(string, MtpServerClientOptions?)"/> starts the MTP application as a child process
/// and owns that process.</item>
/// <item><see cref="LaunchInProcessAsync"/> hosts the MTP application in the caller's own process through a
/// callback and owns the resulting server task (for embedded hosts that cannot spawn a process).</item>
/// <item>The <see cref="MtpServerClient(MtpJsonRpcConnection, MtpServerClientOptions?)"/> constructor wraps an
/// already-connected transport (used by tests over a paired in-memory stream).</item>
/// </list>
/// The constructor attaches the notification and server-request handlers. The connection read loop starts
/// lazily on the first client operation, giving callers time to subscribe to events first.
/// </remarks>
internal sealed partial class MtpServerClient : IMtpServerClient
{
    private readonly MtpJsonRpcConnection _connection;
    private readonly MtpServerClientOptions _options;
    private readonly IMtpServerHost? _host;
    private readonly SingleFlightTask _shutdown = new();

    private Func<string, IDictionary<string, object?>?, CancellationToken, Task<IDictionary<string, object?>?>>? _serverRequestHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MtpServerClient"/> class over an existing connection.
    /// </summary>
    /// <param name="connection">The transport connection. Its read loop starts on the first client operation.</param>
    /// <param name="options">Client options (name, capabilities, logger). Defaults are used when omitted.</param>
    /// <remarks>
    /// Precondition: the connection's formatter must have been created with the client serializers already
    /// registered — call <see cref="SerializerUtilities.RegisterClientSerializers"/> before building the
    /// formatter passed to <paramref name="connection"/>. The <see cref="Launch"/> and
    /// <see cref="LaunchInProcessAsync"/> factories do this for you; callers that construct a connection
    /// directly are responsible for the ordering.
    /// </remarks>
    public MtpServerClient(MtpJsonRpcConnection connection, MtpServerClientOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? new MtpServerClientOptions();

        _connection.NotificationReceived += OnNotificationReceived;
        _connection.ServerRequestHandler = OnServerRequestAsync;
    }

    private MtpServerClient(IMtpServerHost host, MtpServerClientOptions options)
        : this(host.Connection, options)
        => _host = host;

    /// <inheritdoc />
    public event EventHandler<MtpTestNodeUpdateEventArgs>? TestNodesUpdated;

    /// <inheritdoc />
    public event EventHandler<MtpLogEventArgs>? LogReceived;

    /// <inheritdoc />
    public event EventHandler<MtpTelemetryEventArgs>? TelemetryReceived;

    /// <inheritdoc />
    public event EventHandler<MtpAttachmentsEventArgs>? AttachmentsReceived;

    /// <inheritdoc />
    public Func<string, IDictionary<string, object?>?, CancellationToken, Task<IDictionary<string, object?>?>>? ServerRequestHandler
    {
        get => Volatile.Read(ref _serverRequestHandler);
        set => Volatile.Write(ref _serverRequestHandler, value);
    }

    /// <inheritdoc />
    public int ProcessId => _host?.ProcessId ?? 0;

    /// <inheritdoc />
    public int? ServerExitCode => _host?.ExitCode;

    /// <inheritdoc />
    public MtpServerCapabilities? Capabilities { get; private set; }

    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> in server mode and returns a connected client.
    /// </summary>
    /// <param name="source">Path to the test application (managed <c>.dll</c> or native <c>.exe</c>).</param>
    /// <param name="options">Client options (name, capabilities, connection timeout, environment, logger).</param>
    public static MtpServerClient Launch(string source, MtpServerClientOptions? options = null)
        => LaunchAsync(source, options, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> in server mode and asynchronously waits for
    /// it to connect.
    /// </summary>
    /// <param name="source">Path to the test application (managed <c>.dll</c> or native <c>.exe</c>).</param>
    /// <param name="options">Client options (name, capabilities, connection timeout, environment, logger).</param>
    /// <param name="cancellationToken">Cancels the launch and connection wait.</param>
    public static async Task<MtpServerClient> LaunchAsync(
        string source,
        MtpServerClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        options ??= new MtpServerClientOptions();
        MtpServerProcess process = await MtpServerProcess.StartAsync(source, options, cancellationToken).ConfigureAwait(false);
        try
        {
            return new MtpServerClient(process, options);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Hosts an MTP application in the caller's own process through <paramref name="serverEntryPoint"/> and
    /// asynchronously waits for it to connect back.
    /// </summary>
    /// <param name="serverEntryPoint">
    /// Builds and runs the MTP application. It receives the complete server-mode argument array, which it must
    /// forward verbatim to the test application, plus a cancellation token, and returns the application's exit
    /// code. The callback is invoked on the thread pool, so it never blocks the caller and never inherits the
    /// caller's synchronization context.
    /// </param>
    /// <param name="options">Client options (name, capabilities, connection timeout, shutdown timeout, logger).</param>
    /// <param name="cancellationToken">
    /// Cancels the launch and the connection wait. It scopes the launch only: once the returned client exists,
    /// canceling this token no longer affects the hosted application.
    /// </param>
    /// <exception cref="PlatformNotSupportedException">
    /// The current platform has no loopback TCP listener (browser/WASM). This API is TCP-based and does not
    /// enable WASM hosting.
    /// </exception>
    /// <exception cref="MtpServerConnectionClosedException">
    /// The application failed, was canceled, or exited before connecting back, or did not connect back within
    /// <see cref="MtpServerClientOptions.ConnectionTimeout"/>. The callback's own exception, when there is one,
    /// is the inner exception.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is the embedded-host counterpart of <see cref="LaunchAsync"/>: the client still owns the loopback
    /// listener, the server-mode arguments, the connect race, the serializer/formatter/transport setup and the
    /// shutdown sequence — the caller only supplies "how to run the application":
    /// </para>
    /// <code>
    /// using IMtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
    ///     async (serverArgs, token) =>
    ///     {
    ///         ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(serverArgs);
    ///         builder.AddMSTest(() =&gt; testAssemblies);
    ///         using ITestApplication app = await builder.BuildAsync();
    ///         return await app.RunAsync();
    ///     },
    ///     options,
    ///     cancellationToken);
    /// </code>
    /// <para>
    /// Ownership: the returned client owns the hosted application. Disposing it closes the transport (which is
    /// how a server-mode application is asked to stop) and then waits for the callback within a documented
    /// bound — see <see cref="MtpServerClientOptions.ServerShutdownTimeout"/>. Call
    /// <see cref="ExitAsync"/> before disposing for a protocol-level shutdown.
    /// </para>
    /// <para>
    /// There is deliberately no synchronous overload: the callback runs in the caller's process, so blocking
    /// the launching thread risks deadlocking the very application being launched.
    /// </para>
    /// </remarks>
    public static async Task<MtpServerClient> LaunchInProcessAsync(
        Func<string[], CancellationToken, Task<int>> serverEntryPoint,
        MtpServerClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (serverEntryPoint is null)
        {
            throw new ArgumentNullException(nameof(serverEntryPoint));
        }

        options ??= new MtpServerClientOptions();
        MtpServerInProcessHost host = await MtpServerInProcessHost.StartAsync(serverEntryPoint, options, cancellationToken).ConfigureAwait(false);
        try
        {
            return new MtpServerClient(host, options);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MtpServerCapabilities> InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var args = new InitializeRequestArgs(
            MtpServerConnector.GetCurrentProcessId(),
            new ClientInfo(_options.ClientName, _options.ClientVersion),
            new ClientCapabilities(_options.DebuggerProvider, _options.IsStateful) { ShowMessage = _options.ShowMessage })
        {
            ProtocolVersions = _options.SupportedProtocolVersions.ToArray(),
        };

        ResponseMessage response = await _connection.SendRequestAsync(JsonRpcMethods.Initialize, args, cancellationToken).ConfigureAwait(false);
        MtpServerCapabilities capabilities = DecodeCapabilities(AsResultDictionary(response.Result));
        string effectiveProtocolVersion = capabilities.ProtocolVersion ?? JsonRpcProtocolVersions.V1;
        if (!IsSupportedProtocolVersion(effectiveProtocolVersion))
        {
            throw new MtpServerClientException(
                $"The server negotiated unsupported protocol version '{effectiveProtocolVersion}'. "
                + $"Supported versions: {string.Join(", ", _options.SupportedProtocolVersions)}.");
        }

        Capabilities = capabilities;
        return capabilities;
    }

    /// <inheritdoc />
    public Task ExitAsync(CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        return _connection.SendNotificationAsync(JsonRpcMethods.Exit, null, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DetachHandlers();

        if (_host is not null)
        {
            // The host owns and joins its one teardown. Its synchronous disposal also preserves the IDisposable
            // contract by reporting a callback failure rather than throwing it.
            _host.Dispose();
            return;
        }

        // An existing-connection client caches and joins the scheduled fallback teardown.
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks - this IS the synchronous disposal path; ShutdownAsync is the awaitable one.
        StartConnectionShutdownAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc />
    public Task ShutdownAsync()
    {
        DetachHandlers();
        return _host?.ShutdownAsync() ?? StartConnectionShutdownAsync();
    }

    private Task StartConnectionShutdownAsync()
        => _shutdown.StartAsync(_connection.Dispose);

    private void DetachHandlers()
    {
        _connection.NotificationReceived -= OnNotificationReceived;
        _connection.ServerRequestHandler = null;
    }

    // A null result is tolerated (the server answered with no payload -> decode defaults/empty). A non-null
    // result that is not the expected IDictionary<string, object?> is a protocol violation, so surface it
    // with the actual runtime type instead of silently discarding it via `as` (which would look like an
    // empty/absent result and hide the mismatch).
    private static IDictionary<string, object?>? AsResultDictionary(object? result)
        => result switch
        {
            null => null,
            IDictionary<string, object?> dictionary => dictionary,
            _ => throw new MtpServerClientException(
                $"Expected the server response result to be an IDictionary<string, object?> but it was '{result.GetType()}'."),
        };

    private void EnsureStarted()
        => _connection.Start();
}
