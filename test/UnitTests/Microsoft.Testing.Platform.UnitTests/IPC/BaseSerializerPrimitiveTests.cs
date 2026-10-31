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
        Assert.ThrowsExactly<EndOfStreamException>(
            () => SerializerProbe.ReadByteValue(new MemoryStream()));
    }

    [TestMethod]
    public void ShippingSerializer_UsesUtf8ByteLengthAndPreservesUnicodeAndNullInIndependentPayloads()
    {
        Type shippingSerializer = GetShippingSerializer();
        MethodInfo write = shippingSerializer.GetMethod("WriteString", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo read = shippingSerializer.GetMethod("ReadString", BindingFlags.Static | BindingFlags.NonPublic)!;
        byte[] expectedBytes = [.. BitConverter.GetBytes(4), 0xCE, 0xA9, 0x00, 0x78];
        using MemoryStream written = new();

        write.Invoke(null, [written, "Ω\0x"]);

        Assert.AreSequenceEqual(expectedBytes, written.ToArray());
        using MemoryStream independentPayload = new(expectedBytes, writable: false);
        Assert.AreEqual("Ω\0x", read.Invoke(null, [independentPayload]));
        Assert.AreEqual(8, independentPayload.Position);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "Negative string payload size")]
    [DataRow(2, DisplayName = "String payload exceeds remaining byte")]
    public void ShippingSerializer_InvalidStringSize_IsRejectedBeforeReadingPayload(int invalidSize)
    {
        MethodInfo read = GetShippingSerializer().GetMethod("ReadString", BindingFlags.Static | BindingFlags.NonPublic)!;
        using MemoryStream stream = new([.. BitConverter.GetBytes(invalidSize), 0x41]);

        TargetInvocationException reflectionFailure = Assert.ThrowsExactly<TargetInvocationException>(() => read.Invoke(null, [stream]));
        InvalidDataException failure = Assert.IsInstanceOfType<InvalidDataException>(reflectionFailure.InnerException);

        Assert.AreEqual($"Payload size {invalidSize} is invalid for the remaining stream.", failure.Message);
        Assert.AreEqual(4, stream.Position);
    }

    private static Type GetShippingSerializer()
    {
        Type type = typeof(ITestApplication).Assembly.GetType("Microsoft.Testing.Platform.IPC.Serializers.BaseSerializer", throwOnError: true)!;
        Assert.AreEqual("Microsoft.Testing.Platform", type.Assembly.GetName().Name);
        return type;
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

        public static byte ReadByteValue(Stream stream) => ReadByte(stream);

        public static void WriteStringValue(Stream stream, string value) => WriteString(stream, value);

        public static string ReadStringValue(Stream stream) => ReadString(stream);

        public static void WriteLongSize(Stream stream) => WriteSize<long>(stream);
    }

    [TestMethod]
    public void BoundedPayload_SeeksAndReadsCannotEscapeOwnedField()
    {
        using MemoryStream stream = new([99, 10, 20, 30, 40, 88]);
        stream.Position = 1;

        int result = FieldProbe.ReadPayload(stream, 4, window =>
        {
            Assert.IsTrue(window.CanRead);
            Assert.IsTrue(window.CanSeek);
            Assert.IsFalse(window.CanWrite);
            Assert.AreEqual(4, window.Length);
            Assert.AreEqual(0, window.Position);
            Assert.AreEqual(10, window.ReadByte());
            Assert.AreEqual(1, window.Seek(1, SeekOrigin.Begin));
            Assert.AreEqual(20, window.ReadByte());
            Assert.AreEqual(1, window.Seek(-1, SeekOrigin.Current));
            Assert.AreEqual(20, window.ReadByte());
            Assert.AreEqual(3, window.Seek(-1, SeekOrigin.End));
            Assert.AreEqual(40, window.ReadByte());
            Assert.AreEqual(-1, window.ReadByte());
            Assert.AreEqual(0, window.Read(new byte[8], 2, 5));
            window.Flush();
            Assert.AreEqual("value", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.Position = -1).ParamName);
            Assert.AreEqual("value", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.Position = 5).ParamName);
            Assert.AreEqual("origin", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.Seek(0, (SeekOrigin)999)).ParamName);
            Assert.ThrowsExactly<NotSupportedException>(() => window.SetLength(2));
            Assert.ThrowsExactly<NotSupportedException>(() => window.Write([1], 0, 1));
            return 17;
        });

        Assert.AreEqual(17, result);
        Assert.AreEqual(5, stream.Position);
        Assert.AreEqual(88, stream.ReadByte());
        Assert.AreEqual(6, stream.Length);
    }

    [TestMethod]
    [DataRow(false, -1)]
    [DataRow(false, 2)]
    [DataRow(true, -1)]
    [DataRow(true, 2)]
    public void ReadFields_MalformedSizeRejectsBeforeCallback(bool stateful, int size)
    {
        using MemoryStream stream = new([.. BitConverter.GetBytes((ushort)1), .. BitConverter.GetBytes((ushort)9), .. BitConverter.GetBytes(size), 42]);
        int invocations = 0;
        string expected = size < 0 ? "Field 9 has a negative size." : "Field 9 extends beyond the end of the stream.";

        Exception error = size < 0
            ? Assert.ThrowsExactly<InvalidDataException>(() => FieldProbe.ReadEnvelope(stream, stateful, () => invocations++))
            : Assert.ThrowsExactly<EndOfStreamException>(() => FieldProbe.ReadEnvelope(stream, stateful, () => invocations++));

        Assert.AreEqual(expected, error.Message);
        Assert.AreEqual(0, invocations);
        Assert.AreEqual(8, stream.Position);
    }

    [TestMethod]
    [DataRow("string")]
    [DataRow("byte")]
    [DataRow("identified-byte")]
    public void WriteNullableFields_OmittedValuesDoNotDisturbPrefix(string shape)
    {
        using MemoryStream stream = new();
        stream.WriteByte(0xAA);
        FieldProbe.WriteNullable(stream, shape, omit: true);
        Assert.AreSequenceEqual(new byte[] { 0xAA }, stream.ToArray());

        FieldProbe.WriteNullable(stream, shape, omit: false);

        byte[] expected = shape switch
        {
            "string" => [0xAA, .. BitConverter.GetBytes(1), 0x41],
            "byte" => [0xAA, 0x7F],
            _ => [0xAA, .. BitConverter.GetBytes((ushort)9), .. BitConverter.GetBytes(1), 0x7F],
        };
        Assert.AreSequenceEqual(expected, stream.ToArray());
    }

    [TestMethod]
    public void PrimitiveSize_UnsupportedStructDoesNotWrite()
    {
        using MemoryStream stream = new();
        stream.WriteByte(0xAA);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => FieldProbe.WriteUnsupportedSize(stream));

        Assert.StartsWith("This program location is thought to be unreachable. File='", error.Message);
        Assert.AreSequenceEqual(new byte[] { 0xAA }, stream.ToArray());
    }

    private sealed class FieldProbe : BaseSerializer
    {
        internal static int ReadPayload(Stream stream, int size, Func<Stream, int> read)
            => ReadFieldPayload(stream, size, read);

        internal static void ReadEnvelope(Stream stream, bool stateful, Action invoked)
        {
            if (stateful)
            {
                _ = ReadFields(stream, 0, (_, _, _, state) =>
                {
                    invoked();
                    return state;
                });
            }
            else
            {
                ReadFields(stream, (_, _) =>
                {
                    invoked();
                    return true;
                });
            }
        }

        internal static void WriteNullable(Stream stream, string shape, bool omit)
        {
            switch (shape)
            {
                case "string":
                    WriteField(stream, omit ? null : "A");
                    break;
                case "byte":
                    WriteField(stream, omit ? null : 0x7F);
                    break;
                default:
                    WriteField(stream, 9, omit ? null : 0x7F);
                    break;
            }
        }

        internal static void WriteUnsupportedSize(Stream stream) => WriteSize<double>(stream);
    }
}
