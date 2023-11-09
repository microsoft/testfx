// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal abstract class NamedPipeBase
{
    private readonly Dictionary<Type, object> _typeSerializer = [];
    private readonly Dictionary<int, object> _idSerializer = [];

    public void RegisterSerializer<T>(INamedPipeSerializer namedPipeSerializer)
    {
        _typeSerializer.Add(typeof(T), namedPipeSerializer);
        _idSerializer.Add(namedPipeSerializer.Id, namedPipeSerializer);
    }

    protected INamedPipeSerializer GetSerializer(int id)
        => _idSerializer.TryGetValue(id, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new InvalidOperationException($"No serializer registered with id: {id}");

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer.TryGetValue(type, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new InvalidOperationException($"No serializer registered with type: {type}");
}
