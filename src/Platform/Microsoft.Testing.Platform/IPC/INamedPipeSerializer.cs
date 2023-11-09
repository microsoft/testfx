// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal interface INamedPipeSerializer
{
    int Id { get; }

    void Serialize(object objectToSerialize, Stream stream);

    object Deserialize(Stream stream);
}
