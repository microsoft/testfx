// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal abstract class NamedPipeSerializer<T> : BaseSerializer, INamedPipeSerializer
    where T : notnull
{
    public abstract int Id { get; }

    internal T Deserialize(Stream stream)
        => DeserializeCore(stream);

    internal void Serialize(T objectToSerialize, Stream stream)
        => SerializeCore(objectToSerialize, stream);

    object INamedPipeSerializer.Deserialize(Stream stream)
        => Deserialize(stream);

    void INamedPipeSerializer.Serialize(object objectToSerialize, Stream stream)
        => Serialize((T)objectToSerialize, stream);

    protected abstract T DeserializeCore(Stream stream);

    protected abstract void SerializeCore(T objectToSerialize, Stream stream);
}
