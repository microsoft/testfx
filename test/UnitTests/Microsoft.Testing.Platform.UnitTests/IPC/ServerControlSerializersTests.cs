// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

using static Microsoft.Testing.Platform.UnitTests.ProtocolSerializerTestHelper;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ServerControlSerializersTests
{
    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)1)]
    [DataRow(byte.MaxValue)]
    public void ServerControlMessageSerializeDeserialize_RoundTripsKind(byte kind)
    {
        ServerControlMessage actual = RoundTrip(new ServerControlMessageSerializer(), new ServerControlMessage(kind));

        Assert.AreEqual(kind, actual.Kind);
    }

    [TestMethod]
    public void ServerControlMessageDeserialize_SkipsUnknownField()
    {
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xAA, 0xBB]),
            (ServerControlMessageFieldsId.Kind, [42]));

        using var stream = new MemoryStream(payload);
        var actual = (ServerControlMessage)Deserialize(new ServerControlMessageSerializer(), stream);

        Assert.AreEqual((byte)42, actual.Kind);
    }

    [TestMethod]
    public void ServerControlMessageDeserialize_WithNoFieldsDefaultsKindToZero()
    {
        using var stream = new MemoryStream(WriteFields());
        var actual = (ServerControlMessage)Deserialize(new ServerControlMessageSerializer(), stream);

        Assert.AreEqual((byte)0, actual.Kind);
    }

    [TestMethod]
    public void WaitForServerControlRequestDeserialize_ReturnsCachedInstance()
    {
        WaitForServerControlRequest actual = RoundTrip(new WaitForServerControlRequestSerializer(), WaitForServerControlRequest.CachedInstance);

        Assert.AreSame(WaitForServerControlRequest.CachedInstance, actual);
    }

    [TestMethod]
    public void WaitForServerControlRequestSerialize_WritesNoBytes()
    {
        using var stream = new MemoryStream();
        Serialize(new WaitForServerControlRequestSerializer(), WaitForServerControlRequest.CachedInstance, stream);

        Assert.AreEqual(0, stream.Length);
    }

    private static byte[] WriteFields(params (ushort Id, byte[] Payload)[] fields)
    {
        using var stream = new MemoryStream();
        WriteUShort(stream, (ushort)fields.Length);

        foreach ((ushort id, byte[] payload) in fields)
        {
            WriteUShort(stream, id);
            WriteInt(stream, payload.Length);
            stream.Write(payload, 0, payload.Length);
        }

        return stream.ToArray();
    }

    private static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteUShort(Stream stream, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
