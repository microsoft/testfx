// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Default <see cref="IMtpServerClient"/> implementation over a <see cref="MtpJsonRpcConnection"/>.
/// </summary>
/// <remarks>
/// Two ways to obtain a client:
/// <list type="bullet">
/// <item><see cref="Launch(string, MtpServerClientOptions?)"/> starts the MTP application and owns its process.</item>
/// <item>The <see cref="MtpServerClient(MtpJsonRpcConnection, MtpServerClientOptions?)"/> constructor wraps an
/// already-connected transport (used by tests over a paired in-memory stream).</item>
/// </list>
/// The constructor attaches the notification and server-request handlers and only then starts the
/// connection read loop, so no server-to-client message can slip past before the handlers are wired.
/// </remarks>
internal sealed class MtpServerClient : IMtpServerClient
{
    private readonly MtpJsonRpcConnection _connection;
    private readonly MtpServerClientOptions _options;
    private readonly MtpServerProcess? _process;

    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MtpServerClient"/> class over an existing connection.
    /// </summary>
    /// <param name="connection">The transport connection. Its read loop is started by this constructor.</param>
    /// <param name="options">Client options (name, capabilities, logger). Defaults are used when omitted.</param>
    public MtpServerClient(MtpJsonRpcConnection connection, MtpServerClientOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? new MtpServerClientOptions();

        _connection.NotificationReceived += OnNotificationReceived;
        _connection.ServerRequestHandler = OnServerRequestAsync;
        _connection.Start();
    }

    private MtpServerClient(MtpServerProcess process, MtpServerClientOptions options)
        : this(process.Connection, options)
        => _process = process;

    /// <inheritdoc />
    public event EventHandler<MtpTestNodeUpdateEventArgs>? TestNodesUpdated;

    /// <inheritdoc />
    public event EventHandler<MtpLogEventArgs>? LogReceived;

    /// <inheritdoc />
    public event EventHandler<MtpTelemetryEventArgs>? TelemetryReceived;

    /// <inheritdoc />
    public event EventHandler<MtpAttachmentsEventArgs>? AttachmentsReceived;

    /// <inheritdoc />
    public Func<string, IDictionary<string, object?>?, CancellationToken, Task<object?>>? ServerRequestHandler { get; set; }

    /// <inheritdoc />
    public int ProcessId => _process?.ProcessId ?? 0;

    /// <inheritdoc />
    public MtpServerCapabilities? Capabilities { get; private set; }

    /// <summary>
    /// Launches the MTP application at <paramref name="source"/> in server mode and returns a connected client.
    /// </summary>
    /// <param name="source">Path to the test application (managed <c>.dll</c> or native <c>.exe</c>).</param>
    /// <param name="options">Client options (name, capabilities, connection timeout, environment, logger).</param>
    public static MtpServerClient Launch(string source, MtpServerClientOptions? options = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        options ??= new MtpServerClientOptions();
        var process = MtpServerProcess.Start(source, options);
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

    /// <inheritdoc />
    public async Task<MtpServerCapabilities> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var args = new InitializeRequestArgs(
            GetCurrentProcessId(),
            new ClientInfo(_options.ClientName, _options.ClientVersion),
            new ClientCapabilities(_options.DebuggerProvider, _options.IsStateful));

        ResponseMessage response = await _connection.SendRequestAsync(JsonRpcMethods.Initialize, args, cancellationToken).ConfigureAwait(false);
        MtpServerCapabilities capabilities = DecodeCapabilities(response.Result as IDictionary<string, object?>);
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
        => _connection.SendNotificationAsync(JsonRpcMethods.Exit, null, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _connection.NotificationReceived -= OnNotificationReceived;
        _connection.ServerRequestHandler = null;

        if (_process is not null)
        {
            _process.Dispose();
        }
        else
        {
            _connection.Dispose();
        }
    }

    private static int GetCurrentProcessId()
    {
        using var current = Process.GetCurrentProcess();
        return current.Id;
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
            multiConnectionProvider);
    }

    private static int? AsInt(object? value)
        => value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            _ => null,
        };

    private static bool AsBool(IDictionary<string, object?> dictionary, string key)
        => dictionary.TryGetValue(key, out object? value) && value is bool boolean && boolean;

    private async Task DiscoverCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        var args = new DiscoverRequestArgs(Guid.NewGuid(), tests, graphFilter);
        await _connection.SendRequestAsync(JsonRpcMethods.TestingDiscoverTests, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MtpRunResult> RunCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        var args = new RunRequestArgs(Guid.NewGuid(), tests, graphFilter);
        ResponseMessage response = await _connection.SendRequestAsync(JsonRpcMethods.TestingRunTests, args, cancellationToken).ConfigureAwait(false);

        IDictionary<string, object?> resultDict = response.Result as IDictionary<string, object?> ?? new Dictionary<string, object?>();
        RunResponseArgs runResponse = SerializerUtilities.Deserialize<RunResponseArgs>(resultDict);
        MtpAttachment[] artifacts = runResponse.Artifacts
            .Select(artifact => new MtpAttachment(artifact.Uri, artifact.Producer, artifact.Type, artifact.DisplayName, artifact.Description))
            .ToArray();

        return new MtpRunResult(artifacts);
    }

    private async Task<object?> OnServerRequestAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        Func<string, IDictionary<string, object?>?, CancellationToken, Task<object?>>? handler = ServerRequestHandler;
        return handler is null
            ? null
            : await handler(request.Method, request.Params as IDictionary<string, object?>, cancellationToken).ConfigureAwait(false);
    }

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
