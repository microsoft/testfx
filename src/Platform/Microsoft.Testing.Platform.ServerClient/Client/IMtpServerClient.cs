// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// High-level client for driving a Microsoft.Testing.Platform (MTP) application over its server-mode
/// JSON-RPC protocol: initialize handshake, discover, run, run-with-filter, and exit, plus events for
/// the server-initiated notifications (test-node updates, logs, telemetry, attachments).
/// </summary>
internal interface IMtpServerClient : IDisposable
{
    /// <summary>
    /// Raised when the server reports test-node state changes (<c>testing/testUpdates/tests</c>).
    /// </summary>
    event EventHandler<MtpTestNodeUpdateEventArgs>? TestNodesUpdated;

    /// <summary>
    /// Raised when the server sends a log message (<c>client/log</c>).
    /// </summary>
    event EventHandler<MtpLogEventArgs>? LogReceived;

    /// <summary>
    /// Raised when the server sends a telemetry update (<c>telemetry/update</c>).
    /// </summary>
    event EventHandler<MtpTelemetryEventArgs>? TelemetryReceived;

    /// <summary>
    /// Raised when the server reports run attachments (<c>testing/testUpdates/attachments</c>).
    /// </summary>
    event EventHandler<MtpAttachmentsEventArgs>? AttachmentsReceived;

    /// <summary>
    /// Gets the process id of the launched application, or 0 when the client was created over an
    /// externally supplied connection (for example in tests).
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Gets the capabilities negotiated during <see cref="InitializeAsync"/>, or <see langword="null"/>
    /// before initialize has completed.
    /// </summary>
    MtpServerCapabilities? Capabilities { get; }

    /// <summary>
    /// Gets or sets an opt-in handler for server-initiated requests (for example the debugger-attach
    /// request). When <see langword="null"/> the client answers every server request with
    /// <see langword="null"/>.
    /// </summary>
    Func<string, IDictionary<string, object?>?, CancellationToken, Task<object?>>? ServerRequestHandler { get; set; }

    /// <summary>
    /// Sends the <c>initialize</c> request and returns the negotiated server capabilities.
    /// </summary>
    Task<MtpServerCapabilities> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers every test in the application.
    /// </summary>
    Task DiscoverTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers the tests identified by <paramref name="testNodeUids"/>.
    /// </summary>
    Task DiscoverTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers the tests that match the supplied graph filter.
    /// </summary>
    Task DiscoverTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs every test in the application.
    /// </summary>
    Task<MtpRunResult> RunTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the tests identified by <paramref name="testNodeUids"/>.
    /// </summary>
    Task<MtpRunResult> RunTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the tests that match the supplied graph filter.
    /// </summary>
    Task<MtpRunResult> RunTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the <c>exit</c> notification, asking the application to shut down.
    /// </summary>
    Task ExitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The capabilities the server advertised in its <c>initialize</c> response.
/// </summary>
internal sealed class MtpServerCapabilities
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpServerCapabilities"/> class.
    /// </summary>
    public MtpServerCapabilities(
        int? serverProcessId,
        string? serverName,
        string? serverVersion,
        bool supportsDiscovery,
        bool multiRequestSupport,
        bool vstestProviderSupport,
        bool supportsAttachments,
        bool multiConnectionProvider)
    {
        ServerProcessId = serverProcessId;
        ServerName = serverName;
        ServerVersion = serverVersion;
        SupportsDiscovery = supportsDiscovery;
        MultiRequestSupport = multiRequestSupport;
        VSTestProviderSupport = vstestProviderSupport;
        SupportsAttachments = supportsAttachments;
        MultiConnectionProvider = multiConnectionProvider;
    }

    /// <summary>Gets the process id reported by the server.</summary>
    public int? ServerProcessId { get; }

    /// <summary>Gets the server name (product identifier).</summary>
    public string? ServerName { get; }

    /// <summary>Gets the server version.</summary>
    public string? ServerVersion { get; }

    /// <summary>Gets a value indicating whether the server supports discovery.</summary>
    public bool SupportsDiscovery { get; }

    /// <summary>Gets a value indicating whether the server supports multiple requests on one connection (keep-alive).</summary>
    public bool MultiRequestSupport { get; }

    /// <summary>Gets a value indicating whether the server exposes the VSTest provider.</summary>
    public bool VSTestProviderSupport { get; }

    /// <summary>Gets a value indicating whether the server reports attachments.</summary>
    public bool SupportsAttachments { get; }

    /// <summary>Gets a value indicating whether the server supports multiple connections.</summary>
    public bool MultiConnectionProvider { get; }
}

/// <summary>
/// The result of a run request: the artifacts (attachments) the server produced.
/// </summary>
internal sealed class MtpRunResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpRunResult"/> class.
    /// </summary>
    public MtpRunResult(IReadOnlyList<MtpAttachment> artifacts)
        => Artifacts = artifacts;

    /// <summary>Gets the artifacts produced by the run.</summary>
    public IReadOnlyList<MtpAttachment> Artifacts { get; }
}

