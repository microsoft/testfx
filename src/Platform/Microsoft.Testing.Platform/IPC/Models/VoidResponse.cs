// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
[GenerateSerializer(VoidResponseFieldsId.MessagesSerializerId)]
internal sealed class VoidResponse : IResponse
{
    public static readonly VoidResponse CachedInstance = new();
}
