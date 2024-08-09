// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class HandshakeInfoSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 9;

    public object Deserialize(Stream stream)
    {
        Dictionary<string, string> properties = new();

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            properties.Add(ReadString(stream), ReadString(stream));
        }

        return new HandshakeInfo(properties);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var handshakeInfo = (HandshakeInfo)objectToSerialize;

        if (handshakeInfo.Properties is null || handshakeInfo.Properties.Count == 0)
        {
            return;
        }

        WriteShort(stream, (ushort)handshakeInfo.Properties.Count);
        foreach (KeyValuePair<string, string> property in handshakeInfo.Properties)
        {
            WriteField(stream, property.Key);
            WriteField(stream, property.Value);
        }
    }
}
