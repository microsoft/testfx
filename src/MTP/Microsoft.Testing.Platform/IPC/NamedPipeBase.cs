// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

// Serializer registry shared over the 'dotnet test' named pipe. This is the part of the pipe base that is shared as
// source with dotnet/sdk (via RegisterSerializers.RegisterAllSerializers); it is intentionally self-contained (no
// framing/transport, no PipeStream, no platform helpers) so it compiles standalone in a consumer that has no
// reference to Microsoft.Testing.Platform. The framing/transport lives in the repo-local NamedPipeConnectionBase.
[Embedded]
internal abstract class NamedPipeBase
{
    private readonly Dictionary<Type, INamedPipeSerializer> _typeSerializer = [];
    private readonly Dictionary<int, INamedPipeSerializer> _idSerializer = [];

    public void RegisterSerializer(INamedPipeSerializer namedPipeSerializer, Type type)
    {
        _typeSerializer.Add(type, namedPipeSerializer);
        _idSerializer.Add(namedPipeSerializer.Id, namedPipeSerializer);
    }

    protected INamedPipeSerializer GetSerializer(int id)
        => _idSerializer[id];

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer[type];
}
