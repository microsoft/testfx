// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class HandshakeMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => HandshakeMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        Dictionary<byte, string> properties = [];

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            properties.Add(ReadByte(stream), ReadString(stream));
        }

        return new HandshakeMessage(properties);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var handshakeMessage = (HandshakeMessage)objectToSerialize;

        if (handshakeMessage.Properties is null || handshakeMessage.Properties.Count == 0)
        {
            return;
        }

        WriteUShort(stream, (ushort)handshakeMessage.Properties.Count);
        foreach ((byte key, string value) in handshakeMessage.Properties)
        {
            WriteField(stream, key);
            WriteField(stream, value);
        }
    }
}
