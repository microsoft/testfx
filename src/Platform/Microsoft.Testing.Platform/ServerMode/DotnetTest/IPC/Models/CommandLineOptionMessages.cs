// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

namespace Microsoft.Testing.Platform.IPC.Models;

[PipeSerializableMessage(ProtocolConstants.ProtocolName, 31)]
internal sealed record CommandLineOptionMessage(
    [property: PipePropertyId(1)] string? Name,
    [property: PipePropertyId(2)] string? Description,
    [property: PipePropertyId(3)] bool? IsHidden,
    [property: PipePropertyId(4)] bool? IsBuiltIn);

[PipeSerializableMessage(ProtocolConstants.ProtocolName, 3)]
internal sealed record CommandLineOptionMessages(
    [property: PipePropertyId(1)] string? ModulePath,
    [property: PipePropertyId(2)] CommandLineOptionMessage[]? CommandLineOptionMessageList) : IRequest;
