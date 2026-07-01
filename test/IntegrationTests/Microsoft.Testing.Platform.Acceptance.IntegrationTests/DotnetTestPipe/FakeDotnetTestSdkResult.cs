// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Captures everything the fake SDK observed during a single test-app run driven through the
/// <c>--server dotnettestcli --dotnet-test-pipe</c> protocol: the raw frames received on the
/// named pipe, the SDK-selected handshake reply, and the process-level <see cref="TestHostResult"/>.
/// Used by baseline tests to assert current behavior before any source change.
/// </summary>
internal sealed class FakeDotnetTestSdkResult
{
    public FakeDotnetTestSdkResult(
        TestHostResult testHostResult,
        IReadOnlyList<RawMessage> receivedMessages,
        Dictionary<byte, string>? receivedHandshake,
        Dictionary<byte, string>? sentHandshakeReply,
        string? negotiatedProtocolVersion,
        bool serverControlPipeConnected = false,
        bool serverCancelSent = false)
    {
        TestHostResult = testHostResult;
        ReceivedMessages = receivedMessages;
        ReceivedHandshake = receivedHandshake;
        SentHandshakeReply = sentHandshakeReply;
        NegotiatedProtocolVersion = negotiatedProtocolVersion;
        ServerControlPipeConnected = serverControlPipeConnected;
        ServerCancelSent = serverCancelSent;
    }

    /// <summary>The process-level result: exit code, captured stdout, captured stderr.</summary>
    public TestHostResult TestHostResult { get; }

    /// <summary>All raw frames the test app sent over the pipe, in arrival order.</summary>
    public IReadOnlyList<RawMessage> ReceivedMessages { get; }

    /// <summary>The decoded handshake properties the test app sent (advertising its supported
    /// protocol versions, PID, architecture, framework, OS, etc.).</summary>
    public Dictionary<byte, string>? ReceivedHandshake { get; }

    /// <summary>The handshake the fake SDK replied with (carrying the selected protocol version).</summary>
    public Dictionary<byte, string>? SentHandshakeReply { get; }

    /// <summary>The version the fake SDK selected from the test app's advertised list, or null/empty if none.</summary>
    public string? NegotiatedProtocolVersion { get; }

    /// <summary>True when the test app connected back to the reverse server-control pipe the SDK advertised.</summary>
    public bool ServerControlPipeConnected { get; }

    /// <summary>True when the fake SDK pushed a CancelSession control message to the test app.</summary>
    public bool ServerCancelSent { get; }

    /// <summary>Returns all frames whose serializer ID matches <paramref name="serializerId"/>.</summary>
    public IEnumerable<RawMessage> MessagesWithSerializerId(int serializerId)
        => ReceivedMessages.Where(m => m.SerializerId == serializerId);
}
