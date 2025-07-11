// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !PLATFORM_MSBUILD
using Microsoft.Testing.Platform.Resources;
#endif

namespace Microsoft.Testing.Platform.IPC;

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
        => _idSerializer.TryGetValue(id, out INamedPipeSerializer? serializer)
            ? serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.NoSerializerRegisteredWithIdErrorMessage,
                id));

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer.TryGetValue(type, out INamedPipeSerializer? serializer)
            ? serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.NoSerializerRegisteredWithTypeErrorMessage,
                type));
}
