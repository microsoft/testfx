// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class NamedPipeConnectionTests
{
    [TestMethod]
    [DataRow(SocketError.ConnectionReset, 0)]
    [DataRow(SocketError.ConnectionReset, 2)]
    [DataRow(SocketError.ConnectionReset, 6)]
    [DataRow(SocketError.ConnectionAborted, 0)]
    [DataRow(SocketError.ConnectionAborted, 2)]
    [DataRow(SocketError.ConnectionAborted, 6)]
    public async Task ReadNextMessageAsync_PeerDisconnect_ReturnsEofAndClearsPartialFrame(
        SocketError socketError, int prefixLength)
    {
        using var connection = new ConnectionProbe();
        byte[] frame = CreateVoidFrame();
        var failure = new IOException("Peer disconnected.", new SocketException((int)socketError));
        using var interrupted = new ReadFailureStream(frame.Take(prefixLength).ToArray(), failure);

        Assert.IsNull(await connection.ReadAsync(interrupted));

        using var complete = new MemoryStream(frame);
        Assert.AreSame(VoidResponse.CachedInstance, await connection.ReadAsync(complete));
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 6)]
    [DataRow(true, 0)]
    [DataRow(true, 6)]
    public async Task ReadNextMessageAsync_OtherIoFailure_IsNotSilenced(bool socketFailure, int prefixLength)
    {
        using var connection = new ConnectionProbe();
        var failure = new IOException(
            "Unexpected read failure.",
            socketFailure ? new SocketException((int)SocketError.AccessDenied) : null);
        using var interrupted = new ReadFailureStream(CreateVoidFrame().Take(prefixLength).ToArray(), failure);

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(interrupted));

        Assert.AreSame(failure, actual);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(3)]
    public async Task ReadNextMessageAsync_InvalidFrameLength_IsNotSilenced(int payloadLength)
    {
        using var connection = new ConnectionProbe();
        using var stream = new MemoryStream(BitConverter.GetBytes(payloadLength));

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(stream));

        Assert.AreEqual(
            $"The transport returned an invalid frame payload length of {payloadLength} bytes.",
            failure.Message);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task ReadNextMessageAsync_DeserializerIoFailure_IsNotSilenced(SocketError socketError)
    {
        var failure = new IOException("Deserializer failed.", new SocketException((int)socketError));
        using var connection = new ConnectionProbe(new FailingResponseSerializer(failure));
        using var stream = new MemoryStream(CreateVoidFrame());

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(stream));

        Assert.AreSame(failure, actual);
    }

    private static byte[] CreateVoidFrame()
        => [.. BitConverter.GetBytes(sizeof(int)), .. BitConverter.GetBytes(new VoidResponseSerializer().Id)];

    private sealed class ConnectionProbe : NamedPipeConnectionBase, IDisposable
    {
        public ConnectionProbe(INamedPipeSerializer? serializer = null)
            => RegisterSerializer(serializer ?? new VoidResponseSerializer(), typeof(VoidResponse));

        public Task<object?> ReadAsync(Stream stream)
            => ReadNextMessageAsync(stream, CancellationToken.None);

        public void Dispose()
            => DisposeBuffers();
    }

    private sealed class FailingResponseSerializer(IOException failure) : NamedPipeSerializer<VoidResponse>
    {
        public override int Id => new VoidResponseSerializer().Id;

        protected override VoidResponse DeserializeCore(Stream stream)
            => throw failure;

        protected override void SerializeCore(VoidResponse objectToSerialize, Stream stream)
            => throw new NotSupportedException();
    }

    private sealed class ReadFailureStream(byte[] prefix, IOException failure) : MemoryStream(prefix)
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Position == Length
                ? Task.FromException<int>(failure)
                : base.ReadAsync(buffer, offset, count, cancellationToken);

#if NET
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Position == Length
                ? new ValueTask<int>(Task.FromException<int>(failure))
                : base.ReadAsync(buffer, cancellationToken);
#endif
    }
}
