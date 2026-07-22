// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class BaseSerializerPrimitiveTests
{
    [TestMethod]
    public void WritePrimitives_WritesNativeEndianWireFormat()
    {
        using var stream = new MemoryStream();

        SerializerProbe.WriteIntValue(stream, 0x01020304);
        SerializerProbe.WriteLongValue(stream, 0x0102030405060708);
        SerializerProbe.WriteUShortValue(stream, 0x0102);
        SerializerProbe.WriteBoolValue(stream, false);
        SerializerProbe.WriteBoolValue(stream, true);
        SerializerProbe.WriteStringValue(stream, "A");
        SerializerProbe.WriteLongSize(stream);

        byte[] expected = [.. BitConverter.GetBytes(0x01020304), .. BitConverter.GetBytes(0x0102030405060708), .. BitConverter.GetBytes((ushort)0x0102), 0x00, 0x01, .. BitConverter.GetBytes(1), 0x41, .. BitConverter.GetBytes(sizeof(long))];
        Assert.AreSequenceEqual(
            expected,
            stream.ToArray());
    }

    [TestMethod]
    public void ReadPrimitives_ReadsNativeEndianWireFormat()
    {
        byte[] bytes = [.. BitConverter.GetBytes(0x01020304), .. BitConverter.GetBytes(0x0102030405060708), .. BitConverter.GetBytes((ushort)0x0102), 0xFF, .. BitConverter.GetBytes(1), 0x41];
        using var stream = new MemoryStream(bytes);

        Assert.AreEqual(0x01020304, SerializerProbe.ReadIntValue(stream));
        Assert.AreEqual(0x0102030405060708, SerializerProbe.ReadLongValue(stream));
        Assert.AreEqual((ushort)0x0102, SerializerProbe.ReadUShortValue(stream));
        Assert.IsTrue(SerializerProbe.ReadBoolValue(stream));
        Assert.AreEqual("A", SerializerProbe.ReadStringValue(stream));
    }

    [TestMethod]
    public void ReadPrimitives_WhenStreamIsTruncated_ThrowsEndOfStreamException()
    {
        Assert.ThrowsExactly<EndOfStreamException>(
            () => SerializerProbe.ReadIntValue(new MemoryStream(new byte[sizeof(int) - 1])));
        Assert.ThrowsExactly<EndOfStreamException>(
            () => SerializerProbe.ReadLongValue(new MemoryStream(new byte[sizeof(long) - 1])));
        Assert.ThrowsExactly<EndOfStreamException>(
            () => SerializerProbe.ReadUShortValue(new MemoryStream(new byte[sizeof(ushort) - 1])));
        Assert.ThrowsExactly<EndOfStreamException>(
            () => SerializerProbe.ReadBoolValue(new MemoryStream()));
    }

    private sealed class SerializerProbe : BaseSerializer
    {
        public static void WriteIntValue(Stream stream, int value) => WriteInt(stream, value);

        public static int ReadIntValue(Stream stream) => ReadInt(stream);

        public static void WriteLongValue(Stream stream, long value) => WriteLong(stream, value);

        public static long ReadLongValue(Stream stream) => ReadLong(stream);

        public static void WriteUShortValue(Stream stream, ushort value) => WriteUShort(stream, value);

        public static ushort ReadUShortValue(Stream stream) => ReadUShort(stream);

        public static void WriteBoolValue(Stream stream, bool value) => WriteBool(stream, value);

        public static bool ReadBoolValue(Stream stream) => ReadBool(stream);

        public static void WriteStringValue(Stream stream, string value) => WriteString(stream, value);

        public static string ReadStringValue(Stream stream) => ReadString(stream);

        public static void WriteLongSize(Stream stream) => WriteSize<long>(stream);
    }
}
