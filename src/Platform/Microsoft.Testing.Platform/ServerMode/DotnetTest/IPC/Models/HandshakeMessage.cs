// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

namespace Microsoft.Testing.Platform.IPC.Models;

[PipeSerializableMessage("DotNetTestProtocol", 9)]
internal sealed record HandshakeMessage(
    [property: PipePropertyId(1)] Dictionary<byte, string>? Properties)
    : IRequest, IResponse;
