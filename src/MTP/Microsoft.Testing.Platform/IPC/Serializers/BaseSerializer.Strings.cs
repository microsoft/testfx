// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal abstract partial class BaseSerializer
{
    protected static string ReadString(Stream stream)
    {
        int length = ReadInt(stream);
        return ReadStringValue(stream, length);
    }

    protected static string ReadStringValue(Stream stream, int size)
    {
        ValidatePayloadSize(stream, size);

#if NETCOREAPP
        byte[] rentedBytes = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
        try
        {
            ReadExactly(stream, rentedBytes, 0, size);
            return Encoding.UTF8.GetString(rentedBytes, 0, size);
        }

        // Stryker disable once all: returning a temporary pooled buffer changes allocation reuse, not the decoded string.
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rentedBytes);
        }
#else
        byte[] bytes = new byte[size];
        ReadExactly(stream, bytes, 0, size);
        return Encoding.UTF8.GetString(bytes, 0, size);
#endif
    }

    protected static void WriteString(Stream stream, string str)
    {
#if NETCOREAPP
        int byteCount = Encoding.UTF8.GetByteCount(str);
        byte[] rentedBytes = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Encoding.UTF8.GetBytes(str, rentedBytes);
            WriteInt(stream, byteCount);
            stream.Write(rentedBytes, 0, byteCount);
        }

        // Stryker disable once all: returning a temporary pooled buffer changes allocation reuse, not the encoded string.
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rentedBytes);
        }
#else
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        WriteInt(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
#endif
    }

    private static void ValidatePayloadSize(Stream stream, int size)
    {
        if (size < 0 || size > stream.Length - stream.Position)
        {
            throw new InvalidDataException($"Payload size {size} is invalid for the remaining stream.");
        }
    }
}
