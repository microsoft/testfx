// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

using static Microsoft.Testing.Platform.UnitTests.ProtocolSerializerTestHelper;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Guards the <c>BaseSerializer.ReadExactly</c> behavior against regressions. The other serializer round-trip tests
/// only exercise <see cref="MemoryStream"/>, which always satisfies a read request in a single call and therefore
/// never triggers the short-read handling. These tests deserialize through a stream that intentionally returns
/// fewer bytes than requested (partial reads) and through a truncated stream (premature EOF), which are exactly the
/// conditions the centralized read helper exists to handle. On non-NETCOREAPP target frameworks (for example the
/// net462 test run) this exercises the hand-written read-until-complete loop; on NETCOREAPP it exercises the
/// framework <c>Stream.ReadExactly</c> path.
/// </summary>
[TestClass]
public sealed class BaseSerializerPartialReadTests
{
    [TestMethod]
    public void Deserialize_WhenStreamReturnsOneBytePerRead_RoundTripsCorrectly()
    {
        object serializer = new AzureDevOpsLogMessageSerializer();
        var message = new AzureDevOpsLogMessage("MyExecId", "MyInstId", "##[group]A reasonably long log line so several primitive reads span multiple partial reads.");

        byte[] payload = SerializeToBytes(serializer, message);

        // Wrap the payload in a stream that hands back at most one byte per Read call, forcing every primitive read
        // (length prefixes, field ids, and string payloads) to loop until it has the full value.
        using var partialReadStream = new PartialReadStream(new MemoryStream(payload), maxBytesPerRead: 1);
        var actual = (AzureDevOpsLogMessage)Deserialize(serializer, partialReadStream);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.AreEqual(message.LogText, actual.LogText);
    }

    [TestMethod]
    public void Deserialize_WhenStreamIsTruncated_ThrowsEndOfStreamException()
    {
        object serializer = new AzureDevOpsLogMessageSerializer();
        var message = new AzureDevOpsLogMessage("MyExecId", "MyInstId", "##[group]A reasonably long log line that will be cut off mid-read to force an end-of-stream.");

        byte[] payload = SerializeToBytes(serializer, message);

        // Cut the payload in half so a length-prefixed read claims more bytes than are available, driving the reader
        // to the end of the stream before the request is satisfied.
        byte[] truncated = new byte[payload.Length / 2];
        Array.Copy(payload, truncated, truncated.Length);

        using var truncatedStream = new MemoryStream(truncated);
        TargetInvocationException wrapper = Assert.ThrowsExactly<TargetInvocationException>(
            () => Deserialize(serializer, truncatedStream));
        Assert.IsInstanceOfType<EndOfStreamException>(wrapper.InnerException);
    }

    private static byte[] SerializeToBytes<TMessage>(object serializer, TMessage message)
    {
        using var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// A read-only stream decorator that never returns more than <c>maxBytesPerRead</c> bytes from a single
    /// <see cref="Read(byte[], int, int)"/> call, simulating streams (such as network/pipe streams) that satisfy a
    /// read request incrementally. All other stream operations delegate to the wrapped stream.
    /// </summary>
    private sealed class PartialReadStream(Stream inner, int maxBytesPerRead) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => inner.Read(buffer, offset, Math.Min(count, maxBytesPerRead));
    }
}
