// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Client;

// The client under test re-compiles the public platform data model (TestNode, LogLevel, ...). Reference
// ServerClient's copy of those types via the 'serverclient' alias so the payloads the fake server builds are
// the exact types the ServerClient serializers are keyed on. Never plainly import
// Microsoft.Testing.Platform.Logging here: that would re-introduce an ambiguous LogLevel.
using DiscoveredTestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.DiscoveredTestNodeStateProperty;
using FailedTestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.FailedTestNodeStateProperty;
using LinePosition = serverclient::Microsoft.Testing.Platform.Extensions.Messages.LinePosition;
using LinePositionSpan = serverclient::Microsoft.Testing.Platform.Extensions.Messages.LinePositionSpan;
using LogLevel = serverclient::Microsoft.Testing.Platform.Logging.LogLevel;
using PassedTestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.PassedTestNodeStateProperty;
using PropertyBag = serverclient::Microsoft.Testing.Platform.Extensions.Messages.PropertyBag;
using ServerLogMessage = serverclient::Microsoft.Testing.Platform.Logging.ServerLogMessage;
using SessionUid = serverclient::Microsoft.Testing.Platform.TestHost.SessionUid;
using StandardErrorProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.StandardErrorProperty;
using StandardOutputProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.StandardOutputProperty;
using TestFileLocationProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestFileLocationProperty;
using TestNode = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUpdateMessage = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNodeUpdateMessage;

namespace Microsoft.Testing.Platform.ServerClient.UnitTests;

