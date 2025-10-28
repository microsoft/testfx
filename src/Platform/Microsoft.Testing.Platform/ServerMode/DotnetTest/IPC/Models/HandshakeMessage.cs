// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
[PipeSerializableMessage("DotNetTestProtocol", 9)]
internal sealed record HandshakeMessage(
    [property: PipePropertyId(1)] Dictionary<byte, string>? Properties) : IRequest, IResponse;
