// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !PLATFORM_MSBUILD
using Microsoft.Testing.Platform.Resources;
#endif

namespace Microsoft.Testing.Platform.IPC;

internal abstract class NamedPipeBase
{
    private readonly Dictionary<Type, object> _typeSerializer = [];
    private readonly Dictionary<int, object> _idSerializer = [];

    public void RegisterSerializer<TSerializer, TInput>()
        where TSerializer : INamedPipeSerializer, new()
    {
        INamedPipeSerializer namedPipeSerializer = new TSerializer();
        _typeSerializer.Add(typeof(TInput), namedPipeSerializer);
        _idSerializer.Add(namedPipeSerializer.Id, namedPipeSerializer);
    }

    protected INamedPipeSerializer GetSerializer(int id)
        => _idSerializer.TryGetValue(id, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.NoSerializerRegisteredWithIdErrorMessage,
                id));

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer.TryGetValue(type, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.NoSerializerRegisteredWithTypeErrorMessage,
                type));
}