/// <summary>
/// A run attachment / artifact reported by the server.
/// </summary>
internal sealed class MtpAttachment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpAttachment"/> class.
    /// </summary>
    public MtpAttachment(string? uri, string? producer, string? type, string? displayName, string? description)
    {
        Uri = uri;
        Producer = producer;
        Type = type;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>Gets the attachment URI.</summary>
    public string? Uri { get; }

    /// <summary>Gets the producer that emitted the attachment.</summary>
    public string? Producer { get; }

    /// <summary>Gets the attachment type.</summary>
    public string? Type { get; }

    /// <summary>Gets the display name.</summary>
    public string? DisplayName { get; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; }
}

/// <summary>
/// Event args for a batch of test-node state changes.
/// </summary>
internal sealed class MtpTestNodeUpdateEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpTestNodeUpdateEventArgs"/> class.
    /// </summary>
    public MtpTestNodeUpdateEventArgs(Guid runId, IReadOnlyList<MtpTestNodeUpdate> changes)
    {
        RunId = runId;
        Changes = changes;
    }

    /// <summary>Gets the run id the changes belong to.</summary>
    public Guid RunId { get; }

    /// <summary>Gets the reported test-node changes.</summary>
    public IReadOnlyList<MtpTestNodeUpdate> Changes { get; }
}

/// <summary>
/// A single test-node change. The raw node is exposed as <see cref="Node"/>; the most common fields
/// are surfaced as convenience accessors.
/// </summary>
internal sealed class MtpTestNodeUpdate
{
    private const string NodeTypeKey = "node-type";
    private const string ExecutionStateKey = "execution-state";
    private const string ErrorMessageKey = "error.message";
    private const string ErrorStackTraceKey = "error.stacktrace";
    private const string DurationKey = "time.duration-ms";

    /// <summary>
    /// Initializes a new instance of the <see cref="MtpTestNodeUpdate"/> class.
    /// </summary>
    public MtpTestNodeUpdate(IDictionary<string, object?> node, string? parentUid)
    {
        Node = new Dictionary<string, object?>(node);
        ParentUid = parentUid;
    }

    /// <summary>Gets the raw node property bag as it arrived on the wire.</summary>
    public IReadOnlyDictionary<string, object?> Node { get; }

    /// <summary>Gets the uid of the parent node, when the server supplied one.</summary>
    public string? ParentUid { get; }

    /// <summary>Gets the node uid.</summary>
    public string? Uid => GetString(JsonRpcStrings.Uid);

    /// <summary>Gets the node display name.</summary>
    public string? DisplayName => GetString(JsonRpcStrings.DisplayName);

    /// <summary>Gets the node type (<c>group</c> or <c>action</c>).</summary>
    public string? NodeType => GetString(NodeTypeKey);

    /// <summary>Gets the execution state (<c>discovered</c>, <c>in-progress</c>, <c>passed</c>, <c>failed</c>, ...).</summary>
    public string? ExecutionState => GetString(ExecutionStateKey);

    /// <summary>Gets the error message, when the node carries one.</summary>
    public string? ErrorMessage => GetString(ErrorMessageKey);

    /// <summary>Gets the error stack trace, when the node carries one.</summary>
    public string? ErrorStackTrace => GetString(ErrorStackTraceKey);

    /// <summary>Gets the reported duration in milliseconds, when the node carries one.</summary>
    public double? DurationInMilliseconds => Node.TryGetValue(DurationKey, out object? value)
        ? value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            _ => null,
        }
        : null;

    private string? GetString(string key)
        => Node.TryGetValue(key, out object? value) ? value as string : null;
}

/// <summary>
/// Event args for a server log message.
/// </summary>
internal sealed class MtpLogEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpLogEventArgs"/> class.
    /// </summary>
    public MtpLogEventArgs(string level, string message)
    {
        Level = level;
        Message = message;
    }

    /// <summary>Gets the log level string as reported by the server.</summary>
    public string Level { get; }

    /// <summary>Gets the log message.</summary>
    public string Message { get; }
}

/// <summary>
/// Event args for a telemetry update.
/// </summary>
internal sealed class MtpTelemetryEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpTelemetryEventArgs"/> class.
    /// </summary>
    public MtpTelemetryEventArgs(string eventName, IReadOnlyDictionary<string, object?> metrics)
    {
        EventName = eventName;
        Metrics = metrics;
    }

    /// <summary>Gets the telemetry event name.</summary>
    public string EventName { get; }

    /// <summary>Gets the telemetry metrics.</summary>
    public IReadOnlyDictionary<string, object?> Metrics { get; }
}

/// <summary>
/// Event args for a batch of run attachments.
/// </summary>
internal sealed class MtpAttachmentsEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MtpAttachmentsEventArgs"/> class.
    /// </summary>
    public MtpAttachmentsEventArgs(IReadOnlyList<MtpAttachment> attachments)
        => Attachments = attachments;

    /// <summary>Gets the reported attachments.</summary>
    public IReadOnlyList<MtpAttachment> Attachments { get; }
}
