// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

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
internal sealed class MtpServerClient : IMtpServerClient
{
    private readonly MtpJsonRpcConnection _connection;
    private readonly MtpServerClientOptions _options;
    private readonly IMtpServerHost? _host;
    private readonly object _shutdownLock = new();

    private Func<string, IDictionary<string, object?>?, CancellationToken, Task<IDictionary<string, object?>?>>? _serverRequestHandler;
    private Task? _shutdown;

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
            new ClientCapabilities(_options.DebuggerProvider, _options.IsStateful))
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
    public Task DiscoverTestsAsync(CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(null, null, cancellationToken);

    /// <inheritdoc />
    public Task DiscoverTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(BuildTestNodes(testNodeUids ?? throw new ArgumentNullException(nameof(testNodeUids))), null, cancellationToken);

    /// <inheritdoc />
    public Task DiscoverTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(null, graphFilter ?? throw new ArgumentNullException(nameof(graphFilter)), cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsAsync(CancellationToken cancellationToken = default)
        => RunCoreAsync(null, null, cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default)
        => RunCoreAsync(BuildTestNodes(testNodeUids ?? throw new ArgumentNullException(nameof(testNodeUids))), null, cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default)
        => RunCoreAsync(null, graphFilter ?? throw new ArgumentNullException(nameof(graphFilter)), cancellationToken);

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
    {
        lock (_shutdownLock)
        {
            return _shutdown ??= Task.Run(_connection.Dispose);
        }
    }

    private void DetachHandlers()
    {
        _connection.NotificationReceived -= OnNotificationReceived;
        _connection.ServerRequestHandler = null;
    }

    private static ICollection<TestNode> BuildTestNodes(IReadOnlyCollection<string> testNodeUids)
        => testNodeUids.Select(uid => new TestNode { Uid = uid, DisplayName = uid }).ToList();

    private static MtpServerCapabilities DecodeCapabilities(IDictionary<string, object?>? result)
    {
        result ??= new Dictionary<string, object?>();

        int? processId = result.TryGetValue(JsonRpcStrings.ProcessId, out object? processIdObj) ? AsInt(processIdObj) : null;

        string? serverName = null;
        string? serverVersion = null;
        if (result.TryGetValue(JsonRpcStrings.ServerInfo, out object? serverInfoObj)
            && serverInfoObj is IDictionary<string, object?> serverInfo)
        {
            serverName = serverInfo.TryGetValue(JsonRpcStrings.Name, out object? nameObj) ? nameObj as string : null;
            serverVersion = serverInfo.TryGetValue(JsonRpcStrings.Version, out object? versionObj) ? versionObj as string : null;
        }

        bool supportsDiscovery = false;
        bool multiRequestSupport = false;
        bool vstestProviderSupport = false;
        bool supportsAttachments = false;
        bool multiConnectionProvider = false;
        string? protocolVersion = null;
        if (result.TryGetValue(JsonRpcStrings.ProtocolVersion, out object? protocolVersionObj))
        {
            protocolVersion = protocolVersionObj switch
            {
                null => null,
                string value => value,
                _ => throw new MtpServerClientException(
                    $"Expected '{JsonRpcStrings.ProtocolVersion}' to be a string but it was '{protocolVersionObj.GetType()}'."),
            };
        }

        if (result.TryGetValue(JsonRpcStrings.Capabilities, out object? capabilitiesObj)
            && capabilitiesObj is IDictionary<string, object?> capabilities
            && capabilities.TryGetValue(JsonRpcStrings.Testing, out object? testingObj)
            && testingObj is IDictionary<string, object?> testing)
        {
            supportsDiscovery = AsBool(testing, JsonRpcStrings.SupportsDiscovery);
            multiRequestSupport = AsBool(testing, JsonRpcStrings.MultiRequestSupport);
            vstestProviderSupport = AsBool(testing, JsonRpcStrings.VSTestProviderSupport);
            supportsAttachments = AsBool(testing, JsonRpcStrings.AttachmentsSupport);
            multiConnectionProvider = AsBool(testing, JsonRpcStrings.MultiConnectionProvider);
        }

        return new MtpServerCapabilities(
            processId,
            serverName,
            serverVersion,
            supportsDiscovery,
            multiRequestSupport,
            vstestProviderSupport,
            supportsAttachments,
            multiConnectionProvider,
            protocolVersion);
    }

    private bool IsSupportedProtocolVersion(string negotiatedProtocolVersion)
        => _options.SupportedProtocolVersions.Count == 0
            ? negotiatedProtocolVersion == JsonRpcProtocolVersions.V1
            : _options.SupportedProtocolVersions.Contains(negotiatedProtocolVersion, StringComparer.Ordinal);

    private static int? AsInt(object? value)
        => value switch
        {
            int i => i,
            short s => s,
            byte b => b,

            // The JSON formatter may widen an integer to long/ulong/double depending on its magnitude, so
            // accept those too but only when the value round-trips into an Int32 without loss. Anything out
            // of range or non-integral is treated as absent (null) rather than silently truncated.
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            uint u when u <= int.MaxValue => (int)u,
            ulong ul when ul <= int.MaxValue => (int)ul,

            // `d % 1d is 0d` is the integrality test (behaviorally equal to `d == Math.Floor(d)`) written as
            // a constant pattern so it does not trip the analyzer's "equality on floating-point" rule; the
            // remainder of an in-range integral double against 1 is exactly zero.
            double d when d is >= int.MinValue and <= int.MaxValue && d % 1d is 0d => (int)d,
            _ => null,
        };

    private static bool AsBool(IDictionary<string, object?> dictionary, string key)
        => dictionary.TryGetValue(key, out object? value) && value is bool boolean && boolean;

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

    private async Task DiscoverCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        EnsureStarted();
        var args = new DiscoverRequestArgs(Guid.NewGuid(), tests, graphFilter);
        await _connection.SendRequestAsync(JsonRpcMethods.TestingDiscoverTests, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MtpRunResult> RunCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        EnsureStarted();
        var args = new RunRequestArgs(Guid.NewGuid(), tests, graphFilter);
        ResponseMessage response = await _connection.SendRequestAsync(JsonRpcMethods.TestingRunTests, args, cancellationToken).ConfigureAwait(false);

        IDictionary<string, object?> resultDict = AsResultDictionary(response.Result) ?? new Dictionary<string, object?>();
        RunResponseArgs runResponse = SerializerUtilities.Deserialize<RunResponseArgs>(resultDict);
        MtpAttachment[] artifacts = runResponse.Artifacts
            .Select(artifact => new MtpAttachment(artifact.Uri, artifact.Producer, artifact.Type, artifact.DisplayName, artifact.Description))
            .ToArray();

        return new MtpRunResult(artifacts);
    }

    private async Task<object?> OnServerRequestAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        Func<string, IDictionary<string, object?>?, CancellationToken, Task<IDictionary<string, object?>?>>? handler =
            Volatile.Read(ref _serverRequestHandler);
        if (handler is null)
        {
            return null;
        }

        IDictionary<string, object?>? result = await handler(request.Method, request.Params as IDictionary<string, object?>, cancellationToken).ConfigureAwait(false);

        // The response serializer is registered for the concrete Dictionary<string, object?> the formatter
        // writes, so normalize any other IDictionary implementation to that exact type. This keeps the
        // connection's "always answers" guarantee: a non-Dictionary result would otherwise throw on
        // serialize and the server would wait forever for a response that never comes.
        if (result is null or Dictionary<string, object?>)
        {
            return result;
        }

        var normalized = new Dictionary<string, object?>(result);
        return normalized;
    }

    private void EnsureStarted()
        => _connection.Start();

    private void OnNotificationReceived(NotificationMessage notification)
    {
        switch (notification.Method)
        {
            case JsonRpcMethods.TestingTestUpdatesTests:
                RaiseTestNodesUpdated(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.ClientLog:
                RaiseLog(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.TelemetryUpdate:
                RaiseTelemetry(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.TestingTestUpdatesAttachments:
                RaiseAttachments(notification.Params as IDictionary<string, object?>);
                break;

            default:
                break;
        }
    }

    private void RaiseTestNodesUpdated(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpTestNodeUpdateEventArgs>? handler = TestNodesUpdated;
        if (@params is null || handler is null)
        {
            return;
        }

        // A 'changes: null' payload is the completion sentinel the server sends right before the run/discover
        // response; there is nothing to report, so skip it (do not raise an empty update for it).
        if (!@params.TryGetValue(JsonRpcStrings.Changes, out object? changesObj) || changesObj is null)
        {
            return;
        }

        Guid runId = Guid.Empty;
        if (@params.TryGetValue(JsonRpcStrings.RunId, out object? runIdObj) && runIdObj is string runIdString
            && Guid.TryParse(runIdString, out Guid parsedRunId))
        {
            runId = parsedRunId;
        }

        var changes = new List<MtpTestNodeUpdate>();
        if (changesObj is ICollection<object> changeItems)
        {
            foreach (IDictionary<string, object?> change in changeItems.OfType<IDictionary<string, object?>>())
            {
                if (!change.TryGetValue(JsonRpcStrings.Node, out object? nodeObj)
                    || nodeObj is not IDictionary<string, object?> node)
                {
                    continue;
                }

                string? parentUid = change.TryGetValue(JsonRpcStrings.Parent, out object? parentObj) ? parentObj as string : null;
                changes.Add(new MtpTestNodeUpdate(node, parentUid));
            }
        }

        handler(this, new MtpTestNodeUpdateEventArgs(runId, changes));
    }

    private void RaiseLog(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpLogEventArgs>? handler = LogReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        string level = (@params.TryGetValue(JsonRpcStrings.Level, out object? levelObj) ? levelObj as string : null) ?? string.Empty;
        string message = (@params.TryGetValue(JsonRpcStrings.Message, out object? messageObj) ? messageObj as string : null) ?? string.Empty;
        handler(this, new MtpLogEventArgs(level, message));
    }

    private void RaiseTelemetry(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpTelemetryEventArgs>? handler = TelemetryReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        string eventName = (@params.TryGetValue(JsonRpcStrings.EventName, out object? eventNameObj) ? eventNameObj as string : null) ?? string.Empty;

        var metrics = new Dictionary<string, object?>();
        if (@params.TryGetValue(JsonRpcStrings.Metrics, out object? metricsObj) && metricsObj is IDictionary<string, object?> metricsDict)
        {
            foreach (KeyValuePair<string, object?> pair in metricsDict)
            {
                metrics[pair.Key] = pair.Value;
            }
        }

        handler(this, new MtpTelemetryEventArgs(eventName, metrics));
    }

    private void RaiseAttachments(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpAttachmentsEventArgs>? handler = AttachmentsReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        var attachments = new List<MtpAttachment>();
        if (@params.TryGetValue(JsonRpcStrings.Attachments, out object? attachmentsObj)
            && attachmentsObj is ICollection<object> attachmentItems)
        {
            foreach (IDictionary<string, object?> attachment in attachmentItems.OfType<IDictionary<string, object?>>())
            {
                attachments.Add(new MtpAttachment(
                    attachment.TryGetValue(JsonRpcStrings.Uri, out object? uriObj) ? uriObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Producer, out object? producerObj) ? producerObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Type, out object? typeObj) ? typeObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.DisplayName, out object? displayNameObj) ? displayNameObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Description, out object? descriptionObj) ? descriptionObj as string : null));
            }
        }

        handler(this, new MtpAttachmentsEventArgs(attachments));
    }
}
