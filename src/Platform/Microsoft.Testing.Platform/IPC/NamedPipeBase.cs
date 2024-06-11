// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

#if !PLATFORM_MSBUILD
using Microsoft.Testing.Platform.Resources;
#endif

namespace Microsoft.Testing.Platform.IPC;

internal abstract class NamedPipeBase
{
    private readonly Dictionary<Type, object> _typeSerializer = [];
    private readonly Dictionary<int, object> _idSerializer = [];

    public void RegisterSerializer(INamedPipeSerializer namedPipeSerializer, Type type)
    {
        _typeSerializer.Add(type, namedPipeSerializer);
        _idSerializer.Add(namedPipeSerializer.Id, namedPipeSerializer);
    }

    protected INamedPipeSerializer GetSerializer(int id)
        => _idSerializer.TryGetValue(id, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
#if PLATFORM_MSBUILD
                "No serializer registered with ID '{0}'",
#else
                PlatformResources.NoSerializerRegisteredWithIdErrorMessage,
#endif
                id));

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer.TryGetValue(type, out object? serializer)
            ? (INamedPipeSerializer)serializer
            : throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
#if PLATFORM_MSBUILD
                "No serializer registered with type '{0}'",
#else
                PlatformResources.NoSerializerRegisteredWithTypeErrorMessage,
#endif
                type));
}