/// <summary>
/// A minimal in-process MTP server that speaks the real server-mode wire protocol over a loopback TCP
/// connection. It reuses the platform's own <see cref="TcpMessageHandler"/> and serializers, so the bytes on
/// the wire are byte-for-byte what a real MTP test host would produce. Tests use it to drive the client under
/// test (<see cref="MtpServerClient"/>) through initialize/discover/run plus every server-to-client
/// notification and server-initiated request.
/// </summary>
internal sealed class FakeMtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly IMessageFormatter _formatter;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly List<NotificationMessage> _receivedNotifications = [];
    private readonly object _receivedNotificationsLock = new();
    private readonly List<RequestMessage> _receivedRequests = [];
    private readonly object _receivedRequestsLock = new();
    private readonly Dictionary<int, TaskCompletionSource<ResponseMessage>> _pendingServerRequests = [];
    private readonly object _pendingServerRequestsLock = new();
    private readonly TaskCompletionSource<TcpMessageHandler> _handlerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TcpClient? _serverClient;
    private NetworkStream? _serverStream;
    private int _nextServerRequestId = 10000;

    public FakeMtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _formatter = FormatterUtilities.CreateFormatter();

        InitializeResponse = new InitializeResponseArgs(
            ProcessId: 4242,
            ServerInfo: new ServerInfo("FakeMtpServer", "1.2.3"),
            Capabilities: new ServerCapabilities(new ServerTestingCapabilities(
                SupportsDiscovery: true,
                MultiRequestSupport: true,
                VSTestProviderSupport: false,
                SupportsAttachments: true,
                MultiConnectionProvider: false)));

        _ = Task.Run(AcceptAndServeAsync);
    }

    /// <summary>Gets the loopback port the fake server is listening on.</summary>
    public int Port { get; }

    /// <summary>Gets or sets the response returned for an <c>initialize</c> request.</summary>
    public InitializeResponseArgs InitializeResponse { get; set; }

    /// <summary>Gets or sets the response returned for a <c>testing/runTests</c> request.</summary>
    public RunResponseArgs RunResponse { get; set; } = new RunResponseArgs([]);

    /// <summary>
    /// Gets or sets a value indicating whether a <c>testing/runTests</c> request is deliberately left
    /// unanswered, so a test can drive cancellation, a malformed frame, or a disconnect against an in-flight
    /// request.
    /// </summary>
    public bool WithholdRunResponse { get; set; }

    /// <summary>Gets a snapshot of every notification the client has sent to the server.</summary>
    public IReadOnlyList<NotificationMessage> ReceivedNotifications
    {
        get
        {
            lock (_receivedNotificationsLock)
            {
                return _receivedNotifications.ToArray();
            }
        }
    }

    /// <summary>Gets a snapshot of the method names of every request the client has sent to the server.</summary>
    public IReadOnlyList<string> ReceivedRequestMethods
    {
        get
        {
            lock (_receivedRequestsLock)
            {
                return _receivedRequests.Select(request => request.Method).ToArray();
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of every request the client has sent to the server, including the deserialized
    /// <see cref="RequestMessage.Params"/>. Tests use this to assert the exact payload (UID list, graph
    /// filter, ...) reached the wire, not merely that the method name was invoked.
    /// </summary>
    public IReadOnlyList<RequestMessage> ReceivedRequests
    {
        get
        {
            lock (_receivedRequestsLock)
            {
                return _receivedRequests.ToArray();
            }
        }
    }

    /// <summary>
    /// Connects a fresh <see cref="MtpServerClient"/> to this server over loopback TCP. The client's
    /// constructor starts its read loop, so the returned client is immediately live.
    /// </summary>
    public MtpServerClient ConnectClient(MtpServerClientOptions? options = null)
    {
        var tcp = new TcpClient();
        try
        {
            tcp.Connect(IPAddress.Loopback, Port);
            tcp.NoDelay = true;
            NetworkStream stream = tcp.GetStream();

            // A NetworkStream is duplex, so the same stream is used for both the read and write directions.
            var handler = new TcpMessageHandler(tcp, stream, stream, FormatterUtilities.CreateFormatter());
            var connection = new MtpJsonRpcConnection(handler);

            // Ownership of the socket transfers to the returned client (its Dispose closes it). Only if
            // construction throws before we hand it over do we dispose it here.
            return new MtpServerClient(connection, options);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>Pushes a <c>testing/testUpdates/tests</c> notification carrying a discovered node.</summary>
    public Task SendDiscoveredTestNodeAsync(Guid runId, string uid, string displayName)
        => SendTestNodeAsync(runId, uid, displayName, new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance));

    /// <summary>Pushes a <c>testing/testUpdates/tests</c> notification carrying a passed node.</summary>
    public Task SendPassedTestNodeAsync(Guid runId, string uid, string displayName)
        => SendTestNodeAsync(runId, uid, displayName, new PropertyBag(PassedTestNodeStateProperty.CachedInstance));

    /// <summary>Pushes a <c>testing/testUpdates/tests</c> notification carrying a failed node.</summary>
    public Task SendFailedTestNodeAsync(Guid runId, string uid, string displayName, string explanation)
        => SendTestNodeAsync(runId, uid, displayName, new PropertyBag(new FailedTestNodeStateProperty(explanation)));

    /// <summary>
    /// Pushes a <c>testing/testUpdates/tests</c> notification carrying a passed action node that also
    /// carries captured standard output/error and a source-file location, so the client's convenience
    /// accessors (<c>StandardOutput</c>, <c>StandardError</c>, <c>FilePath</c>, <c>LineStart</c>,
    /// <c>LineEnd</c>) can be exercised end to end.
    /// </summary>
    public Task SendPassedTestNodeWithDetailsAsync(
        Guid runId,
        string uid,
        string displayName,
        string standardOutput,
        string standardError,
        string filePath,
        int lineStart,
        int lineEnd)
        => SendTestNodeAsync(runId, uid, displayName, new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new StandardOutputProperty(standardOutput),
            new StandardErrorProperty(standardError),
            new TestFileLocationProperty(filePath, new LinePositionSpan(new LinePosition(lineStart, 0), new LinePosition(lineEnd, 0)))));

    /// <summary>Pushes the completion sentinel (a null <c>Changes</c> array) that the client must skip.</summary>
    public Task SendTestNodesCompletionAsync(Guid runId)
        => WriteAsync(new NotificationMessage(
            JsonRpcMethods.TestingTestUpdatesTests,
            new TestNodeStateChangedEventArgs(runId, null)));

    /// <summary>Pushes a <c>client/log</c> notification at <see cref="LogLevel.Information"/>.</summary>
    public Task SendLogAsync(string message)
        => WriteAsync(new NotificationMessage(
            JsonRpcMethods.ClientLog,
            new LogEventArgs(new ServerLogMessage(LogLevel.Information, message))));

    /// <summary>Pushes a <c>telemetry/update</c> notification.</summary>
    public Task SendTelemetryAsync(string eventName, IDictionary<string, object> metrics)
        => WriteAsync(new NotificationMessage(
            JsonRpcMethods.TelemetryUpdate,
            new TelemetryEventArgs(eventName, metrics)));

    /// <summary>Pushes a <c>testing/testUpdates/attachments</c> notification carrying a single attachment.</summary>
    public Task SendAttachmentAsync(string uri, string producer, string type, string displayName, string? description)
        => WriteAsync(new NotificationMessage(
            JsonRpcMethods.TestingTestUpdatesAttachments,
            new TestsAttachments([new RunTestAttachment(uri, producer, type, displayName, description)])));

    /// <summary>
    /// Sends a server-initiated request to the client and returns a task that completes when the client
    /// answers. Params are null because the client keeps request params as a raw dictionary and the tests only
    /// assert on the method name and the returned result.
    /// </summary>
    public Task<ResponseMessage> SendServerRequestAsync(string method)
    {
        int id = Interlocked.Increment(ref _nextServerRequestId);
        var tcs = new TaskCompletionSource<ResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingServerRequestsLock)
        {
            _pendingServerRequests[id] = tcs;
        }

        _ = WriteAsync(new RequestMessage(id, method, null));
        return tcs.Task;
    }

    /// <summary>
    /// Writes a raw, pre-framed body to the client so a test can inject a malformed message. The
    /// <c>Content-Length</c> header is computed from the UTF-8 body so the client reads exactly this body.
    /// </summary>
    public void SendRawFrame(string jsonBody)
    {
        NetworkStream stream = _serverStream ?? throw new InvalidOperationException("No client is connected yet.");
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\nContent-Type: application/testingplatform\r\n\r\n");

        _writeLock.Wait();
        try
        {
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Closes the connection cleanly (shutting down the send direction produces an EOF the client reads as a
    /// graceful disconnect).
    /// </summary>
    public void CloseConnection()
    {
        try
        {
            _serverClient?.Client?.Shutdown(SocketShutdown.Send);
        }
        catch (Exception)
        {
            // Best-effort teardown; the socket may already be gone.
        }

        try
        {
            _serverStream?.Dispose();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }

        try
        {
            _serverClient?.Close();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }
    }

    /// <summary>Polls until a notification with the given method has been received, or times out.</summary>
    public async Task WaitForNotificationAsync(string method, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_receivedNotificationsLock)
            {
                foreach (NotificationMessage notification in _receivedNotifications)
                {
                    if (notification.Method == method)
                    {
                        return;
                    }
                }
            }

            await Task.Delay(15).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for a '{method}' notification from the client.");
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }

        try
        {
            _serverStream?.Dispose();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }

        try
        {
            _serverClient?.Close();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }

        _writeLock.Dispose();
    }

    private Task SendTestNodeAsync(Guid runId, string uid, string displayName, PropertyBag properties)
    {
        var node = new TestNode
        {
            Uid = uid,
            DisplayName = displayName,
            Properties = properties,
        };
        var change = new TestNodeUpdateMessage(new SessionUid("test-session"), node, null);
        return WriteAsync(new NotificationMessage(
            JsonRpcMethods.TestingTestUpdatesTests,
            new TestNodeStateChangedEventArgs(runId, [change])));
    }

    private async Task AcceptAndServeAsync()
    {
        TcpClient socket;
        try
        {
            socket = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The listener was stopped before a client connected (e.g. the test finished). Leave the
            // handler-ready task pending; nothing will await it.
            return;
        }

        socket.NoDelay = true;
        _serverClient = socket;
        _serverStream = socket.GetStream();
        var handler = new TcpMessageHandler(socket, _serverStream, _serverStream, _formatter);
        _handlerReady.TrySetResult(handler);

        try
        {
            await ReadLoopAsync(handler).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The connection was torn down (client disposed or the test ended). Nothing to do.
        }
    }

    private async Task ReadLoopAsync(TcpMessageHandler handler)
    {
        while (await handler.ReadAsync(CancellationToken.None).ConfigureAwait(false) is { } message)
        {
            switch (message)
            {
                case RequestMessage request:
                    lock (_receivedRequestsLock)
                    {
                        _receivedRequests.Add(request);
                    }

                    await HandleClientRequestAsync(request).ConfigureAwait(false);
                    break;

                case NotificationMessage notification:
                    lock (_receivedNotificationsLock)
                    {
                        _receivedNotifications.Add(notification);
                    }

                    break;

                case ResponseMessage response:
                    CompleteServerRequest(response);
                    break;
            }
        }
    }

    private async Task HandleClientRequestAsync(RequestMessage request)
    {
        object? result;
        if (request.Method == JsonRpcMethods.Initialize)
        {
            result = InitializeResponse;
        }
        else if (request.Method == JsonRpcMethods.TestingDiscoverTests)
        {
            result = new DiscoverResponseArgs();
        }
        else if (request.Method == JsonRpcMethods.TestingRunTests)
        {
            if (WithholdRunResponse)
            {
                return;
            }

            result = RunResponse;
        }
        else
        {
            result = null;
        }

        await WriteAsync(new ResponseMessage(request.Id, result)).ConfigureAwait(false);
    }

    private void CompleteServerRequest(ResponseMessage response)
    {
        TaskCompletionSource<ResponseMessage>? tcs;
        lock (_pendingServerRequestsLock)
        {
            if (!_pendingServerRequests.TryGetValue(response.Id, out tcs))
            {
                return;
            }

            _pendingServerRequests.Remove(response.Id);
        }

        tcs.TrySetResult(response);
    }

    private async Task WriteAsync(RpcMessage message)
    {
        TcpMessageHandler handler = await _handlerReady.Task.ConfigureAwait(false);
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await handler.WriteRequestAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
