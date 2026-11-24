// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class HandshakeMessageSerializer : NamedPipeSerializer<HandshakeMessage>, INamedPipeSerializer
{
    public override int Id => HandshakeMessageFieldsId.MessagesSerializerId;

    protected override HandshakeMessage DeserializeCore(Stream stream)
    {
        Dictionary<byte, string> properties = [];

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            properties.Add(ReadByte(stream), ReadString(stream));
        }

        return new(properties);
    }

    protected override void SerializeCore(HandshakeMessage objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        // Deserializer always expected fieldCount to be present.
        // We must write the count even if Properties is null or empty.
        WriteUShort(stream, (ushort)(objectToSerialize.Properties?.Count ?? 0));

        if (objectToSerialize.Properties is null)
        {
            return;
        }

        foreach (KeyValuePair<byte, string> kvp in objectToSerialize.Properties)
        {
            WriteField(stream, kvp.Key);
            WriteField(stream, kvp.Value);
        }
    }
}
